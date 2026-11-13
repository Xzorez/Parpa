using System.Net.Http;
using System.Text.Json.Nodes;
using Microsoft.UI.Dispatching;
using Windows.Devices.Geolocation;

namespace Parpa.Services;

/// <summary>Tiempo actual: temperatura redondeada y glifo de puntos (sol, nube, lluvia…).</summary>
public sealed record WeatherNow(int Temperature, string Glyph, bool Day);

/// <summary>
/// Tiempo del sitio donde está el PC. La ubicación sale de Windows (Ajustes › Privacidad › Ubicación, "Permitir que
/// las aplicaciones de escritorio accedan a la ubicación") y se redondea a 0,1° (unos 10 km) antes de preguntar a
/// Open-Meteo, que no pide cuenta ni clave. Se refresca cada 30 min y solo mientras alguien mira el Inicio.
/// Sin permiso o sin conexión: <see cref="Current"/> queda en null y la píldora no sale.
/// </summary>
public sealed class WeatherService
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RetryEvery = TimeSpan.FromMinutes(5);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private DateTime _nextFetch = DateTime.MinValue;
    private bool _busy;

    public WeatherNow? Current { get; private set; }

    /// <summary>Hay datos nuevos (en el hilo de la interfaz).</summary>
    public event Action? Changed;

    /// <summary>Pide el tiempo si toca (llamado al abrir el Inicio).</summary>
    public void Refresh()
    {
        if (_busy || DateTime.UtcNow < _nextFetch) return;
        _busy = true;
        _ = FetchAsync();
    }

    private async Task FetchAsync()
    {
        try
        {
            var (lat, lon) = await LocateAsync();
            var url = FormattableString.Invariant(
                $"https://api.open-meteo.com/v1/forecast?latitude={lat:0.0}&longitude={lon:0.0}&current=temperature_2m,weather_code,is_day");
            var json = JsonNode.Parse(await Http.GetStringAsync(url))!["current"]!;
            var day = json["is_day"]!.GetValue<int>() == 1;
            var now = new WeatherNow(
                (int)Math.Round(json["temperature_2m"]!.GetValue<double>()),
                GlyphFor(json["weather_code"]!.GetValue<int>(), day),
                day);
            _nextFetch = DateTime.UtcNow + RefreshEvery;
            _dispatcher.TryEnqueue(() =>
            {
                Current = now;
                Changed?.Invoke();
            });
        }
        catch (Exception e)
        {
            Log.Info($"Tiempo: {e.GetType().Name} {e.Message}");
            _nextFetch = DateTime.UtcNow + RetryEvery;
        }
        finally
        {
            _busy = false;
        }
    }

    private static async Task<(double Lat, double Lon)> LocateAsync()
    {
        var access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed) throw new UnauthorizedAccessException($"ubicación: {access}");
        var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default, DesiredAccuracyInMeters = 5000 };
        var position = await locator.GetGeopositionAsync(TimeSpan.FromHours(3), TimeSpan.FromSeconds(10));
        var point = position.Coordinate.Point.Position;
        return (Math.Round(point.Latitude, 1), Math.Round(point.Longitude, 1));
    }

    /// <summary>Códigos WMO de Open-Meteo → glifo de puntos de glyphs.json.</summary>
    private static string GlyphFor(int code, bool day) => code switch
    {
        0 or 1 => day ? "sol" : "noche",
        2 => day ? "solnube" : "nube",
        3 => "nube",
        45 or 48 => "niebla",
        >= 51 and <= 67 or >= 80 and <= 82 => "lluvia",
        >= 71 and <= 77 or 85 or 86 => "nieve",
        >= 95 => "tormenta",
        _ => "nube",
    };
}
