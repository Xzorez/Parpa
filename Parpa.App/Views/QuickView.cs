using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;

namespace Parpa.Views;

/// <summary>
/// Ajustes rápidos (ventana "quick"): 6 mosaicos de 62 px (radio 14, padding 10 12) — No molestar, Sonidos,
/// Mascota, Modo minimal, Ocultar en juegos y Sin quitar foco — y abajo el volumen de sonidos + "TODOS LOS AJUSTES ›".
/// </summary>
public sealed partial class QuickView : Grid, INotchPageView
{
    private const int VolumeBars = 14;

    private readonly NotchServices _app;
    private readonly Grid _tiles = new() { ColumnSpacing = 6, RowSpacing = 6 };
    private readonly StackPanel _bars = new() { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _volumeValue = new();
    private readonly TextBlock _soundsLabel = new();
    private readonly TextBlock _allSettings = new();
    private bool _shown;

    private sealed record Tile(string LabelKey, string Glyph, Func<Task<bool?>> Read, Func<Task> Toggle);

    private readonly Tile[] _tileDefs;

    public QuickView(NotchServices app)
    {
        _app = app;
        RowSpacing = 8;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Los mismos seis que el prototipo (Notch Prototipo.dc.html › tiles).
        _tileDefs =
        [
            Setting("quick_dnd", "luna", s => s.Dnd, (s, v) => s.Dnd = v),
            Setting("quick_sounds", "sonido", s => s.Sounds, (s, v) => s.Sounds = v),
            Setting("quick_mascot", "cara", s => s.Mascot, (s, v) => s.Mascot = v),
            Setting("quick_minimal", "linea", s => s.Minimal, (s, v) => s.Minimal = v),
            Setting("quick_hide_full", "juego", s => s.HideFull, (s, v) => s.HideFull = v),
            Setting("quick_no_focus", "ojo", s => s.NoFocus, (s, v) => s.NoFocus = v),
        ];

        for (var c = 0; c < 3; c++) _tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _tiles.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        _tiles.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        Children.Add(_tiles);

        // Fila inferior: SONIDOS + barras + valor | TODOS LOS AJUSTES ›
        var bottom = new Grid { ColumnSpacing = 8 };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(bottom, 1);
        Children.Add(bottom);

        var sounds = new Grid { Padding = new Thickness(12, 10, 12, 10), Background = Res<Brush>("CardBrush"), CornerRadius = new CornerRadius(14), ColumnSpacing = 12 };
        sounds.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sounds.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sounds.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _soundsLabel.Style = Res<Style>("CardLabel");
        _soundsLabel.VerticalAlignment = VerticalAlignment.Center;
        _volumeValue.FontFamily = Res<FontFamily>("FontDisplay");
        _volumeValue.FontSize = 13;
        _volumeValue.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
        _volumeValue.Foreground = Theme.Brush("primary");
        _volumeValue.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_bars, 1);
        Grid.SetColumn(_volumeValue, 2);
        sounds.Children.Add(_soundsLabel);
        sounds.Children.Add(_bars);
        sounds.Children.Add(_volumeValue);
        bottom.Children.Add(sounds);

        var allSettings = new Border
        {
            Padding = new Thickness(14, 0, 14, 0),
            Background = Theme.Brush("primary"),
            CornerRadius = new CornerRadius(14),
            Child = _allSettings,
        };
        _allSettings.FontFamily = Res<FontFamily>("FontMono");
        _allSettings.FontSize = 10;
        _allSettings.FontWeight = Microsoft.UI.Text.FontWeights.Medium;
        _allSettings.CharacterSpacing = 100;
        _allSettings.Foreground = Theme.Brush("bg");
        _allSettings.VerticalAlignment = VerticalAlignment.Center;
        allSettings.Tapped += (_, e) =>
        {
            e.Handled = true;
            SoundService.Play("page");
            App.Current.OpenSettings();
        };
        Grid.SetColumn(allSettings, 1);
        bottom.Children.Add(allSettings);

        for (var i = 0; i < VolumeBars; i++)
        {
            var index = i;
            var bar = new Border { Width = 5, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center, Background = Theme.Brush("border") };
            bar.Tapped += (_, e) =>
            {
                e.Handled = true;
                // Cada barra fija el volumen en pasos de 5 %.
                Update(s => s.Vol = (int)(Math.Round((index + 1) / (double)VolumeBars * 100 / 5) * 5));
                SoundService.Play("tick");
            };
            _bars.Children.Add(bar);
        }

        SettingsService.Current.Changed += () => { if (_shown) _ = RenderAsync(); };
        app.Focus.Changed += () => { if (_shown) _ = RenderAsync(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    private static Task Update(Action<ParpaSettings> change)
    {
        SettingsService.Current.Update(change);
        return Task.CompletedTask;
    }

    /// <summary>Mosaico que alterna un ajuste booleano.</summary>
    private Tile Setting(string labelKey, string glyph, Func<ParpaSettings, bool> get, Action<ParpaSettings, bool> set) =>
        new(labelKey, glyph, () => Task.FromResult<bool?>(get(_app.Settings)), () => Update(s => set(s, !get(s))));

    public IEnumerable<UIElement> EntranceItems => _tiles.Children.Append(Children[1]);

    public void OnShown()
    {
        _shown = true;
        _soundsLabel.Text = Loc.Get("home_sounds");
        _allSettings.Text = Loc.Get("home_all_settings") + " ›";
        _ = RenderAsync();
    }

    public void OnHidden() => _shown = false;

    private async Task RenderAsync()
    {
        // Volumen de sonidos: barra i llena si (i + 0,5) / 14 ≤ vol; altura 5 → 14 px.
        var fraction = _app.Settings.Vol / 100.0;
        for (var i = 0; i < VolumeBars; i++)
        {
            var bar = (Border)_bars.Children[i];
            var filled = (i + 0.5) / VolumeBars <= fraction + 0.001;
            Motion.AnimateSize(bar, null, filled ? 5 + Math.Round(i / (double)VolumeBars * 9) : 5, TimeSpan.FromMilliseconds(150));
            bar.Background = Theme.Brush(filled ? "primary" : "border");
        }
        _volumeValue.Text = $"{_app.Settings.Vol}%";

        var states = await Task.WhenAll(_tileDefs.Select(t => t.Read()));
        _tiles.Children.Clear();
        for (var i = 0; i < _tileDefs.Length; i++)
        {
            var tile = CreateTile(_tileDefs[i], states[i]);
            Grid.SetRow(tile, i / 3);
            Grid.SetColumn(tile, i % 3);
            _tiles.Children.Add(tile);
        }
    }

    /// <summary>Encendido: fondo claro y texto oscuro. Sin el dispositivo (p. ej. sin Bluetooth): atenuado.</summary>
    private Border CreateTile(Tile def, bool? state)
    {
        var on = state == true;
        var fg = Theme.Brush(on ? "bg" : "primary");
        var glyph = new DotMatrix { Glyph = def.Glyph, OnColor = fg.Color };
        var status = new TextBlock
        {
            Text = state is null ? "—" : Loc.Get(on ? "quick_on" : "quick_off"),
            FontFamily = Res<FontFamily>("FontMono"),
            FontSize = 9,
            CharacterSpacing = 80,
            Foreground = fg,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var top = new Grid();
        top.Children.Add(glyph);
        top.Children.Add(status);
        glyph.HorizontalAlignment = HorizontalAlignment.Left;
        glyph.VerticalAlignment = VerticalAlignment.Center;

        var label = new TextBlock
        {
            Text = Loc.Get(def.LabelKey),
            FontFamily = Res<FontFamily>("FontUi"),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = fg,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var content = new Grid();
        content.Children.Add(top);
        content.Children.Add(label);
        top.VerticalAlignment = VerticalAlignment.Top;

        var tile = Motion.SmoothBackground(new Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(14),
            Background = on ? Theme.Brush("primary") : Res<Brush>("CardBrush"),
            Opacity = state is null ? 0.45 : 1,
            Child = content,
        });
        Motion.PressFeedback(tile);
        tile.Tapped += (_, e) =>
        {
            e.Handled = true;
            if (state is null) return;
            SoundService.Play("click");
            // El mosaico cambia de color con transición y después se aplica el ajuste (que lo redibuja todo).
            var next = !on;
            tile.Background = next ? Theme.Brush("primary") : Res<Brush>("CardBrush");
            var nextFg = Theme.Brush(next ? "bg" : "primary");
            glyph.OnColor = nextFg.Color;
            status.Foreground = label.Foreground = nextFg;
            status.Text = Loc.Get(next ? "quick_on" : "quick_off");
            Motion.AfterTransition(async () =>
            {
                await def.Toggle();
                await RenderAsync();
            });
        };
        return tile;
    }
}
