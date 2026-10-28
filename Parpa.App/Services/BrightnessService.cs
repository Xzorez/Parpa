using System.Management;
using Microsoft.UI.Dispatching;

namespace Parpa.Services;

/// <summary>
/// Cambios de brillo de la pantalla integrada (WMI root\wmi WmiMonitorBrightnessEvent).
/// Los monitores externos no emiten este evento: en un sobremesa simplemente no llega nada.
/// </summary>
public sealed class BrightnessService : IDisposable
{
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private ManagementEventWatcher? _watcher;

    /// <summary>Brillo en %, en el hilo de la UI.</summary>
    public event Action<int>? Changed;

    public void Start()
    {
        try
        {
            _watcher = new ManagementEventWatcher(new ManagementScope(@"root\wmi"),
                new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            _watcher.EventArrived += (_, e) =>
            {
                var level = Convert.ToInt32(e.NewEvent["Brightness"]);
                _dispatcher.TryEnqueue(() => Changed?.Invoke(level));
            };
            _watcher.Start();
        }
        catch (ManagementException ex)
        {
            // Sin pantalla con brillo controlable por WMI (normal en sobremesa).
            Log.Info($"Brillo: no disponible ({ex.ErrorCode})");
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    public void Dispose()
    {
        _watcher?.Stop();
        _watcher?.Dispose();
    }
}
