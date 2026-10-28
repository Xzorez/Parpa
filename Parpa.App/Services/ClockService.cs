using Microsoft.UI.Dispatching;

namespace Parpa.Services;

/// <summary>Reloj alineado al cambio de minuto (un tick por minuto, sin sondeo).</summary>
public sealed class ClockService
{
    private readonly DispatcherQueueTimer _timer;

    public event Action? MinuteChanged;

    public ClockService()
    {
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => { MinuteChanged?.Invoke(); Schedule(); };
        Schedule();
    }

    private void Schedule()
    {
        var now = DateTime.Now;
        _timer.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);
        _timer.Start();
    }

    /// <summary>Hora con la cultura activa: 24 h o 12 h según el ajuste <c>h24</c>.</summary>
    public static string Format(DateTime time, bool h24) =>
        time.ToString(h24 ? "HH:mm" : "h:mm", Loc.Culture);

    /// <summary>Hoy: la hora; ayer: "AYER"; antes: día y mes (como las notas del prototipo).</summary>
    public static string FormatRelative(DateTime time, bool h24) =>
        time.Date == DateTime.Today ? Format(time, h24)
        : time.Date == DateTime.Today.AddDays(-1) ? Loc.Get("time_yesterday")
        : Loc.Upper(time.ToString("d MMM", Loc.Culture).TrimEnd('.'));
}
