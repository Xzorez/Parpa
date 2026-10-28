using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Parpa.Controls;
using Parpa.Services;
using Windows.UI;

namespace Parpa.Views;

public sealed partial class IdleView : UserControl
{
    private const int TickMs = 100;
    private const int Lookahead = 300; // ticks: el ciclo más largo es de 150

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherQueueTimer _timer;
    private ParpaSettings? _s;
    private bool _reduced;
    private bool _animating;

    public IdleView()
    {
        InitializeComponent();
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => Render();
        Mascot.Cells = MascotFrames.Awake();
    }

    public void Apply(ParpaSettings s, bool reducedMotion)
    {
        _s = s;
        _reduced = reducedMotion;
        Mascot.Visibility = s.Mascot ? Visibility.Visible : Visibility.Collapsed;
        Clock.Visibility = s.Mascot ? Visibility.Collapsed : Visibility.Visible;
        Clock.Text = s.Clock ? ClockService.Format(DateTime.Now, s.H24) : string.Empty;
        if (_animating) Render();
    }

    /// <summary>La mascota solo se anima mientras se ve el reposo.</summary>
    public void SetAnimating(bool animating)
    {
        _animating = animating && _s?.Mascot == true;
        if (_animating) Render();
        else _timer.Stop();
    }

    /// <summary>
    /// Dibuja el fotograma actual y programa el temporizador justo para el siguiente cambio visible
    /// (p. ej. 4,5 s hasta el próximo parpadeo), en vez de despertar cada 100 ms.
    /// </summary>
    private void Render()
    {
        if (!_animating || _s is null) return;
        var tick = _clock.ElapsedMilliseconds / TickMs;
        var frame = MascotFrames.At(tick, _s.Mood, _reduced);
        Mascot.Cells = frame.Cells;
        Mascot.OnColor = (Color)Application.Current.Resources[frame.Mode == MascotFrames.Mode.Sleep ? "MutedColor" : "PrimaryColor"];

        var key = frame.Key;
        for (var next = tick + 1; next <= tick + Lookahead; next++)
        {
            if (MascotFrames.At(next, _s.Mood, _reduced).Key == key) continue;
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, next * TickMs - _clock.ElapsedMilliseconds));
            _timer.Start();
            return;
        }
    }
}
