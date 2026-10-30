using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Parpa.Services;

/// <summary>Salida de audio (altavoces, auriculares…).</summary>
public sealed record AudioOutput(string Id, string Name, bool IsDefault);

/// <summary>Una app que está sonando (o puede sonar) en la salida por defecto; Discord, Chrome… usan varios procesos.</summary>
public sealed record AppVolume(string Name, int Volume, bool Muted, IReadOnlyList<uint> ProcessIds);

/// <summary>
/// Volumen de la salida por defecto (IAudioEndpointVolume + callback) y cambios de dispositivo
/// (IMMNotificationClient) para detectar auriculares. Desde la ventana Audio: elegir la salida,
/// silenciar el micrófono y el volumen de cada app.
/// </summary>
public sealed class AudioService : IMMNotificationClient, IDisposable
{
    // PKEY_AudioEndpoint_FormFactor: Headphones = 3, Headset = 5.
    private const uint FormFactorHeadphones = 3;
    private const uint FormFactorHeadset = 5;

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private (int Percent, bool Muted) _last = (-1, false);

    public bool IsHeadphones { get; private set; }

    public string DeviceName { get; private set; } = string.Empty;

    /// <summary>Volumen actual en % (0 si está en silencio).</summary>
    public int Volume => _last.Muted ? 0 : Math.Max(0, _last.Percent);

    /// <summary>Volumen en % y silencio, en el hilo de la UI.</summary>
    public event Action<int, bool>? VolumeChanged;

    /// <summary>La salida por defecto pasa a ser unos auriculares (nombre del dispositivo).</summary>
    public event Action<string>? HeadphonesConnected;

    /// <summary>Se conectó, quitó o cambió alguna salida o micrófono, en el hilo de la UI.</summary>
    public event Action? DevicesChanged;

    public void Start()
    {
        _enumerator.RegisterEndpointNotificationCallback(this);
        AttachDefault();
    }

    private void AttachDefault()
    {
        if (_device is not null)
        {
            _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
            _device.Dispose();
            _device = null;
        }
        try
        {
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (Exception ex)
        {
            Log.Error("Audio: sin salida por defecto", ex);
            IsHeadphones = false;
            return;
        }

        DeviceName = _device.FriendlyName;
        IsHeadphones = ReadFormFactor(_device) is FormFactorHeadphones or FormFactorHeadset;
        var volume = _device.AudioEndpointVolume;
        _last = ((int)Math.Round(volume.MasterVolumeLevelScalar * 100), volume.Mute);
        volume.OnVolumeNotification += OnVolumeNotification;
        Log.Info($"Audio: salida «{DeviceName}», auriculares={IsHeadphones}, volumen={_last.Percent}%");
    }

    private static uint? ReadFormFactor(MMDevice device)
    {
        try
        {
            var key = PropertyKeys.PKEY_AudioEndpoint_FormFactor;
            return device.Properties.Contains(key) ? Convert.ToUInt32(device.Properties[key].Value) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Llega en un hilo de Core Audio: se pasa al de la UI.
    private void OnVolumeNotification(AudioVolumeNotificationData data)
    {
        var state = ((int)Math.Round(data.MasterVolume * 100), data.Muted);
        _dispatcher.TryEnqueue(() =>
        {
            if (state == _last) return; // notificaciones por canal con el mismo volumen maestro
            _last = state;
            try
            {
                VolumeChanged?.Invoke(state.Item1, state.Muted);
            }
            catch (Exception ex)
            {
                Log.Error("Audio: al mostrar el volumen", ex);
            }
        });
    }

    // ---- Ventana Audio ----

    /// <summary>Salidas activas, con la que está por defecto marcada.</summary>
    public IReadOnlyList<AudioOutput> Outputs()
    {
        try
        {
            var current = _device?.ID;
            return _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(d => { using (d) return new AudioOutput(d.ID, d.FriendlyName, d.ID == current); })
                .ToList();
        }
        catch (COMException ex)
        {
            Log.Error("Audio: no se pudieron listar las salidas", ex);
            return [];
        }
    }

    /// <summary>
    /// Pone una salida por defecto para todo (como el menú de sonido de Windows). No hay API pública:
    /// se usa IPolicyConfig, la misma interfaz que usa el propio Windows.
    /// </summary>
    public void SetDefaultOutput(string id)
    {
        try
        {
            var policy = (IPolicyConfig)new PolicyConfigClient();
            try
            {
                foreach (var role in new[] { Role.Console, Role.Multimedia, Role.Communications })
                    Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, role));
            }
            finally
            {
                Marshal.ReleaseComObject(policy);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            Log.Error("Audio: no se pudo cambiar la salida", ex);
        }
    }

    /// <summary>Volumen de la salida por defecto, en %.</summary>
    public void SetVolume(int percent)
    {
        if (_device is null) return;
        var volume = _device.AudioEndpointVolume;
        volume.MasterVolumeLevelScalar = Math.Clamp(percent, 0, 100) / 100f;
        if (percent > 0 && volume.Mute) volume.Mute = false;
    }

    /// <summary>Micrófono por defecto: nombre y si está silenciado. <c>null</c> si no hay micrófono.</summary>
    public (string Name, bool Muted)? Microphone()
    {
        try
        {
            using var mic = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return (mic.FriendlyName, mic.AudioEndpointVolume.Mute);
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>Silencia o activa el micrófono por defecto (el mismo interruptor que el de Windows).</summary>
    public void SetMicrophoneMuted(bool muted)
    {
        try
        {
            using var mic = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            mic.AudioEndpointVolume.Mute = muted;
        }
        catch (COMException ex)
        {
            Log.Error("Audio: no se pudo cambiar el micrófono", ex);
        }
    }

    /// <summary>
    /// Apps con sonido en la salida por defecto, sin los sonidos del sistema. Una fila por app: sus procesos
    /// se juntan por nombre y la fila muestra el volumen de la primera sesión.
    /// </summary>
    public IReadOnlyList<AppVolume> Apps() => Sessions()
        .GroupBy(s => AppName((int)s.GetProcessID, s.DisplayName), StringComparer.CurrentCultureIgnoreCase)
        .Select(g =>
        {
            var volume = g.First().SimpleAudioVolume;
            return new AppVolume(g.Key, (int)Math.Round(volume.Volume * 100), volume.Mute, g.Select(s => s.GetProcessID).Distinct().ToList());
        })
        .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    /// <summary>
    /// La app de audio de una sesión de música de Windows: "Spotify.exe", "SpotifyAB.SpotifyMusic_…!Spotify",
    /// "Brave", "Chrome", "MSEdge"… llevan dentro el nombre de su proceso. <c>null</c> si no suena por la salida actual.
    /// En un navegador es el volumen de todo el navegador (Windows no separa las pestañas).
    /// </summary>
    public AppVolume? AppFor(string appUserModelId)
    {
        var id = appUserModelId.ToLowerInvariant();
        return Apps().FirstOrDefault(a => a.ProcessIds.Any(pid => ProcessName((int)pid) is { Length: >= 3 } name && id.Contains(name)));
    }

    private static readonly Dictionary<int, string> ProcessNames = new();

    private static string ProcessName(int processId)
    {
        if (ProcessNames.TryGetValue(processId, out var cached)) return cached;
        try
        {
            using var process = Process.GetProcessById(processId);
            return ProcessNames[processId] = process.ProcessName.ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return string.Empty; // ya no existe
        }
    }

    public void SetAppVolume(AppVolume app, int percent)
    {
        foreach (var session in Sessions().Where(s => app.ProcessIds.Contains(s.GetProcessID)))
        {
            session.SimpleAudioVolume.Volume = Math.Clamp(percent, 0, 100) / 100f;
            if (percent > 0) session.SimpleAudioVolume.Mute = false;
        }
    }

    public void SetAppMuted(AppVolume app, bool muted)
    {
        foreach (var session in Sessions().Where(s => app.ProcessIds.Contains(s.GetProcessID)))
            session.SimpleAudioVolume.Mute = muted;
    }

    private IEnumerable<AudioSessionControl> Sessions()
    {
        if (_device is null) yield break;
        SessionCollection sessions;
        try
        {
            var manager = _device.AudioSessionManager;
            manager.RefreshSessions();
            sessions = manager.Sessions;
        }
        catch (COMException ex)
        {
            Log.Error("Audio: no se pudieron leer las apps", ex);
            yield break;
        }
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (session.IsSystemSoundsSession || session.State == AudioSessionState.AudioSessionStateExpired || session.GetProcessID == 0) continue;
            yield return session;
        }
    }

    private static readonly Dictionary<int, string> AppNames = new();

    /// <summary>"Spotify", "Discord"… (descripción del ejecutable; si no, el nombre del proceso).</summary>
    private static string AppName(int processId, string displayName)
    {
        if (AppNames.TryGetValue(processId, out var cached)) return cached;
        var name = displayName.StartsWith('@') ? string.Empty : displayName;
        if (name.Length == 0)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                try
                {
                    name = process.MainModule?.FileVersionInfo.FileDescription ?? string.Empty;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // Proceso de otro usuario o protegido: solo se puede leer su nombre.
                }
                if (name.Length == 0) name = process.ProcessName;
            }
            catch (ArgumentException)
            {
                name = "?";
            }
        }
        return AppNames[processId] = name.Trim();
    }

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (role != Role.Multimedia && !(flow == DataFlow.Capture && role == Role.Communications)) return;
        _dispatcher.TryEnqueue(() =>
        {
            if (flow == DataFlow.Render)
            {
                AttachDefault();
                if (IsHeadphones) HeadphonesConnected?.Invoke(DeviceName);
            }
            DevicesChanged?.Invoke();
        });
    }

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState) => _dispatcher.TryEnqueue(() => DevicesChanged?.Invoke());
    void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId) { }
    void IMMNotificationClient.OnDeviceRemoved(string deviceId) { }
    void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

    public void Dispose()
    {
        _enumerator.UnregisterEndpointNotificationCallback(this);
        if (_device is not null) _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
        _device?.Dispose();
        _enumerator.Dispose();
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClient;

    /// <summary>IPolicyConfig (Windows 7 y posteriores): solo se usa SetDefaultEndpoint; el resto ocupa su sitio en la vtable.</summary>
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
        [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
        [PreserveSig] int ResetDeviceFormat(IntPtr a);
        [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
        [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int GetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int SetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, Role role);
        [PreserveSig] int SetEndpointVisibility(IntPtr a, int b);
    }
}
