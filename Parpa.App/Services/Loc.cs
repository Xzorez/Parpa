using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Parpa.Services;

/// <summary>
/// Textos de Strings/{es-ES,en-US}/Resources.resw (generados desde Data/strings.json).
/// Clave = "&lt;grupo&gt;_&lt;clave&gt;", p. ej. <c>windows_home</c>. El idioma cambia al instante.
/// </summary>
public static class Loc
{
    private static readonly ResourceManager Manager = new();
    private static readonly ResourceMap Map = Manager.MainResourceMap.GetSubtree("Resources");
    private static ResourceContext _context = Manager.CreateResourceContext();

    public static CultureInfo Culture { get; private set; } = CultureInfo.CurrentUICulture;

    public static event Action? LanguageChanged;

    /// <param name="setting">Ajuste <c>lang</c>: <c>auto</c>, <c>es</c> o <c>en</c>.</param>
    public static void SetLanguage(string setting)
    {
        var lang = setting switch
        {
            "es" => "es-ES",
            "en" => "en-US",
            // auto: idioma de pantalla de Windows; si no es es/en, inglés.
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es-ES" : "en-US",
        };
        _context = Manager.CreateResourceContext();
        _context.QualifierValues["Language"] = lang;
        Culture = new CultureInfo(lang);
        LanguageChanged?.Invoke();
    }

    public static string Get(string key) => Map.TryGetValue(key, _context)?.ValueAsString ?? key;

    /// <summary>Sustituye marcadores <c>{nombre}</c>: <c>Loc.Format("osd_display_n", ("n", 1))</c>.</summary>
    public static string Format(string key, params (string Name, object Value)[] args)
    {
        var text = Get(key);
        foreach (var (name, value) in args)
            text = text.Replace("{" + name + "}", Convert.ToString(value, Culture));
        return text;
    }

    /// <summary>Etiquetas mono en MAYÚSCULAS con la cultura activa.</summary>
    /// <remarks>El rediseño estilo isla usa texto tipo frase: ya no se pasa a mayúsculas.</remarks>
    public static string Upper(string text) => text;

    private static readonly HashSet<string> Acronyms = ["PDF", "PNG", "ZIP", "OCR", "CPU", "GPU", "RAM"];

    /// <summary>
    /// Claves antiguas en MAYÚSCULAS (homeCards, actions de settings.json) → tipo frase para mostrarlas:
    /// "IMÁGENES · A PNG" → "Imágenes · A PNG". Las claves guardadas no cambian.
    /// </summary>
    public static string Sentence(string text)
    {
        var parts = text.Split(" · ");
        for (var i = 0; i < parts.Length; i++)
        {
            var words = parts[i].Split(' ').Select(w => Acronyms.Contains(w) ? w : w.ToLowerInvariant()).ToArray();
            var joined = string.Join(' ', words);
            parts[i] = joined.Length > 0 ? char.ToUpperInvariant(joined[0]) + joined[1..] : joined;
        }
        return string.Join(" · ", parts);
    }
}
