using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;
using Windows.System;
using Windows.UI.Core;
using WinRT.Interop;

namespace Parpa.AppWindows;

/// <summary>
/// Bienvenida de primer uso (design/Bienvenida y Bandeja.dc.html #9a), 620×460, fondo #111113:
/// hola → monitor y posición → atajo → apps que pueden avisar → listo. Guarda en los mismos ajustes.
/// </summary>
public sealed class WelcomeWindow : Window
{
    private const double DesignWidth = 620;
    private const double DesignHeight = 460;
    private const int Steps = 5;

    private readonly ParpaSettings _s = SettingsService.Current.S;
    private readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly Grid _stepHost = new();
    private readonly TextBlock _back = Ui.Mono(string.Empty, 10, tracking: 80, medium: true);
    private readonly PillButton _next = new() { Primary = true, Padding = new Thickness(18, 10, 18, 10) };
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _faceTimer;
    private DotMatrix? _face;
    private int _step;
    private int _slide; // 1 avanzar, -1 volver, 0 sin animación
    private int _tick;
    private bool _recording;

    public WelcomeWindow()
    {
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(true, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icon", "app.ico"));
        var scale = GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(DesignWidth * scale), (int)(DesignHeight * scale)));
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Move(new Windows.Graphics.PointInt32(area.X + (area.Width - AppWindow.Size.Width) / 2, area.Y + (area.Height - AppWindow.Size.Height) / 2));

        // Cara de la mascota: animada solo mientras la ventana está abierta.
        _faceTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        _faceTimer.Interval = TimeSpan.FromMilliseconds(100);
        _faceTimer.Tick += (_, _) => { _tick++; DrawFace(); };

        Content = BuildShell();
        Motion.WindowIn((FrameworkElement)Content);
        ((FrameworkElement)Content).KeyDown += OnKeyDown;
        Loc.LanguageChanged += Render;
        Theme.Changed += Render;
        Closed += (_, _) =>
        {
            _faceTimer.Stop();
            Loc.LanguageChanged -= Render;
            Theme.Changed -= Render;
        };
        Render();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private Grid BuildShell()
    {
        var root = new Grid { Background = Theme.Brush("windowBg") };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Contenido: padding 32 36 0, gap 20.
        var top = new Grid { Padding = new Thickness(36, 32, 36, 0), RowSpacing = 20 };
        top.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        top.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        top.Children.Add(_dots);
        Grid.SetRow(_stepHost, 1);
        top.Children.Add(_stepHost);
        root.Children.Add(top);

        // Pie: ‹ ATRÁS | botón principal. Padding 20 36 26.
        var footer = new Grid { Padding = new Thickness(36, 20, 36, 26) };
        _back.Tapped += (_, e) => { e.Handled = true; Back(); };
        footer.Children.Add(_back);
        _next.HorizontalAlignment = HorizontalAlignment.Right;
        _next.Click += (_, _) => Next();
        footer.Children.Add(_next);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        return root;
    }

    private void Render()
    {
        Title = Loc.Get("app_name");
        ((Grid)Content).Background = Theme.Brush("windowBg");

        // Indicador de pasos: el actual 28 px, el resto 8 px; hechos en claro. El ancho y el color cambian con transición.
        if (_dots.Children.Count == 0)
            for (var i = 0; i < Steps; i++)
                _dots.Children.Add(Motion.SmoothBackground(new Border { Height = 4, Width = 8, CornerRadius = new CornerRadius(2) }));
        for (var i = 0; i < Steps; i++)
        {
            var dot = (Border)_dots.Children[i];
            dot.Background = Theme.Brush(i <= _step ? "primary" : "border");
            Motion.AnimateSize(dot, i == _step ? 28 : 8, null, TimeSpan.FromMilliseconds(300));
        }

        _stepHost.Children.Clear();
        _face = null;
        FrameworkElement step = _step switch
        {
            0 => Hello(),
            1 => Position(),
            2 => Shortcut(),
            3 => Apps(),
            _ => Done(),
        };
        _stepHost.Children.Add(step);
        // El paso nuevo llega deslizándose: desde la derecha al avanzar, desde la izquierda al volver.
        if (_slide != 0) Motion.FadeUp(step, 0, TimeSpan.Zero, TimeSpan.FromMilliseconds(300), fromX: 28 * _slide);
        _slide = 0;

        // Paso final: "VER DE NUEVO" a la izquierda y el botón cierra.
        _back.Text = _step == Steps - 1 ? Loc.Get("welcome_again") : "‹ " + Loc.Get("welcome_back");
        _back.Opacity = _step == 0 ? 0 : 1;
        _next.Text = _step switch
        {
            0 => Loc.Get("welcome_finish"),
            3 => Loc.Get("welcome_end"),
            Steps - 1 => Loc.Get("settings_close"),
            _ => Loc.Get("welcome_next"),
        };
        if (_face is not null) _faceTimer.Start();
        else _faceTimer.Stop();
    }

    private void Next()
    {
        if (_step == Steps - 1)
        {
            Close();
            return;
        }
        _recording = false;
        _step++;
        _slide = 1;
        if (_step == Steps - 1) SettingsService.Current.Update(s => s.WelcomeDone = true);
        SoundService.Play(_step == Steps - 1 ? "confirm" : "page");
        Render();
    }

    private void Back()
    {
        if (_step == 0) return;
        _recording = false;
        _step = _step == Steps - 1 ? 0 : _step - 1;
        _slide = -1;
        SoundService.Play("page");
        Render();
    }

    // ---- Pasos ----

    /// <summary>Notch 200×96 (radio 0 0 40 40) con la cara 13×5 en puntos de 6 px.</summary>
    private Border BigNotch()
    {
        _face = new DotMatrix
        {
            Columns = MascotFrames.Columns, Rows = MascotFrames.Rows, DotSize = 6, Gap = 3,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            OnBrush = Theme.Brush("primary"), OffBrush = Theme.Brush("glyphOff"),
        };
        DrawFace();
        return new Border
        {
            Width = 200,
            Height = 96,
            CornerRadius = new CornerRadius(0, 0, 40, 40),
            Background = Theme.Brush("bg"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _face,
        };
    }

    /// <summary>Contenta: los ojos "^" botan cada 0,4 s.</summary>
    private void DrawFace()
    {
        if (_face is null) return;
        _face.Cells = MascotFrames.HappyAt(up: _tick % 8 >= 4);
    }

    private StackPanel Hello()
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(BigNotch());
        var texts = new StackPanel { Spacing = 8, MaxWidth = 420 };
        var hello = Ui.Doto(Loc.Get("welcome_hello"), 40, black: true);
        hello.HorizontalAlignment = HorizontalAlignment.Center;
        hello.TextAlignment = TextAlignment.Center;
        texts.Children.Add(hello);
        var intro = Ui.Text(Loc.Get("welcome_intro"), 15, "soft");
        intro.TextAlignment = TextAlignment.Center;
        intro.LineHeight = 22.5;
        texts.Children.Add(intro);
        panel.Children.Add(texts);
        return panel;
    }

    private static StackPanel StepTitle(string titleKey, string subKey)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(Ui.Text(Loc.Get(titleKey), 22, semibold: true));
        panel.Children.Add(Ui.Text(Loc.Get(subKey), 13, "secondary"));
        return panel;
    }

    private StackPanel Position()
    {
        var panel = new StackPanel { Spacing = 20 };
        panel.Children.Add(StepTitle("welcome_s1_title", "welcome_s1_sub"));
        var monitors = new Border
        {
            Height = 140,
            Padding = new Thickness(22),
            Background = Theme.Brush("bg"),
            CornerRadius = new CornerRadius(14),
            Child = Ui.MonitorPicker(_s, 150, 84, 20, n => Change(s => s.Monitor = n)),
        };
        panel.Children.Add(monitors);
        var segments = Ui.Segmented(
            [("left", Loc.Get("welcome_left")), ("center", Loc.Get("welcome_center")), ("right", Loc.Get("welcome_right"))],
            _s.Pos, v => Change(s => s.Pos = v), padX: 14, padY: 7);
        segments.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(segments);
        return panel;
    }

    /// <summary>Teclas grandes (Doto 700 26, padding 14 20, radio 12) y CAMBIAR ATAJO.</summary>
    private Grid Shortcut()
    {
        var grid = new Grid { RowSpacing = 20 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(StepTitle("welcome_s2_title", "welcome_s2_sub"));

        var center = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var key in _recording ? ["…"] : _s.Keys)
        {
            keys.Children.Add(new Border
            {
                Padding = new Thickness(20, 14, 20, 14),
                CornerRadius = new CornerRadius(12),
                Background = Theme.Brush("glyphOff"),
                BorderBrush = Theme.Brush("border"),
                BorderThickness = new Thickness(0, 0, 0, 4),
                Child = Ui.Doto(key, 26),
            });
        }
        center.Children.Add(keys);
        var change = new PillButton
        {
            Text = Loc.Get(_recording ? "settings_hotkey_rec" : "welcome_change_keys"),
            Foreground = _recording ? new SolidColorBrush(Tokens.Current.Accent(_s.Accent)) : Theme.Brush("primary"),
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        change.Click += (_, _) =>
        {
            _recording = !_recording;
            Render();
        };
        center.Children.Add(change);
        Grid.SetRow(center, 1);
        grid.Children.Add(center);
        return grid;
    }

    /// <summary>Apps en dos columnas (gap 8): encendida en claro, apagada en tarjeta.</summary>
    private StackPanel Apps()
    {
        var panel = new StackPanel { Spacing = 20 };
        panel.Children.Add(StepTitle("welcome_s3_title", "welcome_s3_sub"));
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var apps = _s.Apps.Keys.ToList();
        for (var i = 0; i < apps.Count; i++)
        {
            if (i % 2 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var app = apps[i];
            var on = _s.Apps[app];
            var tile = new Grid
            {
                Padding = new Thickness(16, 14, 16, 14),
                CornerRadius = new CornerRadius(12),
                Background = Theme.Brush(on ? "primary" : "card"),
            };
            var name = Ui.Text(app, 14, on ? "bg" : "primary");
            name.FontWeight = Microsoft.UI.Text.FontWeights.Medium;
            tile.Children.Add(name);
            var state = Ui.Mono(Loc.Get(on ? "settings_on" : "settings_off"), 9, on ? "bg" : "primary");
            state.Opacity = 0.7;
            state.HorizontalAlignment = HorizontalAlignment.Right;
            tile.Children.Add(state);
            tile.Tapped += (_, e) =>
            {
                e.Handled = true;
                SoundService.Play("click");
                Change(s => s.Apps[app] = !on);
            };
            Grid.SetRow(tile, i / 2);
            Grid.SetColumn(tile, i % 2);
            grid.Children.Add(tile);
        }
        panel.Children.Add(grid);
        return panel;
    }

    private StackPanel Done()
    {
        var panel = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(BigNotch());
        var title = Ui.Doto(Loc.Get("welcome_done_title"), 36, black: true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(title);
        var text = Ui.Text(Loc.Get("welcome_done_text"), 14, "soft");
        text.TextAlignment = TextAlignment.Center;
        text.MaxWidth = 400;
        text.LineHeight = 21;
        panel.Children.Add(text);
        return panel;
    }

    private void Change(Action<ParpaSettings> change)
    {
        SettingsService.Current.Update(change);
        Render();
    }

    /// <summary>Graba el atajo: modificador(es) + letra, número o F.</summary>
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
        if (key is null) return;
        e.Handled = true;
        bool Down(VirtualKey k) => InputKeyboardSource.GetKeyStateForCurrentThread(k).HasFlag(CoreVirtualKeyStates.Down);
        var keys = new List<string>();
        if (Down(VirtualKey.Control)) keys.Add("CTRL");
        if (Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows)) keys.Add("WIN");
        if (Down(VirtualKey.Menu)) keys.Add("ALT");
        if (Down(VirtualKey.Shift)) keys.Add("SHIFT");
        if (keys.Count == 0) return;
        keys.Add(key);
        _recording = false;
        SoundService.Play("confirm");
        Change(s => s.Keys = keys);
    }
}
