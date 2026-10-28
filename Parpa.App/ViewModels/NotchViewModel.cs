using Microsoft.UI.Dispatching;
using Parpa.Services;

namespace Parpa.ViewModels;

/// <summary>
/// Estados del notch. Prioridad (gana el primero): Arrastre &gt; Abierto &gt; Aviso &gt; Indicador &gt; Actividad &gt; Reposo.
/// Fase 1 implementa Reposo ↔ Abierto; el resto llega en fases posteriores.
/// </summary>
public enum NotchState { Idle, Open }

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

public sealed class NotchViewModel
{
    private readonly ParpaSettings _s;
    private readonly DispatcherQueueTimer _openTimer;
    private readonly DispatcherQueueTimer _closeTimer;
    private NotchPage _lastPage = NotchPage.Home;

    public NotchState State { get; private set; } = NotchState.Idle;

    public NotchPage Page { get; private set; } = NotchPage.Home;

    /// <summary>Se dispara cada vez que cambia el estado o la ventana abierta.</summary>
    public event Action? Changed;

    public NotchViewModel(ParpaSettings settings)
    {
        _s = settings;
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _openTimer = dispatcher.CreateTimer();
        _openTimer.IsRepeating = false;
        _openTimer.Tick += (_, _) => Open(NotchPage.Home);
        _closeTimer = dispatcher.CreateTimer();
        _closeTimer.IsRepeating = false;
        _closeTimer.Tick += (_, _) => Close();
    }

    public bool IsOpen => State == NotchState.Open;

    public NotchGeometry Geometry => State switch
    {
        NotchState.Open => OpenGeometry(Page),
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

    // ---- Entradas ----

    /// <summary>Hover <c>openTime</c> ms abre en Inicio.</summary>
    public void OnPointerEntered()
    {
        _closeTimer.Stop();
        if (IsOpen) return;
        _openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, _s.OpenTime));
        _openTimer.Start();
    }

    /// <summary>Salir con el ratón cierra tras <c>closeDelay</c> s (si <c>closeOn</c> = "mouse").</summary>
    public void OnPointerExited()
    {
        _openTimer.Stop();
        if (!IsOpen || _s.CloseOn != "mouse") return;
        _closeTimer.Interval = TimeSpan.FromSeconds(Math.Max(0, _s.CloseDelay));
        _closeTimer.Start();
    }

    /// <summary>Clic en el notch: abre en la última ventana.</summary>
    public void OnClicked()
    {
        if (IsOpen) return;
        Open(_lastPage);
    }

    /// <summary>Atajo (Win+Alt+N por defecto): alterna, abriendo en la última ventana.</summary>
    public void OnHotkey()
    {
        if (IsOpen) Close();
        else Open(_lastPage);
    }

    public void OnClickedOutside()
    {
        if (IsOpen) Close();
    }

    /// <summary>Rueda sobre el notch cerrado: abre Inicio (si <c>wheel</c>).</summary>
    public void OnWheel()
    {
        if (!IsOpen && _s.Wheel) Open(NotchPage.Home);
    }

    private void Open(NotchPage page)
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        if (IsOpen && Page == page) return;
        State = NotchState.Open;
        Page = _lastPage = page;
        Changed?.Invoke();
    }

    private void Close()
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        if (!IsOpen) return;
        State = NotchState.Idle;
        Changed?.Invoke();
    }
}
