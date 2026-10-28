using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Parpa.Controls;
using Parpa.Services;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using WinRT.Interop;

namespace Parpa.AppWindows;

/// <summary>
/// Ajustes completos (design/Ajustes Notch.dc.html #7a): 1000×680, barra de título de 40 px,
/// lateral de 230 px con buscador y secciones, y filas de ajuste. Todo se aplica al instante.
/// </summary>
public sealed class SettingsWindow : Window
{
    private const double DesignWidth = 1000;
    private const double DesignHeight = 680;

    private readonly ParpaSettings _s = SettingsService.Current.S;
    private readonly StackPanel _nav = new() { Spacing = 4 };
    private readonly StackPanel _content = new() { Spacing = 18 };
    private readonly TextBlock _title = Ui.Text(string.Empty, 22, semibold: true);
    private readonly TextBlock _toast = Ui.Mono(string.Empty, 10, tracking: 80);
    private readonly TextBox _search = new();
    private readonly TextBlock _windowTitle = Ui.Text(string.Empty, 12);
    private readonly TextBlock _brand = Ui.Doto(string.Empty, 16);
    private readonly TextBlock _version = Ui.Mono(string.Empty, 9, "tertiary");
    private readonly DispatcherTimerLite _toastTimer;
    private string _section = "general";
    private bool _recording;

    /// <summary>Fila de ajuste: etiqueta, descripción y control. <see cref="Opacity"/> &lt; 1 si depende de otro ajuste apagado.</summary>
    private sealed record Row(string Label, string Desc, Func<FrameworkElement> Control, double Opacity = 1);

    private sealed record Head(string Label);

    private static readonly (string Id, string Key, string Glyph)[] Sections =
    [
        ("general", "settings_nav_general", "general"),
        ("position", "settings_nav_position", "sistema"),
        ("look", "settings_nav_look", "brillo"),
        ("alerts", "settings_nav_alerts", "mensaje"),
        ("windows", "settings_nav_windows", "menu"),
        ("sound", "settings_nav_sound", "sonido"),
        ("system", "settings_nav_system", "bateria"),
        ("shortcuts", "settings_nav_shortcuts", "juego"),
    ];

    public SettingsWindow()
    {
        ExtendsContentIntoTitleBar = true;
        var hwnd = WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(DesignWidth * scale), (int)(DesignHeight * scale)));
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icon", "app.ico"));
        CenterOnScreen();
        StyleCaptionButtons();

        _toastTimer = new DispatcherTimerLite(TimeSpan.FromSeconds(2.5), () => _toast.Text = string.Empty);
        Content = BuildShell(out var dragRegion);
        SetTitleBar(dragRegion);

        SettingsService.Current.Changed += Render;
        Loc.LanguageChanged += Render;
        Theme.Changed += OnTheme;
        Closed += (_, _) =>
        {
            SettingsService.Current.Changed -= Render;
            Loc.LanguageChanged -= Render;
            Theme.Changed -= OnTheme;
        };
        ((FrameworkElement)Content).KeyDown += OnKeyDown;
        Render();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void CenterOnScreen()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Move(new Windows.Graphics.PointInt32(
            area.X + (area.Width - AppWindow.Size.Width) / 2,
            area.Y + (area.Height - AppWindow.Size.Height) / 2));
    }

    /// <summary>Botones de la barra (— ▢ ✕) en #8d8d92 sobre transparente; cerrar se pone rojo en hover.</summary>
    private void StyleCaptionButtons()
    {
        var bar = AppWindow.TitleBar;
        bar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        bar.ButtonForegroundColor = Theme.Get("secondary");
        bar.ButtonInactiveForegroundColor = Theme.Get("tertiary");
        bar.ButtonHoverBackgroundColor = Theme.Get("card");
        bar.ButtonHoverForegroundColor = Theme.Get("primary");
        bar.ButtonPressedBackgroundColor = Theme.Get("border");
    }

    private void OnTheme()
    {
        StyleCaptionButtons();
        Render();
    }

    // ---- Estructura ----

    private Grid BuildShell(out FrameworkElement dragRegion)
    {
        var root = new Grid { Background = Theme.Brush("windowBg") };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Barra de título 40 px: mascota + "Ajustes".
        var titleBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Padding = new Thickness(16, 0, 0, 0), Background = Theme.Brush("bg") };
        var logo = new DotMatrix
        {
            Columns = MascotFrames.Columns, Rows = MascotFrames.Rows, DotSize = 2, Gap = 1, VerticalAlignment = VerticalAlignment.Center,
            OnBrush = Theme.Brush("primary"), OffBrush = Theme.Brush("glyphOff"), Cells = MascotFrames.Awake(),
        };
        _windowTitle.VerticalAlignment = VerticalAlignment.Center;
        _windowTitle.FontWeight = Microsoft.UI.Text.FontWeights.Medium;
        titleBar.Children.Add(logo);
        titleBar.Children.Add(_windowTitle);
        root.Children.Add(titleBar);
        dragRegion = titleBar;

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        // Lateral: buscador, secciones y la marca abajo.
        var side = new Grid { Background = Theme.Brush("bg"), Padding = new Thickness(12, 8, 12, 16) };
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _search.Style = (Style)Application.Current.Resources["BareTextBox"];
        _search.FontSize = 12;
        _search.Padding = new Thickness(10, 8, 10, 8);
        _search.MinHeight = 0;
        _search.TextChanged += (_, _) => Render();
        // Buscador: fondo #151517, borde #1e1e21, radio 8, padding 8 10.
        var searchBox = new Border
        {
            Background = Theme.Brush("card"),
            BorderBrush = Theme.Brush("divider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 10),
            Child = _search,
        };
        side.Children.Add(searchBox);
        Grid.SetRow(_nav, 1);
        side.Children.Add(_nav);
        var brand = new StackPanel { Spacing = 2, Padding = new Thickness(10, 0, 10, 0) };
        brand.Children.Add(_brand);
        brand.Children.Add(_version);
        Grid.SetRow(brand, 2);
        side.Children.Add(brand);
        body.Children.Add(side);

        // Contenido: padding 22 32 32, gap 18.
        var header = new Grid();
        header.Children.Add(_title);
        _toast.HorizontalAlignment = HorizontalAlignment.Right;
        _toast.Foreground = new SolidColorBrush(Tokens.Current.Success);
        header.Children.Add(_toast);
        var page = new StackPanel { Spacing = 18, Padding = new Thickness(32, 22, 32, 32) };
        page.Children.Add(header);
        page.Children.Add(_content);
        var scroll = new ScrollViewer { Content = page, HorizontalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(scroll, 1);
        body.Children.Add(scroll);
        return root;
    }

    // ---- Render ----

    private void Render()
    {
        Title = Loc.Get("settings_title");
        _windowTitle.Text = Loc.Get("settings_title");
        _search.PlaceholderText = Loc.Get("settings_search");
        _brand.Text = Loc.Upper(Loc.Get("app_name"));
        _version.Text = $"{Loc.Upper(Loc.Get("settings_version"))} {TrayService.Version}";
        ((Grid)Content).Background = Theme.Brush("windowBg");

        var query = _search.Text.Trim();
        RenderNav(query.Length > 0);
        _content.Children.Clear();

        if (query.Length > 0)
        {
            _title.Text = Loc.Get("settings_results");
            var rows = new StackPanel();
            foreach (var (id, key, _) in Sections)
                foreach (var row in RowsFor(id).OfType<Row>())
                    if ($"{row.Label} {row.Desc}".Contains(query, StringComparison.CurrentCultureIgnoreCase))
                        rows.Children.Add(RowView(row, Loc.Upper(Loc.Get(key))));
            if (rows.Children.Count == 0) rows.Children.Add(new Border { Padding = new Thickness(0, 30, 0, 30), Child = Ui.Mono(Loc.Get("settings_no_results"), 11, "tertiary") });
            _content.Children.Add(rows);
            return;
        }

        _title.Text = Loc.Get(Sections.First(s => s.Id == _section).Key);
        if (_section == "position") _content.Children.Add(MonitorsPanel());
        if (_section == "look") _content.Children.Add(PreviewPanel());
        var list = new StackPanel();
        foreach (var item in RowsFor(_section))
            list.Children.Add(item is Head head ? HeadView(head.Label) : RowView((Row)item, null));
        _content.Children.Add(list);
    }

    /// <summary>Secciones del lateral: glifo + nombre; la elegida con fondo #1a1a1d.</summary>
    private void RenderNav(bool searching)
    {
        _nav.Children.Clear();
        foreach (var (id, key, glyph) in Sections)
        {
            var selected = id == _section && !searching;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new DotMatrix { Glyph = glyph, VerticalAlignment = VerticalAlignment.Center, OnColor = Theme.Get(selected ? "primary" : "secondary") });
            var label = Ui.Text(Loc.Get(key), 13, selected ? "primary" : "soft");
            label.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(label);
            var item = new Border { Padding = new Thickness(10, 9, 10, 9), CornerRadius = new CornerRadius(8), Background = selected ? Theme.Brush("navSelected") : Ui.Clear, Child = row };
            item.PointerEntered += (_, _) => { if (!selected) item.Background = Theme.Brush("card"); };
            item.PointerExited += (_, _) => { if (!selected) item.Background = Ui.Clear; };
            item.Tapped += (_, e) =>
            {
                e.Handled = true;
                SoundService.Play("page");
                _section = id;
                _search.Text = string.Empty;
                Render();
            };
            _nav.Children.Add(item);
        }
    }

    private static Border HeadView(string label) =>
        new() { Padding = new Thickness(0, 18, 0, 8), Child = Ui.Mono(label, 10, tracking: 120, medium: true) };

    /// <summary>Fila: padding 12 0, borde superior #1e1e21; etiqueta 14 px, descripción 12 px #8d8d92.</summary>
    private static Grid RowView(Row row, string? where)
    {
        var grid = new Grid
        {
            Padding = new Thickness(0, 12, 0, 12),
            BorderBrush = Theme.Brush("divider"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            ColumnSpacing = 20,
            Opacity = row.Opacity,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        title.Children.Add(Ui.Text(row.Label, 14));
        if (where is not null) title.Children.Add(Ui.Mono(where, 9, "tertiary"));
        text.Children.Add(title);
        if (row.Desc.Length > 0) text.Children.Add(Ui.Text(row.Desc, 12, "secondary"));
        grid.Children.Add(text);

        var control = row.Control();
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    // ---- Secciones ----

    private static string L(string key) => Loc.Get(key);

    private static void Set(Action<ParpaSettings> change) => SettingsService.Current.Update(change);

    private IEnumerable<object> RowsFor(string section)
    {
        var s = _s;
        switch (section)
        {
            case "general":
                yield return new Head(L("settings_h_language"));
                yield return new Row(L("settings_language"), L("settings_language_d"), () => Ui.Segmented(
                    [("auto", L("settings_lang_auto")), ("es", L("settings_lang_es")), ("en", L("settings_lang_en"))], s.Lang, v => Set(x => x.Lang = v)));
                yield return new Head(L("settings_h_start"));
                yield return new Row(L("settings_startup"), L("settings_startup_d"), () => Toggle(s.Startup, v => Set(x => x.Startup = v)));
                yield return new Head(L("settings_h_openclose"));
                yield return new Row(L("settings_hover_sens"), L("settings_hover_sens_d"), () => Slider(s.HoverSens, 1, 10, 1, v => $"{v:0}/10", v => Set(x => x.HoverSens = v)));
                yield return new Row(L("settings_open_time"), L("settings_open_time_d"), () => Slider(s.OpenTime, 0, 800, 50, v => Loc.Format("time_ms", ("n", (int)v)), v => Set(x => x.OpenTime = v)));
                yield return new Row(L("settings_close_on"), L("settings_close_on_d"), () => Ui.Segmented(
                    [("mouse", L("settings_close_mouse")), ("click", L("settings_close_click"))], s.CloseOn, v => Set(x => x.CloseOn = v)));
                yield return new Row(L("settings_close_delay"), L("settings_close_delay_d"), () => Slider(s.CloseDelay, 0, 2, 0.1,
                    v => Loc.Format("time_sec", ("n", v.ToString("0.0", Loc.Culture))), v => Set(x => x.CloseDelay = v)), s.CloseOn == "mouse" ? 1 : 0.4);
                yield return new Row(L("settings_hide_idle"), L("settings_hide_idle_d"), () => Toggle(s.HideIdle, v => Set(x => x.HideIdle = v)));
                yield return new Head(L("settings_h_dnd"));
                yield return new Row(L("settings_dnd"), L("settings_dnd_d"), () => Toggle(s.Dnd, v => Set(x => x.Dnd = v)));
                break;

            case "position":
                yield return new Head(L("settings_h_position"));
                yield return new Row(L("settings_pos"), L("settings_pos_d"), () => Ui.Segmented(
                    [("left", L("welcome_left")), ("center", L("welcome_center")), ("right", L("welcome_right"))], s.Pos, v => Set(x => x.Pos = v)));
                yield return new Row(L("settings_offset"), L("settings_offset_d"), () => Slider(s.Offset, -100, 100, 10,
                    v => $"{(v > 0 ? "+" : string.Empty)}{v:0} PX", v => Set(x => x.Offset = v)));
                yield return new Row(L("settings_size"), L("settings_size_d"), () => Slider(s.Size, 80, 120, 5, v => $"{v:0}%", v => Set(x => x.Size = v)));
                yield return new Head(L("settings_h_visibility"));
                yield return new Row(L("settings_minimal"), L("settings_minimal_d"), () => Toggle(s.Minimal, v => Set(x => x.Minimal = v)));
                yield return new Row(L("settings_hide_full"), L("settings_hide_full_d"), () => Toggle(s.HideFull, v => Set(x => x.HideFull = v)));
                break;

            case "look":
                yield return new Head(L("settings_h_style"));
                yield return new Row(L("settings_theme"), L("settings_theme_d"), () => Ui.Segmented(
                    [("dark", L("settings_theme_dark")), ("light", L("settings_theme_light")), ("auto", L("settings_theme_auto"))], s.Theme, v => Set(x => x.Theme = v)));
                yield return new Row(L("settings_accent"), L("settings_accent_d"), AccentSwatches);
                yield return new Row(L("settings_anim"), L("settings_anim_d"), () => Ui.Segmented(
                    [("full", L("settings_anim_full")), ("reduced", L("settings_anim_reduced"))], s.Anim, v => Set(x => x.Anim = v)));
                yield return new Head(L("settings_h_mascot"));
                yield return new Row(L("settings_mascot"), L("settings_mascot_d"), () => Toggle(s.Mascot, v => Set(x => x.Mascot = v)));
                yield return new Row(L("settings_mood"), L("settings_mood_d"), () => Ui.Segmented(
                    [("tranquila", L("settings_mood_calm")), ("curiosa", L("settings_mood_curious")), ("dormilona", L("settings_mood_sleepy"))],
                    s.Mood, v => Set(x => x.Mood = v)), s.Mascot ? 1 : 0.4);
                yield return new Head(L("settings_h_clock"));
                yield return new Row(L("settings_clock"), L("settings_clock_d"), () => Toggle(s.Clock, v => Set(x => x.Clock = v)));
                yield return new Row(L("settings_clock_fmt"), string.Empty, () => Ui.Segmented(
                    [(true, L("time_h24")), (false, L("time_h12"))], s.H24, v => Set(x => x.H24 = v)), s.Clock ? 1 : 0.4);
                break;

            case "alerts":
                yield return new Head(Loc.Upper(L("settings_notif_apps")));
                foreach (var app in s.Apps.Keys.ToList())
                    yield return new Row(app, string.Empty, () => Toggle(s.Apps[app], v => Set(x => x.Apps[app] = v)));
                yield return new Row(string.Empty, L("settings_notif_apps_d"), () => new Border());
                yield return new Head(L("settings_h_content"));
                yield return new Row(L("settings_notif_content"), L("settings_content_d"), () => Ui.Segmented(
                    [("full", L("settings_content_full")), ("sender", L("settings_content_sender")), ("hidden", L("settings_content_hidden"))],
                    s.Content, v => Set(x => x.Content = v)));
                yield return new Row(L("settings_notif_dur"), L("settings_notif_dur_d"), () => Slider(s.NotifDur, 2, 10, 0.5,
                    v => Loc.Format("time_sec", ("n", v.ToString("0.0", Loc.Culture))), v => Set(x => x.NotifDur = v)));
                break;

            case "windows":
                yield return new Head(Loc.Upper(L("settings_win_order")));
                foreach (var row in OrderRows()) yield return row;
                yield return new Head(Loc.Upper(L("settings_home_cards")));
                yield return new Row(L("settings_home_cards"), L("settings_home_cards_d"), () => Chips(s.HomeCards, Loc.Upper, (k, v) => Set(x => x.HomeCards[k] = v)));
                yield return new Head(L("home_focus"));
                yield return new Row(L("settings_pomo"), L("settings_pomo_d"), () => Stepper(s.Pomo, 5, 5, 90, v => Set(x => x.Pomo = v)));
                yield return new Head(L("windows_clip"));
                yield return new Row(L("settings_clip_limit"), L("settings_clip_limit_d"), () => Ui.Segmented(
                    [(10, "10"), (25, "25"), (50, "50"), (100, "100")], s.ClipLimit, v => Set(x => x.ClipLimit = v)));
                yield return new Row(L("settings_clip_exclude"), L("settings_clip_exclude_d"), () => Chips(s.ClipExclude, k => k, (k, v) => Set(x => x.ClipExclude[k] = v)));
                yield return new Head(L("windows_shelf"));
                yield return new Row(L("settings_shelf_clear"), L("settings_shelf_clear_d"), () => Toggle(s.ShelfClear, v => Set(x => x.ShelfClear = v)));
                yield return new Row(L("settings_shelf_actions"), L("settings_shelf_actions_d"), () => Chips(s.Actions, k => k, (k, v) => Set(x => x.Actions[k] = v)));
                break;

            case "sound":
                yield return new Row(L("settings_sounds"), L("settings_sounds_d"), () => Toggle(s.Sounds, v => Set(x => x.Sounds = v)));
                yield return new Row(L("settings_volume"), string.Empty, () => Slider(s.Vol, 0, 100, 5, v => $"{v:0}%", v =>
                {
                    Set(x => x.Vol = (int)v);
                    SoundService.Play("tick");
                }), s.Sounds ? 1 : 0.4);
                break;

            case "system":
                yield return new Head(L("settings_h_windows_os"));
                yield return new Row(L("settings_osd"), L("settings_osd_d"), () => Toggle(s.Osd, v => Set(x => x.Osd = v)));
                yield return new Row(L("settings_battery"), L("settings_battery_d"), () => Toggle(s.Battery, v => Set(x => x.Battery = v)));
                yield return new Row(L("settings_wifi"), L("settings_wifi_d"), () => Toggle(s.Wifi, v => Set(x => x.Wifi = v)));
                yield return new Row(L("settings_buds"), L("settings_buds_d"), () => Toggle(s.Buds, v => Set(x => x.Buds = v)));
                yield return new Head(L("settings_h_data"));
                yield return new Row(L("settings_export"), L("settings_export_d"), () => Button(L("settings_export_btn"), ExportAsync));
                yield return new Row(L("settings_import"), L("settings_import_d"), () => Button(L("settings_import_btn"), ImportAsync));
                yield return new Row(L("settings_version"), Loc.Format("settings_version_d", ("v", TrayService.Version)), () => new Border());
                break;

            case "shortcuts":
                yield return new Row(L("settings_hotkey"), L("settings_hotkey_d"), HotkeyControl);
                yield return new Row(L("settings_wheel"), L("settings_wheel_d"), () => Toggle(s.Wheel, v => Set(x => x.Wheel = v)));
                break;
        }
    }

    // ---- Controles ----

    /// <summary>Interruptor 40×22 (perilla 14).</summary>
    private static FrameworkElement Toggle(bool on, Action<bool> set)
    {
        var view = SwitchVisual.Create(on, 40, 22, 14);
        view.Tapped += (_, e) =>
        {
            e.Handled = true;
            SoundService.Play("click");
            set(!on);
        };
        return view;
    }

    /// <summary>16 barras de 6 px (alto 6 → 16) y el valor en Doto 700 15 (ancho 58). Tocar una barra fija el valor.</summary>
    private static FrameworkElement Slider(double value, double min, double max, double step, Func<double, string> format, Action<double> set)
    {
        const int bars = 16;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        var fraction = (value - min) / (max - min);
        for (var i = 0; i < bars; i++)
        {
            var filled = (i + 0.5) / bars <= fraction + 0.001;
            var index = i;
            // Zona de toque más alta que la barra para que sea fácil acertar.
            var hit = new Grid { Height = 22, Background = Ui.Clear };
            hit.Children.Add(new Border
            {
                Width = 6,
                Height = filled ? 6 + Math.Round(i / (double)bars * 10) : 6,
                CornerRadius = new CornerRadius(3),
                Background = Theme.Brush(filled ? "primary" : "border"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            hit.Tapped += (_, e) =>
            {
                e.Handled = true;
                var raw = min + (index + 1) / (double)bars * (max - min);
                set(Math.Round(raw / step) * step);
            };
            dots.Children.Add(hit);
        }
        panel.Children.Add(dots);
        var text = Ui.Doto(format(value), 15);
        text.Width = 58;
        text.TextAlignment = TextAlignment.Right;
        panel.Children.Add(text);
        return panel;
    }

    /// <summary>− valor +: círculos de 26 px con borde y el valor en Doto 700 18.</summary>
    private static FrameworkElement Stepper(int value, int step, int min, int max, Action<int> set)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        panel.Children.Add(Circle("−", () => set(Math.Max(min, value - step))));
        var text = Ui.Doto(Loc.Format("time_min", ("n", value)), 18);
        text.MinWidth = 92;
        text.TextAlignment = TextAlignment.Center;
        panel.Children.Add(text);
        panel.Children.Add(Circle("+", () => set(Math.Min(max, value + step))));
        return panel;
    }

    private static Border Circle(string symbol, Action onTap)
    {
        var label = Ui.Mono(symbol, 13, "soft", 0);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        var circle = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            BorderBrush = Theme.Brush("border"),
            BorderThickness = new Thickness(1),
            Background = Ui.Clear,
            Child = label,
        };
        circle.Tapped += (_, e) =>
        {
            e.Handled = true;
            SoundService.Play("tick");
            onTap();
        };
        return circle;
    }

    /// <summary>Chapas: píldoras con borde; las activas en claro.</summary>
    private static FrameworkElement Chips(Dictionary<string, bool> map, Func<string, string> label, Action<string, bool> set)
    {
        // Filas de hasta 360 px alineadas a la derecha (como flex-wrap del diseño).
        var panel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 360 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        double width = 0;
        foreach (var (key, on) in map.ToList())
        {
            var chip = new PillButton { Text = label(key), Primary = on, Foreground = Theme.Brush("secondary") };
            chip.Click += (_, _) => set(key, !on);
            chip.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            if (width + chip.DesiredSize.Width > 360 && row.Children.Count > 0)
            {
                panel.Children.Add(row);
                row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
                width = 0;
            }
            row.Children.Add(chip);
            width += chip.DesiredSize.Width + 6;
        }
        panel.Children.Add(row);
        return panel;
    }

    private static FrameworkElement Button(string text, Func<Task> action)
    {
        var button = new PillButton { Text = text, Primary = true, Padding = new Thickness(12, 7, 12, 7) };
        button.Click += async (_, _) => await action();
        return button;
    }

    /// <summary>Colores de acento: círculos de 22 px; el elegido con anillo claro.</summary>
    private FrameworkElement AccentSwatches()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        for (var i = 0; i < Tokens.Current.Accents.Count; i++)
        {
            var index = i;
            var selected = _s.Accent == i;
            var swatch = new Grid { Width = 30, Height = 30 };
            swatch.Children.Add(new Ellipse { Width = 30, Height = 30, Stroke = selected ? Theme.Brush("primary") : null, StrokeThickness = 2 });
            swatch.Children.Add(new Ellipse { Width = 22, Height = 22, Fill = new SolidColorBrush(Tokens.Current.Accents[i]) });
            swatch.Tapped += (_, e) =>
            {
                e.Handled = true;
                SoundService.Play("click");
                Set(x => x.Accent = index);
            };
            panel.Children.Add(swatch);
        }
        return panel;
    }

    /// <summary>Orden y visibilidad de las ventanas del menú (wins): índice, ↑ ↓ e interruptor (Inicio no se oculta).</summary>
    private IEnumerable<Row> OrderRows()
    {
        var wins = SettingsService.Current.Wins;
        for (var i = 0; i < wins.Count; i++)
        {
            if (wins[i] is not JsonArray entry) continue;
            var index = i;
            var id = entry[0]?.GetValue<string>() ?? string.Empty;
            var visible = entry.Count > 2 && entry[2]?.GetValue<bool>() == true;
            var label = Loc.Get($"windows_{id}");
            yield return new Row(label.Length > 0 ? char.ToUpper(label[0]) + label[1..].ToLower(Loc.Culture) : id, string.Empty, () =>
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                var number = Ui.Doto((index + 1).ToString("00"), 14, color: "tertiary");
                number.Width = 22;
                panel.Children.Add(number);
                panel.Children.Add(Circle("↑", () => Move(index, -1)));
                panel.Children.Add(Circle("↓", () => Move(index, 1)));
                if (id != "home")
                {
                    var toggle = Toggle(visible, v => SettingsService.Current.Update(_ => entry[2] = v));
                    toggle.Margin = new Thickness(6, 0, 0, 0);
                    panel.Children.Add(toggle);
                }
                return panel;
            }, visible ? 1 : 0.5);
        }
    }

    private static void Move(int index, int delta)
    {
        var wins = SettingsService.Current.Wins;
        var target = index + delta;
        if (target < 0 || target >= wins.Count) return;
        SettingsService.Current.Update(_ =>
        {
            var a = wins[index]!.DeepClone();
            var b = wins[target]!.DeepClone();
            wins[index] = b;
            wins[target] = a;
        });
    }

    /// <summary>Teclas como chapas + CAMBIAR; al grabar, la siguiente combinación con letra/número/F se guarda.</summary>
    private FrameworkElement HotkeyControl()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var key in _recording ? ["…"] : _s.Keys)
        {
            panel.Children.Add(new Border
            {
                Padding = new Thickness(8, 5, 8, 5),
                CornerRadius = new CornerRadius(5),
                Background = Theme.Brush("glyphOff"),
                BorderBrush = Theme.Brush("border"),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Child = Ui.Mono(key, 10, "primary", 0, medium: true),
            });
        }
        var change = new PillButton
        {
            Text = L(_recording ? "settings_hotkey_rec" : "settings_hotkey_change"),
            Foreground = _recording ? new SolidColorBrush(Tokens.Current.Accent(_s.Accent)) : Theme.Brush("primary"),
        };
        change.Click += (_, _) =>
        {
            _recording = !_recording;
            Render();
        };
        panel.Children.Add(change);
        return panel;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_recording) return;
        var key = e.Key switch
        {
            >= VirtualKey.A and <= VirtualKey.Z => ((char)e.Key).ToString(),
            >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((char)e.Key).ToString(),
            >= VirtualKey.F1 and <= VirtualKey.F24 => $"F{e.Key - VirtualKey.F1 + 1}",
            _ => null,
        };
        if (key is null) return; // solo modificadores todavía
        e.Handled = true;
        bool Down(VirtualKey k) => InputKeyboardSource.GetKeyStateForCurrentThread(k).HasFlag(CoreVirtualKeyStates.Down);
        var keys = new List<string>();
        if (Down(VirtualKey.Control)) keys.Add("CTRL");
        if (Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows)) keys.Add("WIN");
        if (Down(VirtualKey.Menu)) keys.Add("ALT");
        if (Down(VirtualKey.Shift)) keys.Add("SHIFT");
        if (keys.Count == 0) return; // un atajo global necesita al menos un modificador
        keys.Add(key);
        _recording = false;
        SoundService.Play("confirm");
        Set(x => x.Keys = keys);
    }

    // ---- Encabezados de sección ----

    private FrameworkElement MonitorsPanel()
    {
        var panel = new StackPanel { Spacing = 14, Padding = new Thickness(22), Background = Theme.Brush("bg"), CornerRadius = new CornerRadius(14) };
        var monitors = Ui.MonitorPicker(_s, 170, 96, 22, n => Set(x => x.Monitor = n));
        monitors.Height = 110;
        panel.Children.Add(monitors);
        var hint = Ui.Mono(L("settings_monitor_hint"), 10, "tertiary", 60);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(hint);
        return panel;
    }

    /// <summary>Vista previa: fondo de escritorio de puntos y el notch en reposo con mascota o reloj y el acento.</summary>
    private FrameworkElement PreviewPanel()
    {
        var light = Theme.IsLight;
        var wall = new Grid { Height = 120, CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(Tokens.ParseHex(light ? "#f2f2f0" : "#d9d7d2")) };
        wall.Children.Add(new DotPattern { DotColor = Tokens.ParseHex(light ? "#dcdad4" : "#b9b6af"), Spacing = 12, CornerRadius = new CornerRadius(14) });

        var notch = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(18, 0, 18, 0),
            Height = _s.Minimal ? 6 : 36,
            Width = _s.Minimal ? 120 : Math.Round(200 * _s.Size / 100),
            Background = Theme.Brush("bg"),
            CornerRadius = new CornerRadius(0, 0, 16, 16),
        };
        if (!_s.Minimal)
        {
            var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
            if (_s.Mascot)
                inner.Children.Add(new DotMatrix
                {
                    Columns = MascotFrames.Columns, Rows = MascotFrames.Rows, DotSize = 3, Gap = 1.5, VerticalAlignment = VerticalAlignment.Center,
                    OnBrush = Theme.Brush("primary"), OffBrush = Theme.Brush("glyphOff"), Cells = MascotFrames.At(0, _s.Mood, false).Cells,
                });
            if (_s.Clock) inner.Children.Add(Ui.Doto(ClockService.Format(DateTime.Now, _s.H24), 16));
            inner.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Tokens.Current.Accent(_s.Accent)), VerticalAlignment = VerticalAlignment.Center });
            notch.Children.Add(inner);
            notch.Padding = new Thickness(0);
            inner.Width = notch.Width;
        }
        wall.Children.Add(notch);

        var caption = Ui.Mono(L("settings_preview"), 9, "secondary", 100);
        caption.Foreground = new SolidColorBrush(Tokens.ParseHex("#6b675f"));
        caption.HorizontalAlignment = HorizontalAlignment.Left;
        caption.VerticalAlignment = VerticalAlignment.Bottom;
        caption.Margin = new Thickness(14, 0, 0, 10);
        wall.Children.Add(caption);
        return wall;
    }

    // ---- Datos ----

    private async Task ExportAsync()
    {
        var picker = new FileSavePicker { SuggestedFileName = "parpa-ajustes" };
        picker.FileTypeChoices.Add("JSON", [".json"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await File.WriteAllTextAsync(file.Path, SettingsService.Current.Export());
        ShowToast(L("settings_exported"), success: true);
    }

    private async Task ImportAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        var ok = SettingsService.Current.Import(await File.ReadAllTextAsync(file.Path));
        ShowToast(L(ok ? "settings_imported" : "settings_import_failed"), ok);
    }

    private void ShowToast(string message, bool success)
    {
        SoundService.Play(success ? "confirm" : "decline");
        _toast.Text = message;
        _toast.Foreground = new SolidColorBrush(success ? Tokens.Current.Success : Tokens.Current.Accent(_s.Accent));
        _toastTimer.Restart();
    }

    /// <summary>Temporizador de un disparo para el aviso verde de la cabecera.</summary>
    private sealed class DispatcherTimerLite
    {
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;

        public DispatcherTimerLite(TimeSpan interval, Action tick)
        {
            _timer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.IsRepeating = false;
            _timer.Interval = interval;
            _timer.Tick += (_, _) => tick();
        }

        public void Restart()
        {
            _timer.Stop();
            _timer.Start();
        }
    }
}
