using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;
using Parpa.ViewModels;
using Windows.UI;

namespace Parpa.Views;

/// <summary>Notch abierto: cabecera (ventana, menú, reloj) y la ventana elegida o el menú de burbujas.</summary>
public sealed partial class OpenView : UserControl
{
    private readonly NotchServices _app;
    private readonly Dictionary<NotchPage, FrameworkElement> _pages = new();
    private LauncherView? _launcher;
    private FrameworkElement? _current;
    private bool _menuHover;

    public OpenView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        Motion.SmoothBackground(MenuButton);
        Motion.PressFeedback(MenuButton);
        // Solo con el notch abierto: cerrado, Show arrancaría los temporizadores de una ventana que no se ve.
        Loc.LanguageChanged += () => { if (!_hidden) Show(_app.Vm.Page, _app.Vm.IsLauncher); };
    }

    public ShelfView Shelf => (ShelfView)Page(NotchPage.Shelf);

    /// <summary>
    /// Cada ventana se crea la primera vez que se abre (objetivo &lt; 100 MB: el árbol XAML de una ventana
    /// que nunca se visita no ocupa memoria). Después se conserva, oculta, para volver a ella al instante.
    /// Descartarlas al cerrar el notch no ayuda: el montón nativo de XAML no devuelve esa memoria y, al
    /// recrearlas, los privados acaban ~8 MB más altos.
    /// </summary>
    private FrameworkElement Page(NotchPage page)
    {
        if (_pages.TryGetValue(page, out var view)) return view;
        view = page.Id switch
        {
            "clip" => new ClipView(_app),
            "shelf" => new ShelfView(_app),
            "todo" => CreateTasks(),
            "notes" => new NotesView(_app),
            "quick" => new QuickView(_app),
            _ => _pages.GetValueOrDefault(NotchPage.Home) ?? new HomeView(_app),
        };
        _pages[page] = view;
        if (view.Parent is null) Add(view);
        return view;
    }

    private TasksView CreateTasks()
    {
        var tasks = new TasksView(_app);
        tasks.HeaderChanged += UpdateHeaderRight;
        return tasks;
    }

    private LauncherView Launcher()
    {
        if (_launcher is null)
        {
            _launcher = new LauncherView(_app);
            Add(_launcher);
        }
        return _launcher;
    }

    private void Add(FrameworkElement view)
    {
        view.Visibility = Visibility.Collapsed;
        Body.Children.Add(view);
    }

    /// <summary>Muestra una ventana (o el menú) y avisa a la anterior para que pare.</summary>
    public void Show(NotchPage page, bool launcher)
    {
        FrameworkElement next = launcher ? Launcher() : Page(page);
        var switched = next != _current;
        var arriving = switched || _hidden;
        if (switched)
        {
            if (_current is INotchPageView old) old.OnHidden();
            if (_current is not null) _current.Visibility = Visibility.Collapsed;
            // Cambio de ventana con el notch ya abierto: la nueva entra con un fundido corto.
            if (_current is not null && !_hidden) Motion.FadeUp(next, 6, TimeSpan.Zero, Motion.Quick);
            _current = next;
            next.Visibility = Visibility.Visible;
        }
        _hidden = false;
        (next as INotchPageView)?.OnShown();
        // Al llegar a una ventana, sus filas/tarjetas entran escalonadas (solo entonces, no en cada refresco).
        if (arriving && next is INotchPageView arrived) Motion.StaggerIn(arrived.EntranceItems);
        if (switched) Motion.FadeUp(PageTitle, 0, TimeSpan.Zero, Motion.Quick, fromX: -6);

        // Cabecera: glifo + título de la ventana; en el menú, solo "MENÚ".
        PageGlyph.Visibility = launcher ? Visibility.Collapsed : Visibility.Visible;
        PageGlyph.Glyph = page.Glyph;
        PageTitle.Text = Loc.Get(launcher ? "windows_menu" : page.TitleKey);
        UpdateMenuButton();
        UpdateHeaderRight();
    }

    /// <summary>Cerrado: la ventana visible para sus temporizadores.</summary>
    public void Hide()
    {
        if (_current is INotchPageView view) view.OnHidden();
        _hidden = true;
    }

    private bool _hidden = true;

    public void Apply(ParpaSettings s) =>
        Clock.Text = s.Clock ? ClockService.Format(DateTime.Now, s.H24) : string.Empty;

    private void UpdateHeaderRight()
    {
        var right = _current is INotchPageView view and not LauncherView ? view.HeaderRight : null;
        HeaderRight.Visibility = right is null ? Visibility.Collapsed : Visibility.Visible;
        Clock.Visibility = right is null ? Visibility.Visible : Visibility.Collapsed;
        if (right is { } r)
        {
            HeaderRight.Text = r.Text;
            HeaderRight.Foreground = new SolidColorBrush(r.Color);
        }
    }

    /// <summary>En el menú el botón queda marcado (#1d1d20) y su glifo en primario.</summary>
    private void UpdateMenuButton()
    {
        var active = _current is LauncherView;
        MenuButton.Background = active ? Res("GlyphOffBrush") : _menuHover ? Res("BorderAltBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        MenuGlyph.OnColor = (Color)Application.Current.Resources[active ? "PrimaryColor" : "SecondaryColor"];
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    private void MenuButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        _app.Vm.ToggleLauncher();
    }

    private void MenuButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _menuHover = true;
        UpdateMenuButton();
    }

    private void MenuButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _menuHover = false;
        UpdateMenuButton();
    }
}
