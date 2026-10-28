using Microsoft.UI.Dispatching;
using Windows.System.Power;

namespace Parpa.Services;

/// <summary>Batería (Windows.System.Power.PowerManager): cargador conectado y batería baja.</summary>
public sealed class PowerService
{
    /// <summary>Umbral de batería baja; el aviso salta una vez al cruzarlo y se rearma al recargar por encima.</summary>
    public const int LowBatteryPercent = 20;

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private bool _pluggedIn;
    private bool _lowNotified;

    public event Action<int>? ChargerConnected;

    public event Action<int>? BatteryLow;

    public static bool HasBattery => PowerManager.BatteryStatus != BatteryStatus.NotPresent;

    public static int Percent => PowerManager.RemainingChargePercent;

    public void Start()
    {
        if (!HasBattery) return;
        _pluggedIn = IsPluggedIn;
        _lowNotified = Percent <= LowBatteryPercent;
        PowerManager.PowerSupplyStatusChanged += (_, _) => _dispatcher.TryEnqueue(Check);
        PowerManager.RemainingChargePercentChanged += (_, _) => _dispatcher.TryEnqueue(Check);
    }

    private static bool IsPluggedIn => PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent;

    private void Check()
    {
        var plugged = IsPluggedIn;
        var percent = Percent;
        if (plugged && !_pluggedIn) ChargerConnected?.Invoke(percent);
        _pluggedIn = plugged;

        if (percent > LowBatteryPercent) _lowNotified = false;
        else if (!plugged && !_lowNotified)
        {
            _lowNotified = true;
            BatteryLow?.Invoke(percent);
        }
    }
}
