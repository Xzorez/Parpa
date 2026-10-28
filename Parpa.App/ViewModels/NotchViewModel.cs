using Microsoft.UI.Dispatching;
using Parpa.Services;

namespace Parpa.ViewModels;

/// <summary>
/// Estados del notch. Prioridad (gana el primero): Arrastre &gt; Abierto &gt; Aviso &gt; Indicador &gt; Actividad &gt; Reposo.
/// Implementados: Abierto, Indicador, Actividad y Reposo; Arrastre y Aviso llegan en fases posteriores.
/// </summary>
public enum NotchState { Idle, Activity, Osd, Open }

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

public sealed class NotchViewModel
{
    private readonly ParpaSettings _s;
    private readonly DispatcherQueueTimer _openTimer;
    private readonly DispatcherQueueTimer _closeTimer;
    private readonly DispatcherQueueTimer _osdTimer;
    private NotchPage _lastPage = NotchPage.Home;
    private bool _isOpen;

    public NotchState State { get; private set; } = NotchState.Idle;

    public NotchPage Page { get; private set; } = NotchPage.Home;

    public NotchActivity? Activity { get; private set; }

    public NotchIndicator? Indicator { get; private set; }

    /// <summary>Se dispara cada vez que cambia el estado, la ventana abierta o el contenido visible.</summary>
    public event Action? Changed;

    public NotchViewModel(ParpaSettings settings)
    {
        _s = settings;
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _openTimer = CreateTimer(dispatcher, () => Open(NotchPage.Home));
        _closeTimer = CreateTimer(dispatcher, Close);
        _osdTimer = CreateTimer(dispatcher, () => { Indicator = null; Update(); });
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

    /// <summary>Indicador del sistema durante <c>osdVisibleMs</c> (1,6 s). Se descarta si el notch está abierto.</summary>
    public void ShowIndicator(NotchIndicator indicator)
    {
        if (_isOpen) return;
        Indicator = indicator;
        _osdTimer.Interval = TimeSpan.FromMilliseconds(Tokens.Current.OsdVisibleMs);
        _osdTimer.Start();
        Update();
    }

    private void Update()
    {
        State = _isOpen ? NotchState.Open
            : Indicator is not null ? NotchState.Osd
            : Activity is not null ? NotchState.Activity
            : NotchState.Idle;
        Changed?.Invoke();
    }

    // ---- Entradas ----

    /// <summary>Hover <c>openTime</c> ms abre en Inicio.</summary>
    public void OnPointerEntered()
    {
        _closeTimer.Stop();
        if (_isOpen) return;
        _openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, _s.OpenTime));
        _openTimer.Start();
    }

    /// <summary>Salir con el ratón cierra tras <c>closeDelay</c> s (si <c>closeOn</c> = "mouse").</summary>
    public void OnPointerExited()
    {
        _openTimer.Stop();
        if (!_isOpen || _s.CloseOn != "mouse") return;
        _closeTimer.Interval = TimeSpan.FromSeconds(Math.Max(0, _s.CloseDelay));
        _closeTimer.Start();
    }

    /// <summary>Clic en el notch: abre en la última ventana.</summary>
    public void OnClicked()
    {
        if (_isOpen) return;
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
        // Al abrir, el indicador visible se descarta.
        _osdTimer.Stop();
        Indicator = null;
        Update();
    }

    private void Close()
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        if (!_isOpen) return;
        _isOpen = false;
        Update();
    }
}
