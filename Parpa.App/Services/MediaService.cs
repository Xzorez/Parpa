using Microsoft.UI.Dispatching;
using Windows.Media.Control;

namespace Parpa.Services;

/// <summary>Lo que suena según las sesiones de medios del sistema (GlobalSystemMediaTransportControlsSessionManager).</summary>
public sealed class MediaService
{
    public sealed record NowPlaying(string Title, string Artist, bool IsPlaying);

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private int _refreshId;

    /// <summary>Sesión en reproducción o en pausa; <c>null</c> si no hay ninguna.</summary>
    public NowPlaying? Current { get; private set; }

    public event Action? Changed;

    public async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception)
        {
            return; // sin acceso a las sesiones de medios: no hay actividad de música
        }
        _manager.CurrentSessionChanged += (_, _) => _dispatcher.TryEnqueue(AttachCurrent);
        AttachCurrent();
    }

    private void AttachCurrent()
    {
        if (_session is not null)
        {
            _session.PlaybackInfoChanged -= OnSessionChanged;
            _session.MediaPropertiesChanged -= OnSessionChanged;
        }
        _session = _manager?.GetCurrentSession();
        if (_session is not null)
        {
            _session.PlaybackInfoChanged += OnSessionChanged;
            _session.MediaPropertiesChanged += OnSessionChanged;
        }
        _ = RefreshAsync();
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) =>
        _dispatcher.TryEnqueue(() => _ = RefreshAsync());

    private async Task RefreshAsync()
    {
        var id = ++_refreshId;
        NowPlaying? next = null;
        try
        {
            var status = _session?.GetPlaybackInfo()?.PlaybackStatus;
            if (_session is not null && status is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                    or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
            {
                var props = await _session.TryGetMediaPropertiesAsync();
                next = new NowPlaying(props?.Title ?? string.Empty, props?.Artist ?? string.Empty,
                    status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
            }
        }
        catch (Exception)
        {
            // La app de música cerró la sesión mientras se leía.
        }

        if (id != _refreshId || next == Current) return;
        Current = next;
        Changed?.Invoke();
    }
}
