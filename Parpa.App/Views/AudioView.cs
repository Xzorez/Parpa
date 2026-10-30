using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;

namespace Parpa.Views;

/// <summary>
/// Ventana Audio: arriba SALIDA (altavoces, auriculares… como segmentos) y el MICRÓFONO (tocar = silenciar);
/// en medio el VOLUMEN general; abajo una fila por app con su volumen (tocar el nombre = silenciarla).
/// Las barras (<see cref="BarSlider"/>) se pulsan, se arrastran o se mueven con la rueda (pasos de 5 %).
/// </summary>
public sealed partial class AudioView : Grid, INotchPageView
{
    private const int Bars = 20; // una barra = 5 %
    private const int MaxOutputs = 4;

    private readonly NotchServices _app;
    private readonly AudioService _audio;
    private readonly Grid _top = new() { ColumnSpacing = 8 };
    private readonly Border _master;
    private readonly BarSlider _masterBars = new(Bars, 5, 5, 14);
    private readonly TextBlock _masterValue = Value();
    private readonly TextBlock _masterLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _list;
    private readonly DispatcherQueueTimer _poll;
    private string _appsKey = string.Empty;
    private readonly List<BarSlider> _appSliders = new();
    private bool _shown;

    public AudioView(NotchServices app)
    {
        _app = app;
        _audio = app.System.Audio;
        RowSpacing = 8;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        Children.Add(_top);

        // VOLUMEN + barras + valor (como el de sonidos en Ajustes rápidos).
        _masterLabel.Style = Res<Style>("CardLabel");
        var master = new Grid { ColumnSpacing = 12 };
        master.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        master.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        master.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_masterBars, 1);
        Grid.SetColumn(_masterValue, 2);
        master.Children.Add(_masterLabel);
        master.Children.Add(_masterBars);
        master.Children.Add(_masterValue);
        _master = new Border { Padding = new Thickness(12, 10, 12, 10), Background = Res<Brush>("CardBrush"), CornerRadius = new CornerRadius(14), Child = master };
        Grid.SetRow(_master, 1);
        Children.Add(_master);
        _masterBars.ValueChanged += percent =>
        {
            _audio.SetVolume(percent);
            _masterValue.Text = $"{percent}%";
        };
        _master.PointerWheelChanged += (_, e) => _masterBars.Wheel(e);

        _list = new ScrollViewer { HorizontalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = _rows };
        Grid.SetRow(_list, 2);
        Children.Add(_list);

        // Las apps aparecen y desaparecen sin aviso de Windows: con la ventana a la vista se mira cada 1,5 s.
        _poll = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _poll.Interval = TimeSpan.FromSeconds(1.5);
        _poll.Tick += (_, _) => RenderApps(force: false);

        _audio.VolumeChanged += (_, _) => { if (_shown) RenderMaster(); };
        _audio.DevicesChanged += () => { if (_shown) Render(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    public IEnumerable<UIElement> EntranceItems => _top.Children.Append(_master).Concat(_rows.Children);

    public void OnShown()
    {
        _shown = true;
        _masterLabel.Text = Loc.Get("audio_volume");
        Render();
        _list.ChangeView(null, 0, null, disableAnimation: true);
        _poll.Start();
    }

    public void OnHidden()
    {
        _shown = false;
        _poll.Stop();
    }

    private void Render()
    {
        RenderTop();
        RenderMaster();
        RenderApps(force: true);
    }

    // ---- SALIDA y MICRÓFONO ----

    private void RenderTop()
    {
        _top.Children.Clear();

        var outputs = _audio.Outputs().Take(MaxOutputs).ToList();
        var current = outputs.FirstOrDefault(o => o.IsDefault)?.Id ?? string.Empty;
        var output = new StackPanel { Spacing = 8 };
        output.Children.Add(new TextBlock { Style = Res<Style>("CardLabel"), Text = Loc.Get("audio_output") });
        output.Children.Add(outputs.Count == 0
            ? Ui.Mono(Loc.Get("audio_no_output"), 10)
            : Ui.Segmented(outputs.Select(o => (o.Id, ShortName(o.Name))), current, id => _audio.SetDefaultOutput(id)));
        var outputCard = Card(output);
        _top.Children.Add(outputCard);

        var mic = _audio.Microphone();
        var micCard = MicTile(mic);
        Grid.SetColumn(micCard, 1);
        _top.Children.Add(micCard);
    }

    /// <summary>Como un mosaico de Ajustes rápidos: claro con el micro activo, apagado si está silenciado.</summary>
    private Border MicTile((string Name, bool Muted)? mic)
    {
        var on = mic is { Muted: false };
        var fg = Theme.Brush(on ? "bg" : "primary");
        var glyph = new DotMatrix { Glyph = "micro", OnColor = fg.Color, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var status = new TextBlock
        {
            Text = mic is null ? "—" : Loc.Get(on ? "audio_mic_on" : "audio_mic_off"),
            FontFamily = Res<FontFamily>("FontMono"),
            FontSize = 9,
            CharacterSpacing = 80,
            Foreground = fg,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var label = new TextBlock
        {
            Text = Loc.Get("audio_mic"),
            FontFamily = Res<FontFamily>("FontUi"),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = fg,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 22, 0, 0),
        };
        if (mic is { } m) ToolTipService.SetToolTip(label, m.Name);
        var content = new Grid();
        content.Children.Add(glyph);
        content.Children.Add(status);
        content.Children.Add(label);

        var tile = Motion.SmoothBackground(new Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(14),
            Background = on ? Theme.Brush("primary") : Res<Brush>("CardBrush"),
            Opacity = mic is null ? 0.45 : 1,
            Child = content,
        });
        Motion.PressFeedback(tile);
        tile.Tapped += (_, e) =>
        {
            e.Handled = true;
            if (mic is not { } current) return;
            SoundService.Play("click");
            _audio.SetMicrophoneMuted(!current.Muted);
            Motion.AfterTransition(RenderTop);
        };
        return tile;
    }

    /// <summary>
    /// "Altavoces (Logitech G733 Gaming Headset)" → "LOGITECH G733 GAMING…": Windows llama "Altavoces" a casi todas,
    /// así que lo que las distingue es lo de dentro del paréntesis.
    /// </summary>
    private static string ShortName(string name)
    {
        var open = name.IndexOf(" (", StringComparison.Ordinal);
        var shortName = open > 0 && name.EndsWith(')') ? name[(open + 2)..^1] : name;
        return Loc.Upper(shortName.Length > 22 ? shortName[..21].TrimEnd() + "…" : shortName);
    }

    // ---- VOLUMEN ----

    private void RenderMaster()
    {
        if (_masterBars.IsDragging) return; // lo que llega de Windows va por detrás del ratón
        var volume = _audio.Volume;
        _masterBars.Show(volume);
        _masterValue.Text = $"{volume}%";
    }

    // ---- Apps ----

    private void RenderApps(bool force)
    {
        var apps = _audio.Apps();
        var key = string.Join("|", apps.Select(a => $"{a.Name}:{a.Volume}:{a.Muted}:{a.ProcessIds.Count}"));
        if (!force && (key == _appsKey || _appSliders.Any(s => s.IsDragging))) return;
        _appsKey = key;

        _rows.Children.Clear();
        _appSliders.Clear();
        if (apps.Count == 0)
        {
            _rows.Children.Add(new Border
            {
                Padding = new Thickness(4, 12, 4, 0),
                BorderBrush = Res<Brush>("DividerBrush"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = Ui.Mono(Loc.Get("audio_no_apps"), 10, "soft", 80, medium: true),
            });
            return;
        }
        foreach (var app in apps) _rows.Children.Add(AppRow(app));
    }

    private Grid AppRow(AppVolume app)
    {
        var row = new Grid
        {
            Padding = new Thickness(4, 8, 4, 8),
            BorderBrush = Res<Brush>("DividerBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            ColumnSpacing = 14,
            Background = Ui.Clear,
            Opacity = app.Muted ? 0.5 : 1,
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });

        var name = new TextBlock { Style = Res<Style>("RowText"), Text = app.Name };
        var value = new TextBlock
        {
            Style = Res<Style>("RowMeta"),
            Text = app.Muted ? Loc.Get("audio_muted") : $"{app.Volume}%",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTipService.SetToolTip(name, Loc.Get(app.Muted ? "audio_unmute" : "audio_mute"));
        name.Tapped += (_, e) =>
        {
            e.Handled = true;
            SoundService.Play("click");
            _audio.SetAppMuted(app, !app.Muted);
            RenderApps(force: false);
        };

        var bars = new BarSlider(Bars, 4, 4, 10, spacing: 2);
        bars.Show(app.Muted ? 0 : app.Volume);
        bars.ValueChanged += percent =>
        {
            _audio.SetAppVolume(app, percent);
            value.Text = $"{percent}%";
            row.Opacity = 1;
        };
        _appSliders.Add(bars);
        row.PointerWheelChanged += (_, e) => bars.Wheel(e);

        Grid.SetColumn(bars, 1);
        Grid.SetColumn(value, 2);
        row.Children.Add(name);
        row.Children.Add(bars);
        row.Children.Add(value);
        return row;
    }

    private static Border Card(UIElement child) => new()
    {
        Padding = new Thickness(12, 10, 12, 10),
        Background = Res<Brush>("CardBrush"),
        CornerRadius = new CornerRadius(14),
        Child = child,
    };

    private static TextBlock Value() => new()
    {
        FontFamily = Res<FontFamily>("FontDisplay"),
        FontSize = 13,
        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        Foreground = Theme.Brush("primary"),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
