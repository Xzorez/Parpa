using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Windows.UI;

namespace Parpa.Services;

/// <summary>
/// Color dominante de una carátula (estilo isla: el ecualizador y el resplandor de Inicio toman su color).
/// Se reduce la imagen a 16×16, se promedian los píxeles pesando los más saturados y se aviva el resultado.
/// </summary>
public static class ArtColor
{
    /// <summary>Carátula leída una sola vez (bytes + color), compartida por el ecualizador e Inicio.</summary>
    public sealed record Art(byte[] Bytes, Color? Color);

    private static object? _lastSource;
    private static Task<Art?>? _lastTask;

    /// <summary>Último color calculado (null si la canción no tiene carátula o no se pudo leer).</summary>
    public static Color? Current { get; private set; }

    /// <summary>Cambia <see cref="Current"/>.</summary>
    public static event Action? Changed;

    public static async Task<Color?> FromAsync(IRandomAccessStreamReference? thumbnail) => (await LoadAsync(thumbnail))?.Color;

    /// <summary>
    /// Lee la carátula (una vez por canción: quien llegue a la vez espera a la misma lectura, porque
    /// abrir el stream de SMTC dos veces seguidas puede fallar) y calcula su color.
    /// </summary>
    public static Task<Art?> LoadAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null)
        {
            Publish(null);
            return Task.FromResult<Art?>(null);
        }
        if (ReferenceEquals(thumbnail, _lastSource) && _lastTask is not null) return _lastTask;
        _lastSource = thumbnail;
        _lastTask = ReadAsync(thumbnail);
        return _lastTask;
    }

    private static async Task<Art?> ReadAsync(IRandomAccessStreamReference thumbnail)
    {
        try
        {
            byte[] bytes;
            using (var stream = await thumbnail.OpenReadAsync())
            using (var reader = new DataReader(stream.GetInputStreamAt(0)))
            {
                var size = (uint)stream.Size;
                await reader.LoadAsync(size);
                bytes = new byte[size];
                reader.ReadBytes(bytes);
            }
            using var memory = new InMemoryRandomAccessStream();
            await memory.WriteAsync(bytes.AsBuffer());
            memory.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(memory);
            var transform = new BitmapTransform { ScaledWidth = 16, ScaledHeight = 16, InterpolationMode = BitmapInterpolationMode.Linear };
            var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
            var color = Dominant(data.DetachPixelData());
            if (ReferenceEquals(thumbnail, _lastSource)) Publish(color);
            return new Art(bytes, color);
        }
        catch (Exception ex)
        {
            Log.Info($"Carátula: no se pudo leer ({ex.GetType().Name}: {ex.Message})");
            if (ReferenceEquals(thumbnail, _lastSource)) Publish(null);
            return null;
        }
    }

    private static void Publish(Color? color)
    {
        if (Current == color) return;
        Current = color;
        Changed?.Invoke();
    }

    private static Color? Dominant(byte[] bgra)
    {
        double r = 0, g = 0, b = 0, total = 0;
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            double pb = bgra[i] / 255.0, pg = bgra[i + 1] / 255.0, pr = bgra[i + 2] / 255.0;
            var max = Math.Max(pr, Math.Max(pg, pb));
            var min = Math.Min(pr, Math.Min(pg, pb));
            var saturation = max <= 0 ? 0 : (max - min) / max;
            // Los píxeles vivos y no muy oscuros mandan; los grises casi no cuentan.
            var weight = 0.05 + saturation * saturation * max;
            r += pr * weight; g += pg * weight; b += pb * weight; total += weight;
        }
        if (total <= 0) return null;
        return Vivid(r / total, g / total, b / total);
    }

    /// <summary>Sube la saturación y el brillo para que se vea sobre negro (HSV: S ≥ 0,55, V ≥ 0,85).</summary>
    private static Color Vivid(double r, double g, double b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        double h = 0;
        if (delta > 0.0001)
        {
            if (max == r) h = (g - b) / delta % 6;
            else if (max == g) h = (b - r) / delta + 2;
            else h = (r - g) / delta + 4;
            h *= 60;
            if (h < 0) h += 360;
        }
        var s = max <= 0 ? 0 : delta / max;
        // Carátulas en blanco y negro: se queda gris claro en vez de inventar un color.
        if (s < 0.12) return Color.FromArgb(255, 0xd1, 0xd1, 0xd6);
        s = Math.Max(s, 0.55);
        var v = Math.Max(max, 0.85);
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (rr, gg, bb) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromArgb(255, (byte)Math.Round((rr + m) * 255), (byte)Math.Round((gg + m) * 255), (byte)Math.Round((bb + m) * 255));
    }

    /// <summary>Resplandor radial del color: intenso en el centro y transparente en los bordes.</summary>
    public static Brush GlowBrush(Color color)
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.55),
            GradientOrigin = new Point(0.5, 0.55),
            RadiusX = 0.5,
            RadiusY = 0.5,
        };
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(190, color.R, color.G, color.B), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(80, color.R, color.G, color.B), Offset = 0.5 });
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, color.R, color.G, color.B), Offset = 1 });
        return brush;
    }
}
