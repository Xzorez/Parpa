using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using Parpa.Controls;
using WinUIEx;

namespace Parpa.Services;

/// <summary>
/// Icono junto al reloj de Windows (solo los ojos de la mascota: blanco sobre barra oscura,
/// #1c1c1c sobre barra clara) y su menú (Bienvenida y Bandeja #9b): abrir, ajustes, pausar avisos 1 h,
/// modo minimal, mascota, iniciar con Windows, bienvenida y salir.
/// </summary>
public sealed class TrayService : IDisposable
{
    private const uint IconId = 1;
    private static readonly TimeSpan PauseLength = TimeSpan.FromHours(1);

    private readonly ParpaSettings _s;
    private readonly TrayIcon _icon;
    private bool _lightTaskbar;

    public TrayService(ParpaSettings settings)
    {
        _s = settings;
        _lightTaskbar = TaskbarIsLight();
        _icon = new TrayIcon(IconId, IconPath(), Loc.Get("app_name")) { IsVisible = true };
        _icon.Selected += (_, e) => e.Flyout = BuildMenu();
        _icon.ContextMenu += (_, e) => e.Flyout = BuildMenu();
        // El color de la barra puede cambiar con el modo de Windows.
        Theme.Changed += RefreshIcon;
        SettingsService.Current.Changed += RefreshIcon;
    }

    public static string Version =>
        typeof(TrayService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    private string IconPath() =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Icon", _lightTaskbar ? "tray-light.ico" : "tray-dark.ico");

    private void RefreshIcon()
    {
        var light = TaskbarIsLight();
        if (light == _lightTaskbar) return;
        _lightTaskbar = light;
        _icon.SetIcon(IconPath());
    }

    /// <summary>HKCU\…\Themes\Personalize\SystemUsesLightTheme (modo de la barra de tareas, no de las apps).</summary>
    private static bool TaskbarIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    private static string Up(string text) => text.ToUpper(Loc.Culture);

    /// <summary>Letra ancha y negrísima (estilo Wide de la app).</summary>
    private static TextBlock Wide(string text, double size, string color = "primary") =>
        new() { Style = (Style)Application.Current.Resources["Wide"], Text = text, FontSize = size, Foreground = Theme.Brush(color), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Etiqueta diminuta, ancha y muy espaciada (estilo Tiny de la app).</summary>
    private static TextBlock Tiny(string text, string color = "primary") =>
        new() { Style = (Style)Application.Current.Resources["Tiny"], Text = text, Foreground = Theme.Brush(color), HorizontalAlignment = HorizontalAlignment.Left };


    private Flyout BuildMenu()
    {
        var flyout = new Flyout { FlyoutPresenterStyle = PresenterStyle() };
        Fill(flyout);
        return flyout;
    }

    /// <summary>Menú 260 px estilo Akira: negro con marco de tinta de 1,5 px, radio 12. Se rehace al tocar un interruptor.</summary>
    private void Fill(Flyout flyout)
    {
        var list = new StackPanel();
        flyout.Content = new Border
        {
            Width = 260,
            Padding = new Thickness(6),
            Background = Theme.Brush("windowBg"),
            BorderBrush = Theme.Brush("primary"),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(12),
            Child = list,
        };

        // Cabecera: mascota + PARPA + versión.
        var header = new Grid
        {
            Padding = new Thickness(10, 10, 10, 12),
            Margin = new Thickness(0, 0, 0, 4),
            BorderBrush = Theme.Brush("primary"),
            BorderThickness = new Thickness(0, 0, 0, 1.5),
            ColumnSpacing = 10,
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var face = new DotMatrix
        {
            Columns = MascotFrames.Columns, Rows = MascotFrames.Rows, Merge = DotMerge.Blobs, DotSize = 2, Gap = 1, VerticalAlignment = VerticalAlignment.Center,
            OnBrush = Theme.Brush("primary"), OffBrush = Theme.Brush("glyphOff"), Cells = MascotFrames.Awake(),
        };
        var name = Wide(Up(Loc.Get("app_name")), 16);
        var version = Tiny(Version, "muted");
        version.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(name, 1);
        Grid.SetColumn(version, 2);
        header.Children.Add(face);
        header.Children.Add(name);
        header.Children.Add(version);
        list.Children.Add(header);

        list.Children.Add(Item(Loc.Get("tray_open"), "›", () => { flyout.Hide(); App.Current.OpenNotch(); }));
        list.Children.Add(Item(Loc.Get("tray_open_settings"), "›", () => { flyout.Hide(); App.Current.OpenSettings(); }));

        var paused = _s.PausedUntil > DateTime.Now;
        list.Children.Add(Toggle(Loc.Get("tray_pause_1h"),
            paused ? Loc.Format("tray_paused_until", ("time", ClockService.Format(_s.PausedUntil!.Value, _s.H24))) : string.Empty,
            paused, () => SettingsService.Current.Update(s => s.PausedUntil = paused ? null : DateTime.Now + PauseLength), flyout));
        list.Children.Add(Toggle(Loc.Get("tray_minimal"), string.Empty, _s.Minimal,
            () => SettingsService.Current.Update(s => s.Minimal = !s.Minimal), flyout));
        list.Children.Add(Toggle(Loc.Get("settings_mascot"), string.Empty, _s.Mascot,
            () => SettingsService.Current.Update(s => s.Mascot = !s.Mascot), flyout));
        list.Children.Add(Toggle(Loc.Get("tray_startup"), string.Empty, _s.Startup,
            () => SettingsService.Current.Update(s => s.Startup = !s.Startup), flyout));

        var welcome = Item(Loc.Get("tray_welcome"), string.Empty, () => { flyout.Hide(); App.Current.ShowWelcome(); });
        welcome.BorderBrush = Theme.Brush("primary");
        welcome.BorderThickness = new Thickness(0, 1.5, 0, 0);
        welcome.CornerRadius = new CornerRadius(0, 0, 8, 8);
        list.Children.Add(welcome);
        var quit = Item(Loc.Get("tray_quit"), string.Empty, () => { flyout.Hide(); App.Current.Quit(); });
        list.Children.Add(quit);
    }

    /// <summary>Fila: padding 9 10, radio 8; nombre en letra ancha y mayúsculas; a la derecha, etiqueta diminuta.</summary>
    private static Border Item(string label, string right, Action onClick, FrameworkElement? toggle = null)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock
        {
            Text = Up(label), FontFamily = Res<FontFamily>("FontWide"), FontStretch = Windows.UI.Text.FontStretch.Expanded,
            FontSize = 10.5, FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 40,
            Foreground = Theme.Brush("primary"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (toggle is not null)
        {
            Grid.SetColumn(toggle, 1);
            grid.Children.Add(toggle);
        }
        var rightText = Tiny(Up(right), "secondary");
        rightText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(rightText, 2);
        grid.Children.Add(rightText);

        var item = new Border
        {
            Padding = new Thickness(10, 9, 10, 9),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Child = grid,
        };
        Motion.SmoothBackground(item);
        item.PointerEntered += (_, _) => item.Background = Theme.Brush("navSelected");
        item.PointerExited += (_, _) => item.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        item.Tapped += (_, e) =>
        {
            e.Handled = true;
            SoundService.Play("click");
            onClick();
        };
        return item;
    }

    /// <summary>Fila con interruptor 30×16 (perilla 10 px); el menú se rehace para reflejar el cambio.</summary>
    private Border Toggle(string label, string right, bool on, Action change, Flyout flyout)
    {
        var view = SwitchVisual.Create(on, 32, 18, 10);
        return Item(label, right, () =>
        {
            // La perilla se desliza y después se aplica (el menú se rehace para mostrar el estado nuevo).
            SwitchVisual.Flip(view, !on);
            Motion.AfterTransition(() =>
            {
                change();
                Fill(flyout);
            });
        }, view);
    }

    private static Style PresenterStyle()
    {
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Microsoft.UI.Colors.Transparent)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(12)));
        style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 0d));
        style.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 400d));
        return style;
    }

    public void Dispose() => _icon.Dispose();
}
