using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using Parpa.Interop;

namespace Parpa.Services;

/// <summary>Micro y cámara en uso ahora mismo y la app que los usa (vacía si no se sabe).</summary>
public readonly record struct PrivacyState(bool Mic, bool Camera, string App)
{
    public bool Active => Mic || Camera;
}

/// <summary>
/// Micro / cámara en uso (README › APIs): en
/// <c>HKCU\…\CapabilityAccessManager\ConsentStore\{microphone,webcam}</c> cada app tiene
/// <c>LastUsedTimeStart</c> / <c>LastUsedTimeStop</c>; con <c>Stop == 0</c> lo está usando.
/// Sin sondeo: <c>RegNotifyChangeKeyValue</c> avisa de cada cambio y entonces se vuelve a leer.
/// </summary>
public sealed class PrivacyIndicatorService : IDisposable
{
    private const string Root = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _debounce;
    private readonly List<Watch> _watches = new();
    private bool _disposed;

    public PrivacyState State { get; private set; }

    public event Action<PrivacyState>? Changed;

    public PrivacyIndicatorService()
    {
        _debounce = _dispatcher.CreateTimer();
        _debounce.IsRepeating = false;
        _debounce.Interval = Debounce;
        _debounce.Tick += (_, _) => Scan();
    }

    public void Start()
    {
        foreach (var capability in new[] { "microphone", "webcam" })
        {
            var key = Registry.CurrentUser.OpenSubKey($@"{Root}\{capability}");
            if (key is null) continue; // equipo sin ese dispositivo o sin historial de permisos
            _watches.Add(new Watch(key, () => _dispatcher.TryEnqueue(() => { if (!_disposed) _debounce.Start(); })));
        }
        Scan();
    }

    private void Scan()
    {
        var (mic, micApp) = InUse("microphone");
        var (camera, cameraApp) = InUse("webcam");
        var next = new PrivacyState(mic, camera, cameraApp.Length > 0 ? cameraApp : micApp);
        if (next == State) return;
        State = next;
        Log.Info($"Privacidad: micro={mic} cámara={camera} {next.App}");
        Changed?.Invoke(next);
    }

    /// <summary>¿Alguna app (empaquetada o en <c>NonPackaged</c>) tiene la sesión abierta?</summary>
    private static (bool InUse, string App) InUse(string capability)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey($@"{Root}\{capability}");
            if (root is null) return (false, string.Empty);
            if (FindActive(root) is { } packaged) return (true, packaged);
            using var nonPackaged = root.OpenSubKey("NonPackaged");
            if (nonPackaged is not null && FindActive(nonPackaged) is { } desktop) return (true, desktop);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Error("Privacidad: no se pudo leer el registro", ex);
        }
        return (false, string.Empty);
    }

    private static string? FindActive(RegistryKey parent)
    {
        // Windows deja entradas con Stop = 0 si la app se cerró o actualizó a mitad de uso: se descartan
        // las que empezaron antes de arrancar el equipo y, en apps de escritorio, las de un programa que ya no está abierto.
        var boot = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        foreach (var name in parent.GetSubKeyNames())
        {
            if (name == "NonPackaged") continue;
            using var app = parent.OpenSubKey(name);
            if (app?.GetValue("LastUsedTimeStart") is not long start || start == 0) continue;
            if (app.GetValue("LastUsedTimeStop") is not long stop || stop != 0) continue;
            var started = DateTime.FromFileTimeUtc(start);
            if (started < boot) continue;
            if (name.Contains('#') && !IsRunningSince(name.Replace('#', '\\'), started)) continue;
            return AppName(name);
        }
        return null;
    }

    /// <summary>¿Hay un proceso de ese ejecutable abierto desde antes de <paramref name="since"/>?</summary>
    private static bool IsRunningSince(string path, DateTime since)
    {
        foreach (var process in System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path)))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase)
                        && process.StartTime.ToUniversalTime() <= since.AddSeconds(5))
                        return true;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // Proceso elevado o que acaba de cerrarse: con el mismo nombre basta.
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// "Microsoft.WindowsCamera_8wekyb3d8bbwe" → "WindowsCamera";
    /// "C:#Program Files#Discord#Discord.exe" → "Discord".
    /// </summary>
    private static string AppName(string key)
    {
        if (key.Contains('#')) return Path.GetFileNameWithoutExtension(key.Replace('#', '\\'));
        var name = key.Split('_')[0];
        return name[(name.LastIndexOf('.') + 1)..];
    }

    public void Dispose()
    {
        _disposed = true;
        _debounce.Stop();
        foreach (var watch in _watches) watch.Dispose();
        _watches.Clear();
    }

    /// <summary>Aviso de cambios en una clave y sus subclaves; se vuelve a armar tras cada aviso.</summary>
    private sealed class Watch : IDisposable
    {
        private readonly RegistryKey _key;
        private readonly AutoResetEvent _event = new(false);
        private readonly RegisteredWaitHandle _wait;
        private readonly Action _changed;
        private volatile bool _disposed;

        public Watch(RegistryKey key, Action changed)
        {
            _key = key;
            _changed = changed;
            Arm();
            _wait = ThreadPool.RegisterWaitForSingleObject(_event, (_, _) =>
            {
                if (_disposed) return;
                Arm();
                _changed();
            }, null, Timeout.Infinite, executeOnlyOnce: false);
        }

        private void Arm() => Win32.RegNotifyChangeKeyValue(_key.Handle, true,
            Win32.REG_NOTIFY_CHANGE_NAME | Win32.REG_NOTIFY_CHANGE_LAST_SET | Win32.REG_NOTIFY_THREAD_AGNOSTIC,
            _event.SafeWaitHandle, true);

        public void Dispose()
        {
            _disposed = true;
            _wait.Unregister(null);
            _key.Dispose();
            _event.Dispose();
        }
    }
}
