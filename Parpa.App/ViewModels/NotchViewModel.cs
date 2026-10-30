using System.Text.Json.Nodes;
using Microsoft.UI.Dispatching;
using Parpa.Services;

namespace Parpa.ViewModels;

/// <summary>
/// Estados del notch. Prioridad (gana el primero): Arrastre &gt; Abierto &gt; Aviso &gt; Indicador &gt; Actividad &gt; Reposo.
/// </summary>
public enum NotchState { Idle, Activity, Minimal, Osd, Peek, Open, Drop }

/// <summary>Qué se arrastra sobre el notch.</summary>
public enum DropKind { File, Text, Link }

/// <summary>Forma objetivo: ancho y alto del cuerpo (sin orejas) y radio de las esquinas inferiores.</summary>
public readonly record struct NotchGeometry(double Width, double Height, double Radius);

/// <summary>Ventanas del notch abierto, con su clave de texto, glifo y token de alto.</summary>
public sealed record NotchPage(string Id, string TitleKey, string Glyph, string SizeToken)
{
    public static readonly NotchPage Home = new("home", "windows_home", "inicio", "home");
    public static readonly NotchPage Clip = new("clip", "windows_clip", "copias", "clipboard");
    public static readonly NotchPage Shelf = new("shelf", "windows_shelf", "bandeja", "shelf");
    public static readonly NotchPage Todo = new("todo", "windows_todo", "tareas", "tasks");
    public static readonly NotchPage Notes = new("notes", "windows_notes", "notas", "notes");
    public static readonly NotchPage Audio = new("audio", "windows_audio", "sonido", "audio");
    public static readonly NotchPage Quick = new("quick", "windows_quick", "general", "quick");

    /// <summary>Ventanas con vista propia.</summary>
    public static readonly IReadOnlyList<NotchPage> Implemented = [Home, Clip, Shelf, Todo, Notes, Audio, Quick];
}

/// <summary>
/// Actividad en vivo del notch compacto. <see cref="SizeToken"/> da el ancho (tokens.json › notch).
/// A la derecha: punto vivo en acento si <see cref="Live"/> (micro/cámara); si no, ecualizador si
/// <see cref="RightText"/> es null, o ese texto en Doto (temporizadores).
/// </summary>
public sealed record NotchActivity(string SizeToken, string Glyph, string Label, string Sub, bool IsPlaying, string? RightText = null, bool Live = false);

/// <summary>
/// Indicador del sistema (340×40). <see cref="Level"/> 0–100 llena las 16 barras.
/// <see cref="Alert"/> pinta el glifo en acento; <see cref="Success"/> pinta las barras en verde.
/// <see cref="Banner"/>: error breve compacto (300×34) con punto rojo y enlace, durante <c>notifDur</c>.
/// </summary>
public sealed record NotchIndicator(string Glyph, string Label, string Sub, double Level, string Value,
    bool Alert = false, bool Success = false, bool Banner = false);

/// <summary>
/// Aviso (420×112) con una o dos acciones. Mayor <see cref="Priority"/> se muestra antes
/// (y desplaza a uno de menor prioridad que ya se esté viendo). Un aviso nuevo con la misma
/// <see cref="Key"/> sustituye al anterior. <see cref="ErrorDot"/>: punto rojo en vez de glifo.
/// </summary>
public sealed record NotchPeek(
    string Glyph, string Head, string When, string Body,
    string PrimaryLabel, string SecondaryLabel,
    int Priority, bool IsCall,
    Action? OnPrimary = null, Action? OnSecondary = null,
    string? Key = null, bool ErrorDot = false)
{
    public const int PriorityInfo = 0;
    public const int PriorityMessage = 1;
    public const int PriorityCall = 2;
}

public sealed class NotchViewModel
{
    public const string ActivityPrivacy = "privacy";
    public const string ActivityMusic = "music";

    /// <summary>Orden de prioridad de las actividades (gana la primera presente). Micro/cámara siempre delante.</summary>
    private static readonly string[] ActivityOrder = [ActivityPrivacy, ActivityMusic];

    /// <summary>Alto del modo minimal (tokens.json › notch › minimal).</summary>
    private const double MinimalHeight = 24;

    /// <summary>Ancho del error breve compacto (design/Icono y Estados.dc.html #11f).</summary>
    private const double BannerWidth = 300;

    private static readonly TimeSpan WheelThrottle = TimeSpan.FromMilliseconds(350);

    private readonly ParpaSettings _s;
    private readonly JsonArray _wins;
    private readonly DispatcherQueueTimer _openTimer;
    private readonly DispatcherQueueTimer _closeTimer;
    private readonly DispatcherQueueTimer _osdTimer;
    private readonly DispatcherQueueTimer _peekTimer;
    private readonly List<NotchPeek> _queue = new();
    private readonly Dictionary<string, NotchActivity> _activities = new();
    private NotchPage _lastPage = NotchPage.Home;
    private bool _isOpen;
    private bool _pointerInside;
    private bool _suppressed;
    private bool _holdOpen;
    private bool _passive;
    private DateTime _lastWheel;
    private TimeSpan _peekTotal;
    private TimeSpan _peekRemaining;
    private DateTime _peekDeadline;
    private bool _peekPaused;

    public NotchState State { get; private set; } = NotchState.Idle;

    public NotchPage Page { get; private set; } = NotchPage.Home;

    /// <summary>Menú de burbujas abierto (dentro del estado Abierto).</summary>
    public bool IsLauncher { get; private set; }

    public NotchActivity? Activity { get; private set; }

    public NotchIndicator? Indicator { get; private set; }

    public NotchPeek? Peek { get; private set; }

    public DropKind? Drop { get; private set; }

    /// <summary>Fracción de tiempo que le queda al aviso visible (para la barra de puntos).</summary>
    public double PeekRemainingFraction
    {
        get
        {
            if (Peek is null || _peekTotal <= TimeSpan.Zero) return 0;
            var remaining = _peekPaused ? _peekRemaining : _peekDeadline - DateTime.UtcNow;
            return Math.Clamp(remaining / _peekTotal, 0, 1);
        }
    }

    /// <summary>Se dispara cada vez que cambia el estado, la ventana abierta o el contenido visible.</summary>
    public event Action? Changed;

    public NotchViewModel(ParpaSettings settings, JsonArray wins)
    {
        _s = settings;
        _wins = wins;
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _openTimer = CreateTimer(dispatcher, () => Open(NotchPage.Home));
        _closeTimer = CreateTimer(dispatcher, Close);
        _osdTimer = CreateTimer(dispatcher, () => { Indicator = null; Update(); });
        _peekTimer = CreateTimer(dispatcher, EndPeek);
    }

    private static DispatcherQueueTimer CreateTimer(DispatcherQueue dispatcher, Action tick)
    {
        var timer = dispatcher.CreateTimer();
        timer.IsRepeating = false;
        timer.Tick += (_, _) => tick();
        return timer;
    }

    public bool IsOpen => State == NotchState.Open;

    public NotchGeometry Geometry => State switch
    {
        NotchState.Drop => Size("drop", "notchPeek"),
        NotchState.Open => IsLauncher ? Size("launcher", "notchOpen") : Size(Page.SizeToken, "notchOpen"),
        NotchState.Peek => Size("peek", "notchPeek"),
        NotchState.Osd when Indicator!.Banner => new NotchGeometry(BannerWidth, Tokens.Current.NotchSize("idle").H, Tokens.Current.Radius("notchCompact")),
        NotchState.Osd => Size("osd", "notchCompact"),
        NotchState.Activity => Size(Activity!.SizeToken, "notchCompact"),
        // Minimal: max(96, 0,6 × ancho compacto) × 24, radio 12.
        NotchState.Minimal => new NotchGeometry(
            Math.Max(96, 0.6 * Tokens.Current.NotchSize(Activity?.SizeToken ?? "idle").W),
            MinimalHeight,
            Tokens.Current.Radius("notchMinimal")),
        _ => Size("idle", "notchCompact"),
    };

    /// <summary>Mayor forma posible (para dimensionar la ventana).</summary>
    public static NotchGeometry Largest => Size("tasks", "notchOpen");

    private static NotchGeometry Size(string sizeToken, string radiusToken)
    {
        var (w, h) = Tokens.Current.NotchSize(sizeToken);
        return new NotchGeometry(w, h, Tokens.Current.Radius(radiusToken));
    }

    /// <summary>
    /// Ventanas visibles en el orden de Ajustes (<c>wins</c>: [id, nombre, visible]). Inicio siempre va primero.
    /// </summary>
    public IReadOnlyList<NotchPage> Pages
    {
        get
        {
            var pages = new List<NotchPage> { NotchPage.Home };
            foreach (var entry in _wins.OfType<JsonArray>())
            {
                var id = entry[0]?.GetValue<string>();
                var visible = entry.Count > 2 && entry[2]?.GetValue<bool>() == true;
                if (visible && NotchPage.Implemented.FirstOrDefault(p => p.Id == id && p != NotchPage.Home) is { } page)
                    pages.Add(page);
            }
            return pages;
        }
    }

    // ---- Contenido ----

    /// <summary>Actividad en vivo de una fuente (música, foco…); <c>null</c> al terminar. Gana la de más prioridad.</summary>
    public void SetActivity(string slot, NotchActivity? activity)
    {
        if (activity is null) _activities.Remove(slot);
        else _activities[slot] = activity;
        var next = ActivityOrder.Select(s => _activities.GetValueOrDefault(s)).FirstOrDefault(a => a is not null);
        if (next == Activity) return;
        Activity = next;
        Update();
    }

    /// <summary>
    /// Indicador del sistema durante <c>osdVisibleMs</c> (1,6 s); los errores breves duran <c>notifDur</c>.
    /// Se descarta si hay aviso visible o el notch está abierto.
    /// </summary>
    public void ShowIndicator(NotchIndicator indicator)
    {
        if (_isOpen || Peek is not null || _suppressed || Drop is not null) return;
        Indicator = indicator;
        _osdTimer.Interval = indicator.Banner
            ? TimeSpan.FromSeconds(Math.Max(1, _s.NotifDur))
            : TimeSpan.FromMilliseconds(Tokens.Current.OsdVisibleMs);
        _osdTimer.Start();
        Update();
    }

    /// <summary>
    /// Pone un aviso en la cola. No interrumpe si el notch está abierto o en pantalla completa: espera.
    /// Uno de mayor prioridad (llamada) desplaza al visible, que vuelve al principio de la cola.
    /// </summary>
    public void EnqueuePeek(NotchPeek peek)
    {
        if (peek.Key is { } key)
        {
            _queue.RemoveAll(p => p.Key == key);
            if (Peek?.Key == key) HidePeek();
        }
        if (Peek is not null && peek.Priority > Peek.Priority)
        {
            _queue.Insert(0, Peek);
            HidePeek();
        }
        _queue.Add(peek);
        if (Peek is null) TryShowNextPeek(update: true);
    }

    /// <summary>Botón principal del aviso.</summary>
    public void PeekPrimary()
    {
        var action = Peek?.OnPrimary;
        EndPeek();
        action?.Invoke();
    }

    /// <summary>Botón secundario del aviso.</summary>
    public void PeekSecondary()
    {
        var action = Peek?.OnSecondary;
        EndPeek();
        action?.Invoke();
    }

    /// <summary>Pantalla completa con <c>hideFull</c>: el aviso visible vuelve a la cola y no sale ninguno.</summary>
    public void SetSuppressed(bool suppressed)
    {
        if (_suppressed == suppressed) return;
        _suppressed = suppressed;
        if (suppressed && Peek is not null)
        {
            _queue.Insert(0, Peek);
            HidePeek();
            Update();
        }
        else TryShowNextPeek(update: true);
    }

    private bool TryShowNextPeek(bool update)
    {
        if (Peek is not null || _isOpen || _suppressed || Drop is not null || _queue.Count == 0)
        {
            if (update) Update();
            return false;
        }
        var next = _queue.OrderByDescending(p => p.Priority).First(); // estable: a igual prioridad, el más antiguo
        _queue.Remove(next);
        Peek = next;

        _osdTimer.Stop();
        Indicator = null;
        _peekTotal = TimeSpan.FromSeconds(Math.Max(1, _s.NotifDur));
        _peekRemaining = _peekTotal;
        _peekPaused = true;
        if (!_pointerInside) ResumePeek(); // con el ratón encima el tiempo no corre
        Update();
        return true;
    }

    private void PausePeek()
    {
        if (Peek is null || _peekPaused) return;
        _peekRemaining = _peekDeadline - DateTime.UtcNow;
        _peekPaused = true;
        _peekTimer.Stop();
    }

    private void ResumePeek()
    {
        if (Peek is null || !_peekPaused) return;
        _peekPaused = false;
        _peekDeadline = DateTime.UtcNow + _peekRemaining;
        _peekTimer.Interval = _peekRemaining > TimeSpan.Zero ? _peekRemaining : TimeSpan.FromMilliseconds(1);
        _peekTimer.Start();
    }

    private void HidePeek()
    {
        _peekTimer.Stop();
        Peek = null;
    }

    private void EndPeek()
    {
        if (Peek is null) return;
        HidePeek();
        TryShowNextPeek(update: true);
    }

    /// <summary>
    /// Aviso de micro/cámara sobre una pantalla completa: se ve unos segundos pero el notch
    /// no se abre con el ratón ni la rueda, para no tapar el vídeo ni robarle clics.
    /// </summary>
    public void SetPassive(bool passive)
    {
        if (_passive == passive) return;
        _passive = passive;
        if (passive) Close();
    }

    /// <summary>Un ajuste cambió (minimal, ventanas visibles…): recalcula el estado.</summary>
    public void Refresh()
    {
        if (_isOpen && !Pages.Contains(Page)) Page = NotchPage.Home;
        Update();
    }

    private void Update()
    {
        State = Drop is not null ? NotchState.Drop
            : _isOpen ? NotchState.Open
            : Peek is not null ? NotchState.Peek
            : Indicator is not null ? NotchState.Osd
            : _s.Minimal ? NotchState.Minimal // sustituye a Actividad y Reposo
            : Activity is not null ? NotchState.Activity
            : NotchState.Idle;
        Changed?.Invoke();
    }

    // ---- Ventanas ----

    /// <summary>Botón central: abre o cierra el menú de burbujas.</summary>
    public void ToggleLauncher()
    {
        if (!_isOpen) return;
        IsLauncher = !IsLauncher;
        Update();
    }

    /// <summary>Elige una ventana (burbuja, mascota, "VER" de un aviso…). Abre el notch si hace falta.</summary>
    public void ShowPage(NotchPage page)
    {
        IsLauncher = false;
        if (!_isOpen)
        {
            Open(page);
            return;
        }
        Page = _lastPage = page;
        Update();
    }

    /// <summary>Rueda sobre el notch: cerrado abre Inicio; abierto cambia de ventana (si <c>wheel</c>).</summary>
    public void OnWheel(int delta)
    {
        if (!_s.Wheel || _passive || DateTime.UtcNow - _lastWheel < WheelThrottle) return;
        _lastWheel = DateTime.UtcNow;
        if (!_isOpen)
        {
            if (Peek is null) Open(NotchPage.Home);
            return;
        }
        var pages = Pages;
        var index = Math.Max(0, pages.ToList().IndexOf(Page));
        ShowPage(pages[(index + (delta < 0 ? 1 : -1) + pages.Count) % pages.Count]);
    }

    /// <summary>
    /// Mientras hay un campo de texto en uso (nota, nueva tarea) el notch no se cierra al salir el ratón.
    /// </summary>
    public void SetHoldOpen(bool hold)
    {
        _holdOpen = hold;
        if (hold) _closeTimer.Stop();
        else if (!_pointerInside) OnPointerExited();
    }

    // ---- Arrastrar ----

    public void DragEntered(DropKind kind)
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        if (Drop == kind) return;
        Drop = kind;
        if (Peek is not null)
        {
            _queue.Insert(0, Peek);
            HidePeek();
        }
        _osdTimer.Stop();
        Indicator = null;
        Update();
    }

    /// <param name="openPage">Ventana a la que lleva lo soltado (o <c>null</c> si se canceló).</param>
    public void DragEnded(NotchPage? openPage)
    {
        if (Drop is null) return;
        Drop = null;
        if (openPage is not null)
        {
            ShowPage(openPage);
            return;
        }
        if (!TryShowNextPeek(update: false)) Update();
    }

    // ---- Entradas ----

    /// <summary>Hover <c>openTime</c> ms abre en Inicio.</summary>
    public void OnPointerEntered()
    {
        _pointerInside = true;
        _closeTimer.Stop();
        if (_passive) return;
        if (Peek is not null)
        {
            PausePeek(); // hover pausa el aviso; no abre el notch encima de él
            return;
        }
        if (_isOpen || Drop is not null) return;
        // En minimal se abre sin espera (como el prototipo): la línea es fina y cuesta acertar.
        _openTimer.Interval = TimeSpan.FromMilliseconds(_s.Minimal ? 1 : Math.Max(1, _s.OpenTime));
        _openTimer.Start();
    }

    /// <summary>Salir con el ratón cierra tras <c>closeDelay</c> s (si <c>closeOn</c> = "mouse").</summary>
    public void OnPointerExited()
    {
        _pointerInside = false;
        _openTimer.Stop();
        ResumePeek();
        if (!_isOpen || _holdOpen || _s.CloseOn != "mouse") return;
        _closeTimer.Interval = TimeSpan.FromSeconds(Math.Max(0, _s.CloseDelay));
        _closeTimer.Start();
    }

    /// <summary>Clic en el notch: abre en la última ventana.</summary>
    public void OnClicked()
    {
        if (_isOpen || Peek is not null || _passive) return; // en un aviso, los clics van a sus botones
        Open(_lastPage);
    }

    /// <summary>Atajo (Win+Alt+N por defecto): alterna, abriendo en la última ventana.</summary>
    public void OnHotkey()
    {
        if (_isOpen) Close();
        else Open(_lastPage);
    }

    public void OnClickedOutside()
    {
        if (_isOpen) Close();
    }

    /// <summary>Esc o el sistema piden cerrar.</summary>
    public void RequestClose() => Close();

    private void Open(NotchPage page)
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        if (_isOpen && Page == page && !IsLauncher) return;
        _isOpen = true;
        IsLauncher = false;
        Page = _lastPage = Pages.Contains(page) ? page : NotchPage.Home;
        // Al abrir, el indicador visible se descarta y el aviso visible espera en la cola.
        _osdTimer.Stop();
        Indicator = null;
        if (Peek is not null)
        {
            _queue.Insert(0, Peek);
            HidePeek();
        }
        Update();
    }

    private void Close()
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        if (!_isOpen) return;
        _isOpen = false;
        IsLauncher = false;
        _holdOpen = false;
        if (!TryShowNextPeek(update: false)) Update();
    }
}
