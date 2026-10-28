using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Parpa.Services;

/// <summary>
/// Tema oscuro/claro en caliente. Cada color de tokens.json es un pincel único en los recursos
/// (<c>{Nombre}Brush</c>) al que se le cambia el color: todo lo que lo usa se actualiza solo.
/// </summary>
public static class Theme
{
    /// <summary>
    /// Colores de ventanas (Ajustes, bienvenida, menú de bandeja) que el diseño da solo en oscuro.
    /// En claro son derivados de la paleta clara.
    /// </summary>
    private static readonly Dictionary<string, (Color Dark, Color Light)> Extra = new()
    {
        ["windowBg"] = (Tokens.ParseHex("#111113"), Tokens.ParseHex("#efede8")),
        ["navSelected"] = (Tokens.ParseHex("#1a1a1d"), Tokens.ParseHex("#e2dfd9")),
        ["hoverFill"] = (Tokens.ParseHex("#1f1f23"), Tokens.ParseHex("#e4e1db")),
    };

    private static readonly Dictionary<string, SolidColorBrush> Brushes = new();
    private static readonly UISettings UiSettings = new();
    private static ResourceDictionary _resources = null!;
    private static string _setting = "dark";

    public static bool IsLight { get; private set; }

    public static event Action? Changed;

    /// <summary>Crea los pinceles (una vez, antes de cargar ninguna vista).</summary>
    public static void Initialize(ResourceDictionary resources)
    {
        _resources = resources;
        foreach (var name in Tokens.Current.Dark.Keys.Concat(Extra.Keys))
        {
            var brush = new SolidColorBrush();
            Brushes[name] = brush;
            resources[Key(name) + "Brush"] = brush;
        }
        // Campos de texto: el texto sigue la paleta en todos sus estados.
        foreach (var key in new[] { "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused" })
            resources[key] = Brushes["primary"];
        resources["TextControlPlaceholderForeground"] = Brushes["tertiary"];
        resources["TextControlPlaceholderForegroundPointerOver"] = Brushes["tertiary"];
        resources["TextControlPlaceholderForegroundFocused"] = Brushes["tertiary"];

        // "auto": el cambio de modo de Windows llega en otro hilo.
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        UiSettings.ColorValuesChanged += (_, _) => dispatcher.TryEnqueue(() =>
        {
            if (_setting == "auto") Apply(_setting);
        });
    }

    /// <param name="setting">Ajuste <c>theme</c>: dark, light o auto (sigue el modo de apps de Windows).</param>
    public static void Apply(string setting)
    {
        _setting = setting;
        var light = setting == "light" || (setting == "auto" && SystemIsLight());
        var palette = light ? Tokens.Current.Light : Tokens.Current.Dark;
        foreach (var (name, brush) in Brushes)
        {
            var color = palette.TryGetValue(name, out var c) ? c : light ? Extra[name].Light : Extra[name].Dark;
            brush.Color = color;
            _resources[Key(name) + "Color"] = color;
        }
        var changed = light != IsLight;
        IsLight = light;
        if (changed || Changed is not null) Changed?.Invoke();
    }

    public static Color Get(string name) => Brushes[name].Color;

    public static SolidColorBrush Brush(string name) => Brushes[name];

    /// <summary>Fondo de las apps de Windows claro → tema claro.</summary>
    private static bool SystemIsLight()
    {
        var background = UiSettings.GetColorValue(UIColorType.Background);
        return background.R + background.G + background.B > 382;
    }

    private static string Key(string name) => char.ToUpperInvariant(name[0]) + name[1..];
}
