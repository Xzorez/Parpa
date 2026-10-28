using Microsoft.UI.Dispatching;

namespace Parpa.Services;

/// <summary>
/// Sesión de foco (pomodoro) de <c>pomo</c> minutos. El temporizador de 1 s solo corre mientras
/// la sesión está en marcha.
/// </summary>
public sealed class FocusService
{
    public enum FocusState { Idle, Running, Paused }

    private readonly ParpaSettings _s;
    private readonly DispatcherQueueTimer _timer;
    private DateTime _endsAt;

    public FocusState State { get; private set; } = FocusState.Idle;

    public TimeSpan Remaining { get; private set; }

    public TimeSpan Length => TimeSpan.FromMinutes(Math.Max(1, _s.Pomo));

    /// <summary>Cambio de estado o de segundo.</summary>
    public event Action? Changed;

    public FocusService(ParpaSettings settings)
    {
        _s = settings;
        Remaining = Length;
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Empieza, pausa o reanuda (la tarjeta FOCO es un solo botón).</summary>
    public void Toggle()
    {
        switch (State)
        {
            case FocusState.Running:
                Remaining = _endsAt - DateTime.UtcNow;
                State = FocusState.Paused;
                _timer.Stop();
                break;
            default:
                if (State == FocusState.Idle) Remaining = Length;
                _endsAt = DateTime.UtcNow + Remaining;
                State = FocusState.Running;
                _timer.Start();
                break;
        }
        Changed?.Invoke();
    }

    private void Tick()
    {
        Remaining = _endsAt - DateTime.UtcNow;
        if (Remaining <= TimeSpan.Zero)
        {
            _timer.Stop();
            State = FocusState.Idle;
            Remaining = Length;
        }
        Changed?.Invoke();
    }

    public static string Format(TimeSpan time)
    {
        var seconds = (int)Math.Ceiling(Math.Max(0, time.TotalSeconds));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
