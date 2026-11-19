using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;

namespace Parpa.Views;

/// <summary>
/// Ventana Temporizador (estilo Akira). Izquierda: estado diminuto, la cuenta en letra ancha enorme (la rueda sobre
/// los minutos suma o resta 1 min y sobre los segundos 5 s), una línea con lo que ya ha pasado y tiempos rápidos.
/// Derecha: EMPEZAR / PAUSAR / SEGUIR o PARAR (rellenos) y, según el estado, ajustes finos (±1 h, ±1 min, ±10 s),
/// +1 MIN y REINICIAR.
/// </summary>
public sealed partial class TimerView : Grid, INotchPageView
{
    private static readonly int[] Presets = [1, 5, 10, 15, 25, 30, 45, 60];

    private readonly NotchServices _app;
    private readonly TimerService _timer;
    private readonly TextBlock _state = new();
    private readonly TextBlock _minutes = new();
    private readonly TextBlock _colon = new() { Text = ":" };
    private readonly TextBlock _seconds = new();
    private readonly Border _minutesHit = new() { Background = Ui.Clear };
    private readonly Border _secondsHit = new() { Background = Ui.Clear };
    private readonly Grid _progress = new() { Height = 9 };
    private readonly Border _progressFill = new() { Height = 3, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _presets = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly StackPanel _actions = new() { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _left;
    private TimerState? _builtFor;
    private string _lastMinutes = string.Empty;
    private string _lastSeconds = string.Empty;
    private bool _shown;

    public TimerView(NotchServices app)
    {
        _app = app;
        _timer = app.Timer;
        ColumnSpacing = 10;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) });

        // ---- Izquierda: estado, cuenta, línea y tiempos rápidos ----
        _state.Style = Res<Style>("Tiny");
        _state.HorizontalAlignment = HorizontalAlignment.Left;
        foreach (var part in new[] { _minutes, _colon, _seconds })
        {
            part.Style = Res<Style>("Wide");
            part.FontSize = 50;
            part.LineHeight = 52;
            part.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        }
        _minutesHit.Child = _minutes;
        _secondsHit.Child = _seconds;
        _minutesHit.PointerWheelChanged += (_, e) => Wheel(e, TimeSpan.FromMinutes(1));
        _secondsHit.PointerWheelChanged += (_, e) => Wheel(e, TimeSpan.FromSeconds(5));
        var digits = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        digits.Children.Add(_minutesHit);
        digits.Children.Add(_colon);
        digits.Children.Add(_secondsHit);

        _progress.Children.Add(new Border { Height = 1.5, Background = Res<Brush>("BorderBrush"), VerticalAlignment = VerticalAlignment.Center });
        _progressFill.Background = Res<Brush>("PrimaryBrush");
        _progress.Children.Add(_progressFill);
        _progress.SizeChanged += (_, _) => DrawProgress();

        var minLabel = new TextBlock { Style = Res<Style>("Tiny"), Text = "MIN", Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center };
        _presets.Children.Add(minLabel);
        foreach (var minutes in Presets)
        {
            var chip = new PillButton { Text = $"{minutes}", Padding = new Thickness(7, 4, 7, 4) };
            chip.Click += (_, _) => _timer.Set(TimeSpan.FromMinutes(minutes));
            _presets.Children.Add(chip);
        }

        var left = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(_state);
        left.Children.Add(digits);
        left.Children.Add(_progress);
        left.Children.Add(_presets);
        _left = new Border
        {
            Padding = new Thickness(16, 10, 16, 12),
            BorderBrush = Res<Brush>("PrimaryBrush"),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(12),
            Child = left,
        };
        Children.Add(_left);

        // ---- Derecha: botones ----
        Grid.SetColumn(_actions, 1);
        Children.Add(_actions);

        _timer.Changed += () => { if (_shown) Render(); };
        Loc.LanguageChanged += () => { _builtFor = null; if (_shown) Render(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    private static string Up(string key) => Loc.Get(key).ToUpper(Loc.Culture);

    public IEnumerable<UIElement> EntranceItems => [_left, _actions];

    public void OnShown()
    {
        _shown = true;
        Render();
    }

    public void OnHidden()
    {
        _shown = false;
        _builtFor = null; // al volver se rehacen los botones y, si suena, vuelve el latido
        Pulse(false);
    }

    private void Wheel(PointerRoutedEventArgs e, TimeSpan step)
    {
        e.Handled = true;
        if (_timer.State != TimerState.Ready) return;
        var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        SoundService.Play("tick");
        Nudge(delta > 0 ? step : -step);
    }

    /// <summary>Ajuste fino: parado cambia el tiempo elegido; en pausa, lo que falta (sin bajar de 1 s).</summary>
    private void Nudge(TimeSpan delta)
    {
        if (_timer.State == TimerState.Paused)
        {
            if (delta > TimeSpan.Zero && delta == TimeSpan.FromMinutes(1)) _timer.AddMinute();
            return;
        }
        var next = _timer.Duration + delta;
        _timer.Set(next < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : next);
    }

    private void Render()
    {
        var state = _timer.State;
        var time = TimerService.Format(_timer.Remaining);
        var cut = time.LastIndexOf(':');
        _minutes.Text = time[..cut];
        _seconds.Text = time[(cut + 1)..];
        // En marcha, cada cifra que cambia cae desde arriba con un fundido corto (como un marcador).
        if (state == TimerState.Running)
        {
            if (_seconds.Text != _lastSeconds) Motion.FadeUp(_seconds, -7, TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
            if (_minutes.Text != _lastMinutes) Motion.FadeUp(_minutes, -7, TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
        }
        _lastSeconds = _seconds.Text;
        _lastMinutes = _minutes.Text;
        _state.Text = Up(state switch
        {
            TimerState.Running => "timer_running",
            TimerState.Paused => "timer_paused",
            TimerState.Ringing => "timer_ringing",
            _ => "timer_ready",
        });
        // Sonando: el marco se rellena de tinta y los números pasan a negro, como el bloque destacado del cartel.
        var ringing = state == TimerState.Ringing;
        _left.Background = ringing ? Res<Brush>("PrimaryBrush") : null;
        var ink = ringing ? Res<Brush>("BgBrush") : Res<Brush>("PrimaryBrush");
        foreach (var t in new[] { _minutes, _colon, _seconds, _state }) t.Foreground = ink;
        _progressFill.Background = ink;
        _presets.Opacity = state == TimerState.Ready ? 1 : 0.35;
        _presets.IsHitTestVisible = state == TimerState.Ready;
        DrawProgress();
        if (_builtFor != state) BuildActions(state);
    }

    private void DrawProgress() =>
        _progressFill.Width = Math.Max(0, _progress.ActualWidth * (_timer.State == TimerState.Ready ? 0 : _timer.Elapsed));

    /// <summary>Sonando: el bloque late al ritmo de la alarma (1,4 s); al pararla vuelve a su tamaño.</summary>
    private void Pulse(bool on)
    {
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_left);
        visual.StopAnimation("Scale");
        visual.Scale = System.Numerics.Vector3.One;
        if (!on || Motion.Reduced) return;
        var c = visual.Compositor;
        visual.CenterPoint = new System.Numerics.Vector3((float)_left.ActualWidth / 2, (float)_left.ActualHeight / 2, 0);
        var ease = c.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(.4f, 0f), new System.Numerics.Vector2(.2f, 1f));
        var beat = c.CreateVector3KeyFrameAnimation();
        beat.InsertKeyFrame(0f, System.Numerics.Vector3.One);
        beat.InsertKeyFrame(0.12f, new System.Numerics.Vector3(1.025f, 1.025f, 1), ease);
        beat.InsertKeyFrame(0.45f, System.Numerics.Vector3.One, ease);
        beat.InsertKeyFrame(1f, System.Numerics.Vector3.One);
        beat.Duration = TimeSpan.FromMilliseconds(1400);
        beat.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
        visual.StartAnimation("Scale", beat);
    }

    private void BuildActions(TimerState state)
    {
        _builtFor = state;
        Pulse(state == TimerState.Ringing);
        _actions.Children.Clear();
        switch (state)
        {
            case TimerState.Ready:
                _actions.Children.Add(Big("timer_start", _timer.Start));
                _actions.Children.Add(Pair(Small("−1 H", () => Nudge(TimeSpan.FromHours(-1))), Small("+1 H", () => Nudge(TimeSpan.FromHours(1)))));
                _actions.Children.Add(Pair(Small("−1 MIN", () => Nudge(TimeSpan.FromMinutes(-1))), Small("+1 MIN", () => Nudge(TimeSpan.FromMinutes(1)))));
                _actions.Children.Add(Pair(Small("−10 S", () => Nudge(TimeSpan.FromSeconds(-10))), Small("+10 S", () => Nudge(TimeSpan.FromSeconds(10)))));
                break;
            case TimerState.Running:
                _actions.Children.Add(Big("timer_pause", _timer.Pause));
                _actions.Children.Add(Wide(Small(Up("timer_add"), _timer.AddMinute)));
                _actions.Children.Add(Wide(Small(Up("timer_reset"), _timer.Reset)));
                break;
            case TimerState.Paused:
                _actions.Children.Add(Big("timer_resume", _timer.Start));
                _actions.Children.Add(Wide(Small(Up("timer_add"), _timer.AddMinute)));
                _actions.Children.Add(Wide(Small(Up("timer_reset"), _timer.Reset)));
                break;
            case TimerState.Ringing:
                _actions.Children.Add(Big("timer_stop", _timer.Stop, tall: true));
                _actions.Children.Add(Wide(Small(Up("timer_add"), _timer.AddMinute)));
                break;
        }
    }

    /// <summary>Botón principal relleno, a todo el ancho.</summary>
    private static PillButton Big(string key, Action action, bool tall = false)
    {
        var button = new PillButton
        {
            Text = Loc.Get(key),
            Primary = true,
            Padding = new Thickness(14, tall ? 22 : 12, 14, tall ? 22 : 12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ((TextBlock)button.Children[0]).HorizontalAlignment = HorizontalAlignment.Center;
        ((TextBlock)button.Children[0]).FontSize = 11;
        button.Click += (_, _) => action();
        return button;
    }

    private static PillButton Small(string text, Action action)
    {
        var button = new PillButton { Text = text, Padding = new Thickness(8, 6, 8, 6), HorizontalAlignment = HorizontalAlignment.Stretch };
        ((TextBlock)button.Children[0]).HorizontalAlignment = HorizontalAlignment.Center;
        button.Click += (_, _) => action();
        return button;
    }

    private static PillButton Wide(PillButton button)
    {
        button.Padding = new Thickness(8, 9, 8, 9);
        return button;
    }

    private static Grid Pair(FrameworkElement a, FrameworkElement b)
    {
        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(b, 1);
        grid.Children.Add(a);
        grid.Children.Add(b);
        return grid;
    }
}
