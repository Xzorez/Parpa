using Microsoft.UI.Dispatching;
using Parpa.Services;

namespace Parpa.ViewModels;

/// <summary>
/// Estados del notch. Prioridad (gana el primero): Arrastre &gt; Abierto &gt; Aviso &gt; Indicador &gt; Actividad &gt; Reposo.
/// Implementados: Abierto, Aviso, Indicador, Actividad y Reposo; Arrastre llega en la fase 4.
/// </summary>
public enum NotchState { Idle, Activity, Osd, Peek, Open }

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
    public static readonly NotchPage Quick = new("quick", "windows_quick", "general", "quick");
}

/// <summary>Actividad en vivo del notch compacto. <see cref="SizeToken"/> da el ancho (tokens.json › notch).</summary>
public sealed record NotchActivity(string SizeToken, string Glyph, string Label, string Sub, bool IsPlaying);

/// <summary>
/// Indicador del sistema (340×40). <see cref="Level"/> 0–100 llena las 16 barras.
/// <see cref="Alert"/> pinta el glifo en acento; <see cref="Success"/> pinta las barras en verde.
/// </summary>
public sealed record NotchIndicator(string Glyph, string Label, string Sub, double Level, string Value, bool Alert = false, bool Success = false);

/// <summary>
/// Aviso (420×112) con dos acciones. Mayor <see cref="Priority"/> se muestra antes
/// (y desplaza a uno de menor prioridad que ya se esté viendo).
/// </summary>
public sealed record NotchPeek(
    string Glyph, string Head, string When, string Body,
    string PrimaryLabel, string SecondaryLabel,
    int Priority, bool IsCall,
    Action? OnPrimary = null, Action? OnSecondary = null)
{
    public const int PriorityMessage = 1;
    public const int PriorityCall = 2;
}

public sealed class NotchViewModel
{
    private readonly ParpaSettings _s;
    private readonly DispatcherQueueTimer _openTimer;
    private readonly DispatcherQueueTimer _closeTimer;
    private readonly DispatcherQueueTimer _osdTimer;
    private readonly DispatcherQueueTimer _peekTimer;
    private readonly List<NotchPeek> _queue = new();
    private NotchPage _lastPage = NotchPage.Home;
    private bool _isOpen;
    private bool _pointerInside;
    private bool _suppressed;
    private TimeSpan _peekTotal;
    private TimeSpan _peekRemaining;
    private DateTime _peekDeadline;
    private bool _peekPaused;

    public NotchState State { get; private set; } = NotchState.Idle;

    public NotchPage Page { get; private set; } = NotchPage.Home;

    public NotchActivity? Activity { get; private set; }

    public NotchIndicator? Indicator { get; private set; }

    public NotchPeek? Peek { get; private set; }

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

    public NotchViewModel(ParpaSettings settings)
    {
        _s = settings;
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
        NotchState.Open => OpenGeometry(Page),
        NotchState.Peek => Size("peek", "notchPeek"),
        NotchState.Osd => Size("osd", "notchCompact"),
        NotchState.Activity => Size(Activity!.SizeToken, "notchCompact"),
        _ => Size("idle", "notchCompact"),
    };

    /// <summary>Mayor forma posible (para dimensionar la ventana).</summary>
    public static NotchGeometry Largest => Size("tasks", "notchOpen");

    public NotchGeometry OpenGeometry(NotchPage page)
    {
        // Inicio crece a 196 si la letra está activada y la tarjeta MÚSICA visible (como el prototipo).
        var token = page == NotchPage.Home && _s.Lyrics && _s.HomeCards.GetValueOrDefault("MÚSICA", true)
            ? "homeLyrics"
            : page.SizeToken;
        return Size(token, "notchOpen");
    }

    private static NotchGeometry Size(string sizeToken, string radiusToken)
    {
        var (w, h) = Tokens.Current.NotchSize(sizeToken);
        return new NotchGeometry(w, h, Tokens.Current.Radius(radiusToken));
    }

    // ---- Contenido ----

    /// <summary>Actividad en vivo (música…); <c>null</c> al terminar la sesión.</summary>
    public void SetActivity(NotchActivity? activity)
    {
        if (Activity == activity) return;
        Activity = activity;
        Update();
    }

    /// <summary>Indicador del sistema durante <c>osdVisibleMs</c> (1,6 s). Se descarta si hay aviso visible o está abierto.</summary>
    public void ShowIndicator(NotchIndicator indicator)
    {
        if (_isOpen || Peek is not null || _suppressed) return;
        Indicator = indicator;
        _osdTimer.Interval = TimeSpan.FromMilliseconds(Tokens.Current.OsdVisibleMs);
        _osdTimer.Start();
        Update();
    }

    /// <summary>
    /// Pone un aviso en la cola. No interrumpe si el notch está abierto o en pantalla completa: espera.
    /// Uno de mayor prioridad (llamada) desplaza al visible, que vuelve al principio de la cola.
    /// </summary>
    public void EnqueuePeek(NotchPeek peek)
    {
        if (Peek is not null && peek.Priority > Peek.Priority)
        {
            _queue.Insert(0, Peek);
            HidePeek();
        }
        _queue.Add(peek);
        TryShowNextPeek();
    }

    /// <summary>Botón principal del aviso (ABRIR).</summary>
    public void PeekPrimary()
    {
        var action = Peek?.OnPrimary;
        EndPeek();
        action?.Invoke();
    }

    /// <summary>Botón secundario del aviso (MÁS TARDE / IGNORAR).</summary>
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
        else TryShowNextPeek();
    }

    private void TryShowNextPeek()
    {
        if (Peek is not null || _isOpen || _suppressed || _queue.Count == 0) return;
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
        Update();
        TryShowNextPeek();
    }

    private void Update()
    {
        State = _isOpen ? NotchState.Open
            : Peek is not null ? NotchState.Peek
            : Indicator is not null ? NotchState.Osd
            : Activity is not null ? NotchState.Activity
            : NotchState.Idle;
        Changed?.Invoke();
    }

    // ---- Entradas ----

    /// <summary>Hover <c>openTime</c> ms abre en Inicio.</summary>
    public void OnPointerEntered()
    {
        _pointerInside = true;
        _closeTimer.Stop();
        if (Peek is not null)
        {
            PausePeek(); // hover pausa el aviso; no abre el notch encima de él
            return;
        }
        if (_isOpen) return;
        _openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, _s.OpenTime));
        _openTimer.Start();
    }

    /// <summary>Salir con el ratón cierra tras <c>closeDelay</c> s (si <c>closeOn</c> = "mouse").</summary>
    public void OnPointerExited()
    {
        _pointerInside = false;
        _openTimer.Stop();
        ResumePeek();
        if (!_isOpen || _s.CloseOn != "mouse") return;
        _closeTimer.Interval = TimeSpan.FromSeconds(Math.Max(0, _s.CloseDelay));
        _closeTimer.Start();
    }

    /// <summary>Clic en el notch: abre en la última ventana.</summary>
    public void OnClicked()
    {
        if (_isOpen || Peek is not null) return; // en un aviso, los clics van a sus botones
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

    /// <summary>Rueda sobre el notch cerrado: abre Inicio (si <c>wheel</c>).</summary>
    public void OnWheel()
    {
        if (!_isOpen && _s.Wheel) Open(NotchPage.Home);
    }

    private void Open(NotchPage page)
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        if (_isOpen && Page == page) return;
        _isOpen = true;
        Page = _lastPage = page;
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
        Update();
        TryShowNextPeek();
    }
}
