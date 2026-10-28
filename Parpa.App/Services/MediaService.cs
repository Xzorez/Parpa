using Microsoft.UI.Dispatching;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Parpa.Services;

/// <summary>
/// Sesiones de medios del sistema (GlobalSystemMediaTransportControlsSessionManager).
/// <list type="bullet">
/// <item><see cref="Current"/>: lo que está sonando (actividad del notch). Una app abierta o en pausa no cuenta.</item>
/// <item><see cref="Card"/>: la sesión para la tarjeta MÚSICA de Inicio, también en pausa, con controles.</item>
/// </list>
/// </summary>
public sealed class MediaService
{
    public sealed record NowPlaying(string Title, string Artist);

    /// <summary>Tarjeta MÚSICA: posición y duración para la barra de progreso.</summary>
    public sealed record CardInfo(string Title, string Artist, string App, bool IsPlaying,
        TimeSpan Position, TimeSpan Duration, DateTime PositionAt, IRandomAccessStreamReference? Thumbnail)
    {
        /// <summary>Posición estimada ahora (avanza sola mientras suena).</summary>
        public TimeSpan PositionNow => IsPlaying && Duration > TimeSpan.Zero
            ? TimeSpan.FromTicks(Math.Min(Duration.Ticks, (Position + (DateTime.UtcNow - PositionAt)).Ticks))
            : Position;
    }

    /// <summary>Margen antes de quitar la música al parar, para no parpadear entre canciones.</summary>
    private static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(1);

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _stopTimer;
    private readonly List<GlobalSystemMediaTransportControlsSession> _sessions = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _cardSession;
    private int _refreshId;

    /// <summary>Sesión que está sonando; <c>null</c> si no suena nada.</summary>
    public NowPlaying? Current { get; private set; }

    /// <summary>Sesión de la tarjeta MÚSICA (sonando o en pausa); <c>null</c> = "Nada sonando".</summary>
    public CardInfo? Card { get; private set; }

    public event Action? Changed;

    public event Action? CardChanged;

    public MediaService()
    {
        _stopTimer = _dispatcher.CreateTimer();
        _stopTimer.IsRepeating = false;
        _stopTimer.Interval = StopGrace;
        _stopTimer.Tick += (_, _) => SetCurrent(null);
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

    // ---- Controles de la tarjeta ----

    public async Task PreviousAsync() => await Try(s => s.TrySkipPreviousAsync().AsTask());

    public async Task PlayPauseAsync() => await Try(s => s.TryTogglePlayPauseAsync().AsTask());

    public async Task NextAsync() => await Try(s => s.TrySkipNextAsync().AsTask());

    private async Task Try(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> command)
    {
        if (_cardSession is null) return;
        try
        {
            await command(_cardSession);
        }
        catch (Exception ex)
        {
            Log.Error("Música: el reproductor no aceptó el control", ex);
        }
    }

    // ---- Sesiones ----

    private void AttachSessions()
    {
        foreach (var session in _sessions)
        {
            session.PlaybackInfoChanged -= OnSessionChanged;
            session.MediaPropertiesChanged -= OnSessionChanged;
            session.TimelinePropertiesChanged -= OnSessionChanged;
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
            session.TimelinePropertiesChanged += OnSessionChanged;
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

    /// <summary>
    /// Sonando: la sesión actual de Windows si suena; si no, la primera que suene.
    /// Tarjeta: la que suena; si no, la actual de Windows (en pausa).
    /// </summary>
    private async Task RefreshAsync()
    {
        var id = ++_refreshId;
        var current = _manager?.GetCurrentSession();
        var playing = current is not null && IsPlaying(current) ? current : _sessions.FirstOrDefault(IsPlaying);
        var card = playing ?? current ?? _sessions.FirstOrDefault();

        CardInfo? cardInfo = null;
        if (card is not null)
        {
            try
            {
                var props = await card.TryGetMediaPropertiesAsync();
                var timeline = card.GetTimelineProperties();
                cardInfo = new CardInfo(
                    props?.Title ?? string.Empty,
                    props?.Artist ?? string.Empty,
                    AppName(card.SourceAppUserModelId),
                    card == playing,
                    timeline.Position,
                    timeline.EndTime - timeline.StartTime,
                    timeline.LastUpdatedTime.UtcDateTime,
                    props?.Thumbnail);
            }
            catch (Exception)
            {
                // La sesión desapareció mientras se leía; llegará otro evento.
                return;
            }
        }
        if (id != _refreshId) return;

        _cardSession = card;
        if (cardInfo != Card)
        {
            Card = cardInfo;
            CardChanged?.Invoke();
        }

        if (playing is null)
        {
            // Pausa o fin: se quita tras el margen (salvo que vuelva a sonar antes).
            if (Current is not null && !_stopTimer.IsRunning) _stopTimer.Start();
            return;
        }
        _stopTimer.Stop();
        SetCurrent(new NowPlaying(cardInfo!.Title, cardInfo.Artist));
    }

    /// <summary>"Spotify.exe" → "Spotify"; "Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic" → "ZuneMusic".</summary>
    private static string AppName(string appUserModelId)
    {
        var name = appUserModelId.Split('!').Last().Split('\\').Last();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name.Split('.').Last();
    }

    private void SetCurrent(NowPlaying? next)
    {
        if (next == Current) return;
        Current = next;
        Changed?.Invoke();
    }
}
