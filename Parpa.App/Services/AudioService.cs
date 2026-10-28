using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Parpa.Services;

/// <summary>
/// Volumen de la salida por defecto (IAudioEndpointVolume + callback) y cambios de dispositivo
/// (IMMNotificationClient) para detectar auriculares.
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

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow != DataFlow.Render || role != Role.Multimedia) return;
        _dispatcher.TryEnqueue(() =>
        {
            AttachDefault();
            if (IsHeadphones) HeadphonesConnected?.Invoke(DeviceName);
        });
    }

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState) { }
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
}
