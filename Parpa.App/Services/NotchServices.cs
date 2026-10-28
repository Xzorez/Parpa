using Microsoft.UI.Xaml.Controls;
using Parpa.ViewModels;

namespace Parpa.Services;

/// <summary>La ventana del notch: puede tomar el teclado para un campo de texto y devolverlo al terminar.</summary>
public interface IKeyboardHost
{
    /// <summary>Activa la ventana (solo ahora) y pone el foco en <paramref name="control"/>.</summary>
    void BeginTextInput(Control control);

    /// <summary>Devuelve el foco a la app de antes.</summary>
    void EndTextInput();
}

/// <summary>Servicios que usan las ventanas del notch abierto.</summary>
public sealed record NotchServices(
    NotchViewModel Vm,
    ParpaSettings Settings,
    IKeyboardHost Keyboard,
    SystemMonitor System,
    NoticeCenter Notices,
    ClipboardService Clipboard,
    ShelfService Shelf,
    TasksService Tasks,
    NotesService Notes,
    FocusService Focus);

/// <summary>Una ventana del notch abierto (Inicio, Portapapeles…).</summary>
public interface INotchPageView
{
    /// <summary>Se va a mostrar: refrescar datos y arrancar lo que se anime.</summary>
    void OnShown();

    /// <summary>Se oculta: parar temporizadores.</summary>
    void OnHidden();

    /// <summary>Texto a la derecha de la cabecera en vez del reloj (p. ej. "5 / 5" con todo hecho).</summary>
    (string Text, Windows.UI.Color Color)? HeaderRight => null;

    /// <summary>Elementos que entran escalonados al llegar a esta ventana (filas, tarjetas, mosaicos).</summary>
    IEnumerable<Microsoft.UI.Xaml.UIElement> EntranceItems => [];
}
