using System.Diagnostics;
using System.Runtime.InteropServices;
using Parpa.ViewModels;

namespace Parpa.Services;

/// <summary>
/// Convierte los avisos de otras apps en avisos del notch, aplicando Ajustes:
/// apps desactivadas, No molestar (las llamadas pasan) y contenido (completo / solo remitente / oculto).
/// Windows no permite pulsar los botones de avisos ajenos, así que las acciones son ABRIR la app y MÁS TARDE.
/// </summary>
public sealed class NoticeCenter : IDisposable
{
    /// <summary>Indicios de llamada entrante en el texto del aviso (las apps no lo marcan de otra forma legible).</summary>
    private static readonly string[] CallHints =
        ["llamada entrante", "te está llamando", "videollamada entrante", "incoming call", "is calling you", "incoming video call"];

    private readonly NotchViewModel _vm;
    private readonly ParpaSettings _s;
    private readonly NotificationService _notifications = new();

    public NoticeCenter(NotchViewModel vm, ParpaSettings settings)
    {
        _vm = vm;
        _s = settings;
    }

    /// <summary>Permiso de Windows para leer avisos (Inicio y Ajustes muestran el estado denegado en la fase 4).</summary>
    public UserNotificationListenerAccess Access => _notifications.Access;

    /// <summary>Vuelve a comprobar el permiso (tras pulsar ACTIVAR).</summary>
    public Task<bool> RetryAsync() => _notifications.RetryAsync();

    public Task StartAsync()
    {
        _notifications.Arrived += OnArrived;
        return _notifications.StartAsync();
    }

    private void OnArrived(AppNotice notice)
    {
        if (notice.App.Equals(Loc.Get("app_name"), StringComparison.OrdinalIgnoreCase)) return;

        var isCall = CallHints.Any(h => $"{notice.Title} {notice.Body}".Contains(h, StringComparison.OrdinalIgnoreCase));
        if (!IsAppAllowed(notice.App))
        {
            Log.Info($"Avisos: «{notice.App}» desactivada en Ajustes");
            return;
        }
        if (_s.DndActive && !isCall) return; // No molestar: se descarta en silencio; las llamadas siguen entrando
        if (isCall) Log.Info($"Avisos: «{notice.App}» es una llamada");

        var app = Loc.Upper(notice.App);
        var sender = notice.Title;
        var head = _s.Content == "hidden" ? app : $"{app} · {Loc.Upper(sender)}";
        var body = _s.Content switch
        {
            "hidden" => Loc.Get("peek_new_hidden"),
            "sender" => Loc.Format("peek_new_from", ("name", sender)),
            _ => notice.Body.Length > 0 ? notice.Body : sender,
        };

        _vm.EnqueuePeek(new NotchPeek(
            Glyph: isCall ? "llamada" : GlyphFor(notice.App),
            Head: head,
            When: isCall ? string.Empty : Loc.Get("peek_now"),
            Body: body,
            PrimaryLabel: Loc.Get("peek_open"),
            SecondaryLabel: Loc.Get(isCall ? "peek_ignore" : "peek_later"),
            Priority: isCall ? NotchPeek.PriorityCall : NotchPeek.PriorityMessage,
            IsCall: isCall,
            OnPrimary: () => OpenApp(notice.AppUserModelId)));
    }

    /// <summary>Las apps que no están en la lista de Ajustes pueden avisar (como el prototipo).</summary>
    private bool IsAppAllowed(string app)
    {
        foreach (var (name, allowed) in _s.Apps)
            if (app.Equals(name, StringComparison.OrdinalIgnoreCase) || app.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase))
                return allowed;
        return true;
    }

    private static string GlyphFor(string app) =>
        app.Contains("claude", StringComparison.OrdinalIgnoreCase) ? "claude" : "mensaje";

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    /// <summary>Abre la app que avisó (a la app, no a la conversación: Windows no expone más).</summary>
    private static void OpenApp(string appUserModelId)
    {
        try
        {
            AllowSetForegroundWindow(-1); // ASFW_ANY: la app abierta puede pasar al frente
            Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{appUserModelId}") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error($"Avisos: no se pudo abrir {appUserModelId}", ex);
        }
    }

    public void Dispose() => _notifications.Dispose();
}
