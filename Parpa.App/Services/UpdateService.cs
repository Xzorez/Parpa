using System.Net.Http;
using System.Text.Json;

namespace Parpa.Services;

/// <summary>
/// Comprueba en GitHub Releases si hay una versión más nueva. Solo cuando se pulsa BUSCAR en Ajustes:
/// Parpa no se conecta a internet por su cuenta.
/// </summary>
public static class UpdateService
{
    private const string Repo = "Xzorez/Parpa";

    /// <param name="Newer">Hay una versión publicada mayor que la instalada.</param>
    /// <param name="Url">Página del release (descargas y novedades).</param>
    public sealed record Result(bool Newer, string Latest, string Url);

    /// <returns><c>null</c> si no se pudo comprobar (sin conexión, GitHub no responde…).</returns>
    public static async Task<Result?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"Parpa/{TrayService.Version}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var json = JsonDocument.Parse(await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
            var latest = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? string.Empty;
            var url = json.RootElement.GetProperty("html_url").GetString() ?? $"https://github.com/{Repo}/releases/latest";
            var newer = Version.TryParse(latest, out var published) && Version.TryParse(TrayService.Version, out var installed) && published > installed;
            return new Result(newer, latest, url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Error("Actualizaciones: no se pudo comprobar", ex);
            return null;
        }
    }
}
