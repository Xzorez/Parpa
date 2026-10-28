using Parpa.ViewModels;

namespace Parpa.Services;

/// <summary>
/// Traduce los eventos del sistema a lo que muestra el notch: música → actividad;
/// volumen, brillo, batería, Wi-Fi y auriculares → indicador (si están activados en Ajustes).
/// </summary>
public sealed class SystemMonitor : IDisposable
{
    private readonly NotchViewModel _vm;
    private readonly ParpaSettings _s;
    private readonly MediaService _media = new();
    private readonly AudioService _audio = new();
    private readonly BrightnessService _brightness = new();
    private readonly PowerService _power = new();
    private readonly NetworkService _network = new();

    public SystemMonitor(NotchViewModel vm, ParpaSettings settings)
    {
        _vm = vm;
        _s = settings;
    }

    public MediaService Media => _media;

    /// <summary>Filas de la tarjeta SISTEMA: batería (null sin batería), volumen y señal.</summary>
    public (int? Battery, int Volume, int Wifi) Snapshot() =>
        (PowerService.HasBattery ? PowerService.Percent : null, _audio.Volume, _network.SignalPercent);

    /// <summary>Cambió algo de la tarjeta SISTEMA (volumen, red, batería).</summary>
    public event Action? SystemChanged;

    public void Start()
    {
        _media.Changed += OnMediaChanged;
        _ = _media.StartAsync();

        _audio.VolumeChanged += OnVolumeChanged;
        _audio.HeadphonesConnected += OnHeadphones;
        try
        {
            _audio.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Audio: no se pudo iniciar", ex);
        }

        _brightness.Changed += OnBrightnessChanged;
        _brightness.Start();

        _power.ChargerConnected += OnChargerConnected;
        _power.BatteryLow += OnBatteryLow;
        _power.Start();

        _network.Changed += OnNetworkChanged;
        _network.Start();
    }

    private void OnMediaChanged()
    {
        var now = _media.Current;
        _vm.SetActivity(NotchViewModel.ActivityMusic, now is null ? null : new NotchActivity(
            SizeToken: "music",
            Glyph: "musica",
            Label: Loc.Upper(now.Title.Length > 0 ? now.Title : Loc.Get("home_music")),
            Sub: Loc.Upper(now.Artist),
            IsPlaying: true));
    }

    private void OnVolumeChanged(int percent, bool muted)
    {
        SystemChanged?.Invoke();
        if (!_s.Osd) return;
        var level = muted ? 0 : percent;
        _vm.ShowIndicator(new NotchIndicator(
            Glyph: "sonido",
            Label: Loc.Get("osd_volume"),
            Sub: Loc.Get(_audio.IsHeadphones ? "osd_headphones" : "osd_speakers"),
            Level: level,
            Value: $"{level}%"));
    }

    private void OnBrightnessChanged(int percent)
    {
        if (!_s.Osd) return;
        _vm.ShowIndicator(new NotchIndicator(
            Glyph: "brillo",
            Label: Loc.Get("osd_brightness"),
            Sub: Loc.Format("osd_display_n", ("n", 1)),
            Level: percent,
            Value: $"{percent}%"));
    }

    private void OnChargerConnected(int percent)
    {
        if (!_s.Battery) return;
        _vm.ShowIndicator(new NotchIndicator(
            Glyph: "bateria",
            Label: Loc.Get("osd_charging"),
            Sub: Loc.Get("osd_charger_in"),
            Level: percent,
            Value: $"{percent}%",
            Success: true));
    }

    private void OnBatteryLow(int percent)
    {
        if (!_s.Battery) return;
        _vm.ShowIndicator(new NotchIndicator(
            Glyph: "bateria",
            Label: Loc.Get("osd_battery_low"),
            Sub: Loc.Format("osd_battery_left", ("pct", $"{percent}%")),
            Level: percent,
            Value: $"{percent}%",
            Alert: true));
    }

    private void OnNetworkChanged(bool online)
    {
        SystemChanged?.Invoke();
        if (!_s.Wifi) return;
        var name = Loc.Upper(_network.NetworkName);
        var sub = _network.IsWifi ? $"{Loc.Upper(Loc.Get("quick_wifi"))} · {name}" : name;
        _vm.ShowIndicator(new NotchIndicator(
            Glyph: "wifi",
            Label: Loc.Get(online ? "osd_online" : "osd_offline"),
            Sub: sub,
            Level: online ? 100 : 0,
            Value: online ? "OK" : "—",
            Alert: !online));
    }

    private void OnHeadphones(string deviceName)
    {
        if (!_s.Buds) return;
        _vm.ShowIndicator(new NotchIndicator(
            Glyph: "cascos",
            Label: Loc.Get("osd_headphones"),
            Sub: Loc.Upper(deviceName),
            Level: 100,
            Value: "OK"));
    }

    public void Dispose()
    {
        _audio.Dispose();
        _brightness.Dispose();
    }
}
