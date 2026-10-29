using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Dispatching;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Parpa.Services;

/// <summary>Aviso de otra app leído del centro de actividades.</summary>
/// <param name="Title">Primera línea (normalmente el remitente).</param>
/// <param name="Body">Resto de líneas.</param>
public sealed record AppNotice(uint Id, string App, string AppUserModelId, string Title, string Body);

/// <summary>
/// Avisos de otras apps (UserNotificationListener). Sin identidad de paquete no existe el evento
/// NotificationChanged, y leer la lista cuesta ~9 ms de CPU, así que no se consulta a ciegas:
/// <list type="bullet">
/// <item>Cuando Windows muestra el banner de un aviso (ShellExperienceHost descubre su CoreWindow,
/// EVENT_OBJECT_UNCLOAKED) se lee la lista al momento.</item>
/// <item>Con un banner en pantalla, los siguientes avisos se apilan sin volver a descubrir la ventana:
/// durante 8 s tras cada banner se consulta cada segundo.</item>
/// <item>Una consulta de respaldo cada 5 s recoge los avisos que llegan sin banner
/// (apps con banners desactivados). Si Windows no está mostrando banners
/// (SHQueryUserNotificationState ≠ QUNS_ACCEPTS_NOTIFICATIONS: pantalla completa, horas de silencio…),
/// no hay señal que escuchar y la consulta pasa a cada segundo.</item>
/// </list>
/// Con el paquete firmado (fase 6) se pasará a NotificationChanged.
/// </summary>
public sealed class NotificationService : IDisposable
{
    private const uint EVENT_OBJECT_UNCLOAKED = 0x8018;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const string ToastHostProcess = "ShellExperienceHost";
    private const string ToastWindowClass = "Windows.UI.Core.CoreWindow";

    private static readonly TimeSpan FallbackInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BurstInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BurstLength = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan BannerRetry = TimeSpan.FromMilliseconds(400);

    private readonly UserNotificationListener _listener = UserNotificationListener.Current;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _fallbackTimer;
    private readonly DispatcherQueueTimer _retryTimer;
    private readonly WinEventProc _bannerProc;
    private IntPtr _bannerHook;
    private HashSet<uint> _seen = new();
    private DateTime _burstUntil;
    private bool _relaxed;
    private bool _polling;
    private bool _pollAgain;

    public UserNotificationListenerAccess Access { get; private set; } = UserNotificationListenerAccess.Unknown;

    /// <summary>Aviso nuevo, en el hilo de la UI.</summary>
    public event Action<AppNotice>? Arrived;

    public NotificationService()
    {
        _fallbackTimer = _dispatcher.CreateTimer();
        _fallbackTimer.Interval = FallbackInterval;
        _fallbackTimer.Tick += (_, _) =>
        {
            var burst = DateTime.UtcNow < _burstUntil;
            var fast = burst || (!_relaxed && !WindowsShowsBanners());
            _fallbackTimer.Interval = fast ? BurstInterval : FallbackInterval;
            _ = PollAsync(announce: true, burst ? "ráfaga" : fast ? "sin banners" : "respaldo");
        };
        _retryTimer = _dispatcher.CreateTimer();
        _retryTimer.IsRepeating = false;
        _retryTimer.Interval = BannerRetry;
        _retryTimer.Tick += (_, _) => _ = PollAsync(announce: true, "reintento");
        _bannerProc = OnWinEvent;
    }

    public async Task StartAsync()
    {
        try
        {
            Access = await _listener.RequestAccessAsync() switch
            {
                UserNotificationListenerAccessStatus.Allowed => UserNotificationListenerAccess.Allowed,
                UserNotificationListenerAccessStatus.Denied => UserNotificationListenerAccess.Denied,
                _ => UserNotificationListenerAccess.Unknown,
            };
        }
        catch (Exception ex)
        {
            Log.Error("Avisos: no se pudo pedir acceso", ex);
            return;
        }
        if (Access != UserNotificationListenerAccess.Allowed)
        {
            Log.Info($"Avisos: acceso {Access}");
            return;
        }

        // Los avisos que ya estaban al arrancar no se muestran.
        await PollAsync(announce: false);
        _bannerHook = SetWinEventHook(EVENT_OBJECT_UNCLOAKED, EVENT_OBJECT_UNCLOAKED, IntPtr.Zero, _bannerProc,
            0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (_bannerHook == IntPtr.Zero) Log.Info("Avisos: sin gancho de banners; solo consulta de respaldo");
        _fallbackTimer.Interval = WindowsShowsBanners() ? FallbackInterval : BurstInterval;
        _fallbackTimer.Start();
    }

    /// <summary>Tras volver de Configuración: si ahora hay permiso, empieza a leer avisos.</summary>
    /// <returns><c>true</c> si el acceso ha cambiado a permitido.</returns>
    public async Task<bool> RetryAsync()
    {
        if (Access == UserNotificationListenerAccess.Allowed) return false;
        if (_listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed) return false;
        await StartAsync();
        return Access == UserNotificationListenerAccess.Allowed;
    }

    /// <summary>
    /// Notch apartado por pantalla completa: los avisos esperan en la cola igualmente, así que basta la consulta
    /// de respaldo (cada 5 s) en vez de cada segundo (CPU durante un juego). Al volver se lee la lista al momento.
    /// </summary>
    public void SetRelaxed(bool relaxed)
    {
        if (_relaxed == relaxed) return;
        _relaxed = relaxed;
        if (relaxed || !_fallbackTimer.IsRunning) return;
        _ = PollAsync(announce: true, "vuelta");
        _fallbackTimer.Interval = WindowsShowsBanners() ? FallbackInterval : BurstInterval;
        _fallbackTimer.Start();
    }

    /// <summary>Llega por la cola de mensajes del hilo de la UI (gancho fuera de proceso).</summary>
    private void OnWinEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || hwnd == IntPtr.Zero || !IsToastBanner(hwnd)) return;
        _ = PollAsync(announce: true, "banner");
        _retryTimer.Start(); // por si el aviso aún no estaba en la lista
        _burstUntil = DateTime.UtcNow + BurstLength;
        _fallbackTimer.Interval = BurstInterval;
        _fallbackTimer.Start(); // reinicia el intervalo desde ahora
    }

    private const int QUNS_ACCEPTS_NOTIFICATIONS = 5;

    /// <summary>Windows solo muestra banners (y por tanto avisa al gancho) en este estado.</summary>
    private static bool WindowsShowsBanners() =>
        SHQueryUserNotificationState(out var state) != 0 || state == QUNS_ACCEPTS_NOTIFICATIONS;

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    private static bool IsToastBanner(IntPtr hwnd)
    {
        var cls = new StringBuilder(64);
        if (GetClassName(hwnd, cls, cls.Capacity) == 0 || cls.ToString() != ToastWindowClass) return false;
        GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName.Equals(ToastHostProcess, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task PollAsync(bool announce, string source = "inicio")
    {
        if (_polling)
        {
            _pollAgain = true;
            return;
        }
        _polling = true;
        try
        {
            do
            {
                _pollAgain = false;
                var current = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                var ids = new HashSet<uint>(current.Count);
                foreach (var notification in current)
                {
                    ids.Add(notification.Id);
                    if (announce && !_seen.Contains(notification.Id) && Read(notification) is { } notice)
                    {
                        Log.Info($"Avisos: nuevo de «{notice.App}» (vía {source})");
                        Arrived?.Invoke(notice);
                    }
                }
                _seen = ids;
            } while (_pollAgain);
        }
        catch (Exception ex)
        {
            Log.Error("Avisos: no se pudo leer la lista", ex);
        }
        finally
        {
            _polling = false;
        }
    }

    private static AppNotice? Read(UserNotification notification)
    {
        try
        {
            var texts = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?
                .GetTextElements().Select(t => t.Text.Trim()).Where(t => t.Length > 0).ToList() ?? [];
            if (texts.Count == 0) return null;
            var app = notification.AppInfo;
            return new AppNotice(
                notification.Id,
                app.DisplayInfo.DisplayName,
                app.AppUserModelId,
                texts[0],
                string.Join(" ", texts.Skip(1)));
        }
        catch (Exception ex)
        {
            Log.Error("Avisos: aviso ilegible", ex);
            return null;
        }
    }

    public void Dispose()
    {
        if (_bannerHook != IntPtr.Zero) UnhookWinEvent(_bannerHook);
        _bannerHook = IntPtr.Zero;
        _fallbackTimer.Stop();
    }

    private delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc proc, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);
}

public enum UserNotificationListenerAccess { Unknown, Allowed, Denied }
