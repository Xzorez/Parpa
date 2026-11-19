using Microsoft.UI.Dispatching;

namespace Parpa.Services;

/// <summary>Estado del temporizador.</summary>
public enum TimerState { Ready, Running, Paused, Ringing }

/// <summary>
/// Temporizador del notch: cuenta atrás desde el tiempo elegido (1 s – 24 h). Se puede pausar, seguir, sumar un
/// minuto o reiniciar. Al llegar a cero suena una alarma que se repite hasta que se pulsa PARAR (o +1 MIN); suena
/// aunque los sonidos de la interfaz estén apagados o haya No molestar, porque es una alarma que se ha pedido.
/// Para no sonar eternamente si no hay nadie, se calla sola a los 10 minutos (el aviso sigue ahí).
/// El tiempo elegido se recuerda para la próxima vez (ajuste <c>timer</c>).
/// </summary>
public sealed class TimerService
{
    public static readonly TimeSpan Max = TimeSpan.FromHours(24);
    private static readonly TimeSpan AlarmLimit = TimeSpan.FromMinutes(10);

    private readonly ParpaSettings _s;
    private readonly DispatcherQueueTimer _tick;
    private DateTime _endsAt;
    private DateTime _rangAt;

    public TimerState State { get; private set; } = TimerState.Ready;

    /// <summary>Tiempo elegido (lo que vuelve a salir al reiniciar).</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>Tiempo que falta; con la alarma sonando, cero.</summary>
    public TimeSpan Remaining => State switch
    {
        TimerState.Running => Clamp(_endsAt - DateTime.UtcNow),
        TimerState.Ringing => TimeSpan.Zero,
        _ => _remaining,
    };

    private TimeSpan _remaining;

    /// <summary>Fracción ya pasada (0 al empezar, 1 al acabar).</summary>
    public double Elapsed => Duration <= TimeSpan.Zero ? 0 : Math.Clamp(1 - Remaining / Duration, 0, 1);

    /// <summary>Cambió el estado o pasó un segundo (en el hilo de la interfaz).</summary>
    public event Action? Changed;

    /// <summary>Llegó a cero.</summary>
    public event Action? Finished;

    public TimerService(ParpaSettings settings)
    {
        _s = settings;
        Duration = _remaining = Clamp(TimeSpan.FromSeconds(settings.Timer));
        if (Duration <= TimeSpan.Zero) Duration = _remaining = TimeSpan.FromMinutes(5);
        _tick = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _tick.Interval = TimeSpan.FromMilliseconds(250);
        _tick.Tick += (_, _) => OnTick();
    }

    private static TimeSpan Clamp(TimeSpan time) =>
        time < TimeSpan.Zero ? TimeSpan.Zero : time > Max ? Max : time;

    /// <summary>Elige el tiempo (solo sin cuenta en marcha); se guarda para la próxima vez.</summary>
    public void Set(TimeSpan duration)
    {
        if (State is TimerState.Running or TimerState.Ringing) return;
        duration = Clamp(duration);
        if (duration < TimeSpan.FromSeconds(1)) duration = TimeSpan.FromSeconds(1);
        Duration = _remaining = duration;
        State = TimerState.Ready;
        SettingsService.Current.Update(s => s.Timer = (int)duration.TotalSeconds);
        Changed?.Invoke();
    }

    /// <summary>EMPEZAR / SEGUIR.</summary>
    public void Start()
    {
        if (State == TimerState.Running) return;
        if (State == TimerState.Ringing) StopAlarm();
        if (_remaining <= TimeSpan.Zero) _remaining = Duration;
        _endsAt = DateTime.UtcNow + _remaining;
        State = TimerState.Running;
        _tick.Start();
        Changed?.Invoke();
    }

    public void Pause()
    {
        if (State != TimerState.Running) return;
        _remaining = Clamp(_endsAt - DateTime.UtcNow);
        State = TimerState.Paused;
        _tick.Stop();
        Changed?.Invoke();
    }

    /// <summary>REINICIAR: vuelve al tiempo elegido, parado (y calla la alarma).</summary>
    public void Reset()
    {
        StopAlarm();
        _tick.Stop();
        _remaining = Duration;
        State = TimerState.Ready;
        Changed?.Invoke();
    }

    /// <summary>PARAR la alarma: queda listo con el mismo tiempo para repetirlo.</summary>
    public void Stop() => Reset();

    /// <summary>+1 MIN: en marcha o en pausa suma un minuto; sonando, calla la alarma y cuenta un minuto más.</summary>
    public void AddMinute()
    {
        var minute = TimeSpan.FromMinutes(1);
        switch (State)
        {
            case TimerState.Running:
                _endsAt = DateTime.UtcNow + Clamp(_endsAt - DateTime.UtcNow + minute);
                break;
            case TimerState.Ringing:
                StopAlarm();
                _remaining = minute;
                State = TimerState.Paused;
                Start();
                return;
            default:
                _remaining = Clamp(_remaining + minute);
                if (State == TimerState.Ready) Duration = _remaining;
                break;
        }
        Changed?.Invoke();
    }

    private void OnTick()
    {
        if (State == TimerState.Ringing)
        {
            if (DateTime.UtcNow - _rangAt > AlarmLimit) SoundService.SetAlarm(false);
            return;
        }
        if (State != TimerState.Running) return;
        if (_endsAt > DateTime.UtcNow)
        {
            Changed?.Invoke();
            return;
        }
        _remaining = TimeSpan.Zero;
        State = TimerState.Ringing;
        _rangAt = DateTime.UtcNow;
        Log.Info($"Temporizador: fin ({Format(Duration)})");
        SoundService.SetAlarm(true);
        Changed?.Invoke();
        Finished?.Invoke();
    }

    private void StopAlarm()
    {
        SoundService.SetAlarm(false);
        if (State == TimerState.Ringing) State = TimerState.Ready;
    }

    /// <summary>"4:05", "12:00", "1:02:30" (redondeando hacia arriba: 4:59,2 se ve 5:00 hasta que baja).</summary>
    public static string Format(TimeSpan time)
    {
        var total = (int)Math.Ceiling(Math.Max(0, time.TotalSeconds) - 0.001);
        var h = total / 3600;
        var m = total / 60 % 60;
        var s = total % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }
}
