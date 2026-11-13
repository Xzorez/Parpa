using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Parpa.Controls;
using Parpa.Services;
using Parpa.ViewModels;
using Windows.UI;

namespace Parpa.Views;

/// <summary>
/// Menú estilo Akira (área de 96 px): casillas con borde rosa de 1,5 px (Inicio lleva la mascota) que se
/// rellenan al pasar el ratón; debajo, el nombre en letra ancha, una raya y una cadena de círculos.
/// Entran con escala 0,6→1 + opacidad, cada 25 ms.
/// </summary>
public sealed partial class LauncherView : Grid, INotchPageView
{
    private const double AreaHeight = 96;
    private const double TileSize = 50;
    private const double TileGap = 8;
    private const float HoverScale = 1.08f;

    private readonly NotchServices _app;
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = TileGap };
    private readonly TextBlock _label;
    private DotMatrix _mascot = NewMascot();
    private readonly List<Tile> _tiles = new();
    private string? _hover;

    private sealed record Tile(NotchPage Page, Grid Root, Border Face, DotMatrix Glyph, int Look);

    public LauncherView(NotchServices app)
    {
        _app = app;
        Height = AreaHeight;

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

        _row.HorizontalAlignment = HorizontalAlignment.Center;
        _row.VerticalAlignment = VerticalAlignment.Center;
        _row.PointerExited += (_, _) => SetHover(null);
        Children.Add(_row);

        // Pie: nombre ancho · raya · cadena de cuatro círculos.
        _label = new TextBlock { Style = Res<Style>("Wide"), FontSize = 22, VerticalAlignment = VerticalAlignment.Center };
        var footer = new Grid { ColumnSpacing = 12, IsHitTestVisible = false };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var rule = new Border { Height = 1.5, Background = Res<Brush>("PrimaryBrush"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(rule, 1);
        var chain = Chain();
        Grid.SetColumn(chain, 2);
        footer.Children.Add(_label);
        footer.Children.Add(rule);
        footer.Children.Add(chain);
        Grid.SetRow(footer, 1);
        Children.Add(footer);
    }

    /// <summary>Adorno del cartel: cuatro círculos unidos en zigzag.</summary>
    private static FrameworkElement Chain()
    {
        var canvas = new Canvas { Width = 54, Height = 22, VerticalAlignment = VerticalAlignment.Center };
        var line = new Polyline { Stroke = Res<Brush>("PrimaryBrush"), StrokeThickness = 4 };
        foreach (var (x, y) in new[] { (6.0, 6.0), (20.0, 16.0), (34.0, 6.0), (48.0, 16.0) })
        {
            line.Points.Add(new Windows.Foundation.Point(x, y));
            var dot = new Ellipse { Width = 10, Height = 10, Fill = Res<Brush>("PrimaryBrush") };
            Canvas.SetLeft(dot, x - 5);
            Canvas.SetTop(dot, y - 5);
            canvas.Children.Add(dot);
        }
        canvas.Children.Insert(0, line);
        return canvas;
    }

    private static DotMatrix NewMascot() => new()
    {
        Columns = 13, Rows = 5, Merge = DotMerge.Blobs, DotSize = 2, Gap = 1, OnBrush = Theme.Brush("primary"),
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    public void OnShown()
    {
        _hover = null;
        Build();
        AnimateIn();
    }

    public void OnHidden() => _hover = null;

    private void Build()
    {
        _row.Children.Clear();
        _tiles.Clear();
        // Mascota nueva en cada apertura: mover la misma DotMatrix de una baldosa a otra hace fallar a XAML.
        _mascot = NewMascot();

        var pages = _app.Vm.Pages.ToList();
        if (!pages.Contains(NotchPage.Home)) pages.Insert(0, NotchPage.Home);
        for (var i = 0; i < pages.Count; i++)
        {
            // La mascota mira hacia la baldosa: −2 (izquierda) … 2 (derecha).
            var look = pages.Count < 2 ? 0 : (int)Math.Round(-2 + 4.0 * i / (pages.Count - 1));
            var tile = CreateTile(pages[i], look);
            _row.Children.Add(tile.Root);
            _tiles.Add(tile);
        }
        UpdateHover();
    }

    private Tile CreateTile(NotchPage page, int look)
    {
        var home = page == NotchPage.Home;
        var face = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = Res<Brush>("BgBrush"),
            BorderThickness = new Thickness(1.5),
            BorderBrush = Res<Brush>("PrimaryBrush"),
        };
        var root = new Grid { Width = TileSize, Height = TileSize };
        root.Children.Add(face);
        var glyph = home
            ? _mascot
            : new DotMatrix
            {
                Glyph = page.Glyph, DotSize = 3, Gap = 1.5, OnBrush = Theme.Brush("primary"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
        root.Children.Add(glyph);

        // Insignia: círculo rosa de 18 px con borde negro, esquina superior derecha (−6, −6).
        var count = page.Id switch
        {
            "clip" => _app.Clipboard.Items.Count,
            "shelf" => _app.Shelf.Items.Count,
            "todo" => _app.Tasks.Pending,
            "notes" => _app.Notes.Items.Count,
            _ => 0,
        };
        if (count > 0)
            root.Children.Add(new Border
            {
                MinWidth = 18,
                Height = 18,
                Padding = new Thickness(4, 0, 4, 0),
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(2),
                BorderBrush = Res<Brush>("BgBrush"),
                Background = Res<Brush>("PrimaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -6, -6, 0),
                Child = new TextBlock
                {
                    Text = count.ToString(),
                    FontFamily = Res<FontFamily>("FontWide"),
                    FontStretch = Windows.UI.Text.FontStretch.Expanded,
                    FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.Black,
                    Foreground = Res<Brush>("BgBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });

        Motion.EnableImplicit(root, TimeSpan.FromMilliseconds(220));
        root.PointerEntered += (_, _) => { SoundService.Play("tick"); SetHover(page.Id); };
        root.Tapped += (_, e) => { e.Handled = true; _app.Vm.ShowPage(page); };
        return new Tile(page, root, face, glyph, look);
    }

    /// <summary>Las baldosas aparecen de izquierda a derecha (escala 0,6→1 + opacidad, 300 ms, cada 25 ms).</summary>
    private void AnimateIn()
    {
        foreach (var tile in _tiles) Grow(tile.Root, 1f);
        if (Motion.Reduced) return;
        for (var i = 0; i < _tiles.Count; i++)
        {
            var visual = ElementCompositionPreview.GetElementVisual(_tiles[i].Root);
            var c = visual.Compositor;
            var delay = TimeSpan.FromMilliseconds(25 * i + 30);

            var scale = c.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0f, new Vector3(0.6f, 0.6f, 1));
            scale.InsertKeyFrame(1f, Vector3.One, Motion.Spring(c));
            scale.Duration = TimeSpan.FromMilliseconds(300);
            scale.DelayTime = delay;
            scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            visual.StartAnimation("Scale", scale);

            var fade = c.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, Motion.EaseOut(c));
            fade.Duration = TimeSpan.FromMilliseconds(220);
            fade.DelayTime = delay;
            fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            visual.StartAnimation("Opacity", fade);
        }
    }

    /// <summary>Escala desde el centro de la casilla.</summary>
    private static void Grow(FrameworkElement element, float scale)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3((float)TileSize / 2, (float)TileSize / 2, 0);
        visual.Scale = new Vector3(scale, scale, 1);
    }

    private void SetHover(string? id)
    {
        _hover = id;
        UpdateHover();
    }

    private void UpdateHover()
    {
        var index = _tiles.FindIndex(t => t.Page.Id == _hover);
        for (var i = 0; i < _tiles.Count; i++)
        {
            var on = i == index;
            Grow(_tiles[i].Root, on ? HoverScale : 1f);
            Motion.SetLift(_tiles[i].Root, on ? -2 : 0);
            // Casilla elegida: rellena de rosa y el glifo en negro.
            _tiles[i].Face.Background = on ? Res<Brush>("PrimaryBrush") : Res<Brush>("BgBrush");
            _tiles[i].Glyph.OnBrush = on ? Theme.Brush("bg") : Theme.Brush("primary");
        }

        // El pie dice qué se abre; sin ratón encima, Inicio.
        var named = index >= 0 ? _tiles[index].Page : NotchPage.Home;
        _label.Text = (named == NotchPage.Home ? Loc.Get("windows_home") : Loc.Get(named.TitleKey)).ToUpper(Loc.Culture);

        var home = _hover == "home";
        if (!_app.Settings.Mascot) _mascot.Cells = MascotFrames.Block();
        else if (home) _mascot.Cells = MascotFrames.HappyStill();
        else if (index >= 0) _mascot.Cells = MascotFrames.Looking(_tiles[index].Look, 0);
        else _mascot.Cells = MascotFrames.Awake();
    }
}
