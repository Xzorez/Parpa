using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Parpa.Controls;
using Parpa.Interop;
using Parpa.Services;
using Windows.UI;

namespace Parpa.Views;

public sealed partial class IdleView : UserControl
{
    private const int TickMs = 100;
    private const int Lookahead = 300; // ticks: el ciclo más largo es de 150

    /// <summary>Sin tocar teclado ni ratón este rato: se duerme.</summary>
    private static readonly TimeSpan SleepAfter = TimeSpan.FromMinutes(2);

    /// <summary>Al volver alguien, bota contenta este rato.</summary>
    private static readonly TimeSpan WakeTime = TimeSpan.FromSeconds(1.6);

    /// <summary>Sigue al ratón mientras se mueve y un poco después.</summary>
    private static readonly TimeSpan FollowTime = TimeSpan.FromSeconds(2.5);

    private enum Override { None, Sleep, Wake, Follow }

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherQueueTimer _timer;
    private readonly DispatcherQueueTimer _watch;
    private ParpaSettings? _s;
    private bool _reduced;
    private bool _animating;

    private Override _override;
    private (int Column, int Row) _look;
    private Win32.POINT _cursor;
    private DateTime _lastMove = DateTime.MinValue;
    private DateTime _wokeAt;

    /// <summary>Rectángulo de la ventana del notch en píxeles de pantalla (para saber hacia dónde mirar).</summary>
    public Func<Win32.RECT>? Bounds { get; set; }

    public IdleView()
    {
        InitializeComponent();
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => Render();
        _watch = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _watch.Tick += (_, _) => Watch();
        Mascot.Cells = MascotFrames.Awake();
    }

    public void Apply(ParpaSettings s, bool reducedMotion)
    {
        _s = s;
        _reduced = reducedMotion;
        Mascot.Visibility = s.Mascot ? Visibility.Visible : Visibility.Collapsed;
        Clock.Visibility = s.Mascot ? Visibility.Collapsed : Visibility.Visible;
        Clock.Text = s.Clock ? ClockService.Format(DateTime.Now, s.H24) : string.Empty;
        Moon.Visibility = s.DndActive ? Visibility.Visible : Visibility.Collapsed;
        UpdateWatch();
        if (_animating) Render();
    }

    /// <summary>La mascota solo se anima mientras se ve el reposo.</summary>
    public void SetAnimating(bool animating)
    {
        _animating = animating && _s?.Mascot == true;
        UpdateWatch();
        if (_animating) Render();
        else _timer.Stop();
    }

    /// <summary>
    /// Vigila el ratón y el tiempo sin tocar el PC (dos llamadas baratas): 5 veces por segundo, o 1 con ahorro de
    /// batería. Con animaciones reducidas no hay vigilancia y la mascota queda quieta.
    /// </summary>
    private void UpdateWatch()
    {
        if (_animating && !_reduced)
        {
            _watch.Interval = TimeSpan.FromMilliseconds(PowerService.SaverActive ? 1000 : 200);
            _cursor = Cursor();
            _watch.Start();
        }
        else
        {
            _watch.Stop();
            _override = Override.None;
        }
    }

    private static Win32.POINT Cursor()
    {
        Win32.GetCursorPos(out var point);
        return point;
    }

    private void Watch()
    {
        var now = DateTime.UtcNow;
        var cursor = Cursor();
        if (Math.Abs(cursor.X - _cursor.X) + Math.Abs(cursor.Y - _cursor.Y) > 3) _lastMove = now;
        _cursor = cursor;

        var next = Override.None;
        if (Win32.IdleTime() >= SleepAfter) next = Override.Sleep;
        else if (_override == Override.Sleep || _override == Override.Wake && now - _wokeAt < WakeTime)
        {
            if (_override == Override.Sleep) _wokeAt = now;
            next = Override.Wake;
        }
        else if (now - _lastMove < FollowTime) next = Override.Follow;

        var look = next == Override.Follow ? LookAt(cursor) : _look;
        if (next == _override && look == _look) return;
        _override = next;
        _look = look;
        Render();
    }

    /// <summary>Columna −2…2 según lo lejos que esté el ratón a los lados; una fila abajo si está bajo el notch.</summary>
    private (int Column, int Row) LookAt(Win32.POINT cursor)
    {
        if (Bounds?.Invoke() is not { } rect) return (0, 0);
        var center = (rect.Left + rect.Right) / 2.0;
        var dx = cursor.X - center;
        var column = Math.Abs(dx) switch { < 70 => 0, < 450 => 1, _ => 2 } * Math.Sign(dx);
        var row = cursor.Y - rect.Top > 140 ? 1 : 0;
        return (column, row);
    }

    private MascotFrames.Frame FrameAt(long tick) => _override switch
    {
        Override.Sleep => new MascotFrames.Frame(MascotFrames.Sleeping(tick), MascotFrames.Mode.Sleep),
        Override.Wake => new MascotFrames.Frame(MascotFrames.Waking(tick), MascotFrames.Mode.Happy),
        Override.Follow => new MascotFrames.Frame(MascotFrames.Looking(_look.Column, _look.Row, tick % 45 < 2), MascotFrames.Mode.Awake),
        _ => MascotFrames.At(tick, _s!.Mood, _reduced),
    };

    /// <summary>
    /// Dibuja el fotograma actual y programa el temporizador justo para el siguiente cambio visible
    /// (p. ej. 4,5 s hasta el próximo parpadeo), en vez de despertar cada 100 ms.
    /// </summary>
    private void Render()
    {
        if (!_animating || _s is null) return;
        var tick = _clock.ElapsedMilliseconds / TickMs;
        var frame = FrameAt(tick);
        Mascot.Cells = frame.Cells;
        Mascot.OnColor = (Color)Application.Current.Resources[frame.Mode == MascotFrames.Mode.Sleep ? "MutedColor" : "PrimaryColor"];

        var key = frame.Key;
        for (var next = tick + 1; next <= tick + Lookahead; next++)
        {
            if (FrameAt(next).Key == key) continue;
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, next * TickMs - _clock.ElapsedMilliseconds));
            _timer.Start();
            return;
        }
        _timer.Stop();
    }
}
