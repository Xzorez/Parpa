using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Parpa.Services;

/// <summary>
/// Comprueba en GitHub Releases si hay una versión más nueva y la instala. Solo cuando se pulsa BUSCAR
/// (y después ACTUALIZAR) en Ajustes: Parpa no se conecta a internet por su cuenta.
/// </summary>
public static class UpdateService
{
    private const string Repo = "Xzorez/Parpa";

    /// <param name="Newer">Hay una versión publicada mayor que la instalada.</param>
    /// <param name="Url">Página del release (descargas y novedades).</param>
    /// <param name="Setup">Instalador de esta arquitectura, si el release lo trae.</param>
    /// <param name="Sums">SHA256SUMS.txt del release, para comprobar la descarga.</param>
    public sealed record Result(bool Newer, string Latest, string Url, Asset? Setup = null, Asset? Sums = null)
    {
        /// <summary>Se puede instalar desde la app (si no, se abre la página del release).</summary>
        public bool CanInstall => Newer && Setup is not null && Sums is not null;
    }

    public sealed record Asset(string Name, string Url, long Size);

    /// <returns><c>null</c> si no se pudo comprobar (sin conexión, GitHub no responde…).</returns>
    public static async Task<Result?> CheckAsync()
    {
        try
        {
            using var http = Client(TimeSpan.FromSeconds(10));
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var json = JsonDocument.Parse(await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
            var root = json.RootElement;
            var latest = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? string.Empty;
            var url = root.GetProperty("html_url").GetString() ?? $"https://github.com/{Repo}/releases/latest";
            var newer = Version.TryParse(latest, out var published) && Version.TryParse(TrayService.Version, out var installed) && published > installed;

            var setupName = $"Parpa-Setup-{latest}-{Arch}.exe";
            Asset? setup = null, sums = null;
            if (root.TryGetProperty("assets", out var assets))
                foreach (var a in assets.EnumerateArray())
                {
                    var asset = new Asset(a.GetProperty("name").GetString() ?? string.Empty,
                        a.GetProperty("browser_download_url").GetString() ?? string.Empty, a.GetProperty("size").GetInt64());
                    if (asset.Name.Equals(setupName, StringComparison.OrdinalIgnoreCase)) setup = asset;
                    else if (asset.Name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) sums = asset;
                }
            return new Result(newer, latest, url, setup, sums);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Error("Actualizaciones: no se pudo comprobar", ex);
            return null;
        }
    }

    /// <summary>
    /// Descarga el instalador, comprueba su SHA-256 con SHA256SUMS.txt y lo lanza en silencio.
    /// El instalador cierra Parpa, se instala encima (ajustes y datos se conservan) y la vuelve a abrir (/RELAUNCH).
    /// </summary>
    /// <param name="progress">De 0 a 1 mientras descarga.</param>
    /// <returns><c>false</c> si algo falló; en ese caso no se ha tocado nada.</returns>
    public static async Task<bool> InstallAsync(Result update, IProgress<double> progress)
    {
        if (!update.CanInstall) return false;
        var setup = update.Setup!;
        var dir = Path.Combine(Path.GetTempPath(), "Parpa-Update");
        var path = Path.Combine(dir, setup.Name);
        try
        {
            using var http = Client(TimeSpan.FromMinutes(10));
            var expected = Expected(await http.GetStringAsync(update.Sums!.Url), setup.Name);
            if (expected is null)
            {
                Log.Info($"Actualizaciones: {setup.Name} no aparece en SHA256SUMS.txt");
                return false;
            }

            Directory.CreateDirectory(dir);
            using (var response = await http.GetAsync(setup.Url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? setup.Size;
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = File.Create(path);
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read));
                    done += read;
                    if (total > 0) progress.Report(Math.Min(1, (double)done / total));
                }
            }

            string actual;
            await using (var file = File.OpenRead(path))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant();
            if (actual != expected)
            {
                Log.Info($"Actualizaciones: la huella de {setup.Name} no coincide ({actual} ≠ {expected})");
                File.Delete(path);
                return false;
            }

            Log.Info($"Actualizaciones: instalando {update.Latest}");
            Process.Start(new ProcessStartInfo(path, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH") { UseShellExecute = false });
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Log.Error("Actualizaciones: no se pudo instalar", ex);
            return false;
        }
    }

    /// <summary>Líneas «huella  nombre» (formato de sha256sum).</summary>
    private static string? Expected(string sums, string name) =>
        sums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', 2, StringSplitOptions.TrimEntries))
            .FirstOrDefault(p => p.Length == 2 && p[1].Equals(name, StringComparison.OrdinalIgnoreCase))?[0].ToLowerInvariant();

    private static string Arch => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";

    private static HttpClient Client(TimeSpan timeout)
    {
        var http = new HttpClient { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Parpa/{TrayService.Version}");
        return http;
    }
}
