using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Parpa.Services;

/// <summary>
/// Título de una página para guardar un enlace soltado en el notch ("GUARDAR CON TÍTULO"). Solo se conecta
/// a esa página y solo cuando se suelta ahí; si no responde en unos segundos se usa el nombre del sitio.
/// </summary>
public static partial class LinkTitleService
{
    private const int MaxBytes = 512 * 1024; // el título va en la cabecera: no hace falta bajar la página entera
    private const int MaxLength = 120;

    public static async Task<string> FetchAsync(string url)
    {
        var fallback = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host.Replace("www.", string.Empty) : url;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"Mozilla/5.0 (Windows NT 10.0; Win64; x64) Parpa/{TrayService.Version}");
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(Loc.Culture.Name);
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType is not ("text/html" or "application/xhtml+xml"))
                return fallback;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[MaxBytes];
            var read = 0;
            int n;
            while (read < MaxBytes && (n = await stream.ReadAsync(buffer.AsMemory(read))) > 0) read += n;
            var html = Encoding.UTF8.GetString(buffer, 0, read);

            var match = OgTitle().Match(html) is { Success: true } og ? og : TitleTag().Match(html);
            if (!match.Success) return fallback;
            var title = Spaces().Replace(WebUtility.HtmlDecode(match.Groups["t"].Value), " ").Trim();
            if (title.Length == 0) return fallback;
            return title.Length > MaxLength ? title[..(MaxLength - 1)] + "…" : title;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException)
        {
            Log.Error("Enlace: no se pudo leer el título", ex);
            return fallback;
        }
    }

    [GeneratedRegex("""<meta[^>]+property=["']og:title["'][^>]+content=["'](?<t>[^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitle();

    [GeneratedRegex(@"<title[^>]*>(?<t>[^<]+)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
