using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Parpa.Services;
using Parpa.ViewModels;
using Windows.UI;

namespace Parpa.Views;

/// <summary>Notch abierto: cabecera (ventana, menú, reloj) y la ventana elegida o el menú de burbujas.</summary>
public sealed partial class OpenView : UserControl
{
    private readonly NotchServices _app;
    private readonly Dictionary<NotchPage, FrameworkElement> _pages;
    private readonly LauncherView _launcher;
    private FrameworkElement? _current;
    private bool _menuHover;

    public OpenView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        var tasks = new TasksView(app);
        tasks.HeaderChanged += UpdateHeaderRight;
        Shelf = new ShelfView(app);
        _pages = new Dictionary<NotchPage, FrameworkElement>
        {
            [NotchPage.Home] = new HomeView(app),
            [NotchPage.Clip] = new ClipView(app),
            [NotchPage.Shelf] = Shelf,
            [NotchPage.Todo] = tasks,
            [NotchPage.Notes] = new NotesView(app),
        };
        _launcher = new LauncherView(app);
        foreach (var view in _pages.Values.Append(_launcher))
        {
            view.Visibility = Visibility.Collapsed;
            Body.Children.Add(view);
        }
        Loc.LanguageChanged += () => Show(_app.Vm.Page, _app.Vm.IsLauncher);
    }

    public ShelfView Shelf { get; }

    /// <summary>Muestra una ventana (o el menú) y avisa a la anterior para que pare.</summary>
    public void Show(NotchPage page, bool launcher)
    {
        FrameworkElement next = launcher ? _launcher : _pages.GetValueOrDefault(page) ?? _pages[NotchPage.Home];
        if (next != _current)
        {
            if (_current is INotchPageView old) old.OnHidden();
            if (_current is not null) _current.Visibility = Visibility.Collapsed;
            _current = next;
            next.Visibility = Visibility.Visible;
        }
        (next as INotchPageView)?.OnShown();

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
    }

    public void Apply(ParpaSettings s) =>
        Clock.Text = s.Clock ? ClockService.Format(DateTime.Now, s.H24) : string.Empty;

    private void UpdateHeaderRight()
    {
        var right = _current is INotchPageView view && _current != _launcher ? view.HeaderRight : null;
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
        var active = _current == _launcher;
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
