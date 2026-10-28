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
}
