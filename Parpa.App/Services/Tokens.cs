using System.Numerics;
using System.Text.Json.Nodes;
using Windows.UI;

namespace Parpa.Services;

/// <summary>Tokens de diseño leídos de Data/tokens.json (colores, radios, medidas y movimiento).</summary>
public sealed class Tokens
{
    public static Tokens Current { get; private set; } = null!;

    private readonly Dictionary<string, Color> _dark = new();
    private readonly Dictionary<string, Color> _light = new();
    private readonly Dictionary<string, double> _radius = new();
    private readonly Dictionary<string, (double W, double H)> _notch = new();

    public IReadOnlyDictionary<string, Color> Dark => _dark;
    public IReadOnlyDictionary<string, Color> Light => _light;

    public IReadOnlyList<Color> Accents { get; }
    public Color Success { get; }

    public Motion ShapeMotion { get; }
    public Motion ReducedMotion { get; }
    public int OpenDelayMs { get; }
    public int CloseDelayMs { get; }
    public int OsdVisibleMs { get; }

    private Tokens(JsonNode root)
    {
        foreach (var (name, value) in root["color"]!["dark"]!.AsObject()) _dark[name] = ParseHex(value!.GetValue<string>());
        foreach (var (name, value) in root["color"]!["light"]!.AsObject()) _light[name] = ParseHex(value!.GetValue<string>());
        foreach (var (name, value) in root["radius"]!.AsObject()) _radius[name] = value!.GetValue<double>();
        Accents = root["color"]!["accent"]!.AsArray().Select(a => ParseOklch(a!["oklch"]!.GetValue<string>())).ToList();
        Success = ParseOklch(root["color"]!["success"]!.GetValue<string>());

        // "minimal" es una fórmula (max(96, 0.6*compactW)); se resuelve en el ViewModel.
        foreach (var (name, value) in root["notch"]!.AsObject())
        {
            var size = value!.AsArray();
            if (size[0] is JsonValue w && w.TryGetValue(out double width))
                _notch[name] = (width, size[1]!.GetValue<double>());
        }

        var motion = root["motion"]!;
        ShapeMotion = Motion.Parse(motion["shape"]!);
        ReducedMotion = Motion.Parse(motion["reduced"]!);
        OpenDelayMs = motion["openDelayMs"]!.GetValue<int>();
        CloseDelayMs = motion["closeDelayMs"]!.GetValue<int>();
        OsdVisibleMs = motion["osdVisibleMs"]!.GetValue<int>();
    }

    /// <summary>Acento elegido en Ajustes (0 = rojo); solo para estados vivos.</summary>
    public Color Accent(int index) => Accents[Math.Clamp(index, 0, Accents.Count - 1)];

    public static void Load() => Current = new Tokens(DataFiles.ReadData("tokens.json"));

    public double Radius(string name) => _radius[name];

    public (double W, double H) NotchSize(string name) => _notch[name];

    public static Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        byte a = 255;
        if (hex.Length == 8) { a = Convert.ToByte(hex[..2], 16); hex = hex[2..]; }
        return Color.FromArgb(a, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));
    }

    /// <summary><c>oklch(L C h)</c> → sRGB (OKLab de Björn Ottosson), recortado a gama.</summary>
    public static Color ParseOklch(string css)
    {
        var args = css[(css.IndexOf('(') + 1)..css.IndexOf(')')]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        double l = args[0], c = args[1], h = args[2] * Math.PI / 180;
        double a = c * Math.Cos(h), b = c * Math.Sin(h);

        var l_ = Math.Pow(l + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m_ = Math.Pow(l - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s_ = Math.Pow(l - 0.0894841775 * a - 1.2914855480 * b, 3);

        static byte Encode(double linear)
        {
            linear = Math.Clamp(linear, 0, 1);
            var srgb = linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
            return (byte)Math.Round(srgb * 255);
        }

        return Color.FromArgb(255,
            Encode(4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_),
            Encode(-1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_),
            Encode(-0.0041960863 * l_ - 0.7034186147 * m_ + 1.7076147010 * s_));
    }

    /// <summary>Duración + curva cubic-bezier (CSS) de una animación.</summary>
    public readonly record struct Motion(TimeSpan Duration, Vector2 P1, Vector2 P2)
    {
        public static Motion Parse(JsonNode node)
        {
            var ms = node["ms"]!.GetValue<double>();
            var easing = node["easing"]!.GetValue<string>().Trim();
            var (p1, p2) = easing switch
            {
                "ease" => (new Vector2(.25f, .1f), new Vector2(.25f, 1f)),
                "linear" => (new Vector2(0f, 0f), new Vector2(1f, 1f)),
                "ease-out" => (new Vector2(0f, 0f), new Vector2(.58f, 1f)),
                "ease-in" => (new Vector2(.42f, 0f), new Vector2(1f, 1f)),
                "ease-in-out" => (new Vector2(.42f, 0f), new Vector2(.58f, 1f)),
                _ => ParseCubicBezier(easing),
            };
            return new Motion(TimeSpan.FromMilliseconds(ms), p1, p2);
        }

        private static (Vector2, Vector2) ParseCubicBezier(string css)
        {
            var args = css[(css.IndexOf('(') + 1)..css.IndexOf(')')]
                .Split(',')
                .Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            return (new Vector2(args[0], args[1]), new Vector2(args[2], args[3]));
        }
    }
}
