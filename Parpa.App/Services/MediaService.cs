using Microsoft.UI.Dispatching;
using Windows.Media.Control;

namespace Parpa.Services;

/// <summary>
/// Lo que suena según las sesiones de medios del sistema (GlobalSystemMediaTransportControlsSessionManager).
/// Solo cuenta lo que se está reproduciendo: una app de música abierta o en pausa no es actividad.
/// </summary>
public sealed class MediaService
{
    public sealed record NowPlaying(string Title, string Artist);

    /// <summary>Margen antes de quitar la música al parar, para no parpadear entre canciones.</summary>
    private static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(1);

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _stopTimer;
    private readonly List<GlobalSystemMediaTransportControlsSession> _sessions = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private int _refreshId;

    /// <summary>Sesión que está sonando; <c>null</c> si no suena nada.</summary>
    public NowPlaying? Current { get; private set; }

    public event Action? Changed;

    public MediaService()
    {
        _stopTimer = _dispatcher.CreateTimer();
        _stopTimer.IsRepeating = false;
        _stopTimer.Interval = StopGrace;
        _stopTimer.Tick += (_, _) => Set(null);
    }

    public async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Música: sin acceso a las sesiones de medios", ex);
            return;
        }
        _manager.SessionsChanged += (_, _) => _dispatcher.TryEnqueue(AttachSessions);
        _manager.CurrentSessionChanged += (_, _) => _dispatcher.TryEnqueue(() => _ = RefreshAsync());
        AttachSessions();
    }

    private void AttachSessions()
    {
        foreach (var session in _sessions)
        {
            session.PlaybackInfoChanged -= OnSessionChanged;
            session.MediaPropertiesChanged -= OnSessionChanged;
        }
        _sessions.Clear();
        try
        {
            _sessions.AddRange(_manager?.GetSessions() ?? []);
        }
        catch (Exception ex)
        {
            Log.Error("Música: no se pudieron leer las sesiones", ex);
        }
        foreach (var session in _sessions)
        {
            session.PlaybackInfoChanged += OnSessionChanged;
            session.MediaPropertiesChanged += OnSessionChanged;
        }
        _ = RefreshAsync();
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) =>
        _dispatcher.TryEnqueue(() => _ = RefreshAsync());

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch (Exception)
        {
            return false; // la app cerró la sesión
        }
    }

    /// <summary>La sesión actual de Windows si suena; si no, la primera que esté sonando.</summary>
    private async Task RefreshAsync()
    {
        var id = ++_refreshId;
        var current = _manager?.GetCurrentSession();
        var playing = current is not null && IsPlaying(current) ? current : _sessions.FirstOrDefault(IsPlaying);

        if (playing is null)
        {
            // Pausa o fin: se quita tras el margen (salvo que vuelva a sonar antes).
            if (Current is not null && !_stopTimer.IsRunning) _stopTimer.Start();
            return;
        }

        NowPlaying next;
        try
        {
            var props = await playing.TryGetMediaPropertiesAsync();
            next = new NowPlaying(props?.Title ?? string.Empty, props?.Artist ?? string.Empty);
        }
        catch (Exception)
        {
            return; // la sesión desapareció mientras se leía; llegará otro evento
        }

        if (id != _refreshId) return;
        _stopTimer.Stop();
        Set(next);
    }

    private void Set(NowPlaying? next)
    {
        if (next == Current) return;
        Current = next;
        Changed?.Invoke();
    }
}
