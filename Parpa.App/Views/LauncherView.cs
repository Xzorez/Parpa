using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;
using Parpa.ViewModels;
using Windows.UI;

namespace Parpa.Views;

/// <summary>
/// Menú en forma de Dock (área de 96 px): una barra gris redondeada con Inicio (la mascota) y una baldosa
/// de color por ventana. Al pasar el ratón la baldosa crece desde abajo, las vecinas un poco, y el nombre
/// aparece encima. Entran con escala 0,6→1 + opacidad, cada 25 ms.
/// </summary>
public sealed partial class LauncherView : Grid, INotchPageView
{
    private const double AreaHeight = 96;
    private const double TileSize = 48;
    private const double TileGap = 8;
    private const double DockPadding = 8;
    private const float HoverScale = 1.22f;
    private const float NeighbourScale = 1.08f;

    private readonly NotchServices _app;
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = TileGap };
    private readonly Border _dock;
    private readonly TextBlock _label;
    private readonly TranslateTransform _labelShift = new();
    private DotMatrix _mascot = NewMascot();
    private readonly List<Tile> _tiles = new();
    private string? _hover;

    private sealed record Tile(NotchPage Page, Grid Root, Border Face, int Look);

    public LauncherView(NotchServices app)
    {
        _app = app;
        Height = AreaHeight;

        _label = new TextBlock
        {
            FontFamily = Res<FontFamily>("FontUi"),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Res<Brush>("PrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Opacity = 0,
            IsHitTestVisible = false,
            OpacityTransition = Motion.Reduced ? null : new ScalarTransition { Duration = Motion.Quick },
        };
        var labelBox = new Border
        {
            Child = _label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            RenderTransform = _labelShift,
        };

        _dock = new Border
        {
            Background = Res<Brush>("CardBrush"),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(DockPadding),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = _row,
        };
        _dock.PointerExited += (_, _) => SetHover(null);

        Children.Add(_dock);
        Children.Add(labelBox);
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
            CornerRadius = new CornerRadius(15),
            Background = home ? Res<Brush>("BgBrush") : new SolidColorBrush(Ui.PageColor(page.Id)),
            BorderThickness = new Thickness(home ? 1 : 0),
            BorderBrush = Res<Brush>("BorderBrush"),
        };
        var root = new Grid { Width = TileSize, Height = TileSize };
        root.Children.Add(face);
        if (home) root.Children.Add(_mascot);
        else
            root.Children.Add(new DotMatrix
            {
                Glyph = page.Glyph, DotSize = 3, Gap = 1.5, OnColor = Ui.PageInk(page.Id),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            });

        // Insignia: 18 px de alto, radio 9, esquina superior derecha (−4, −4).
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
                BorderBrush = Res<Brush>("CardBrush"),
                Background = new SolidColorBrush(Color.FromArgb(255, 0xff, 0x45, 0x3a)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -4, -4, 0),
                Child = new TextBlock
                {
                    Text = count.ToString(),
                    FontFamily = Res<FontFamily>("FontUi"),
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });

        Motion.EnableImplicit(root, TimeSpan.FromMilliseconds(220));
        root.PointerEntered += (_, _) => { SoundService.Play("tick"); SetHover(page.Id); };
        root.Tapped += (_, e) => { e.Handled = true; _app.Vm.ShowPage(page); };
        return new Tile(page, root, face, look);
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

    /// <summary>Escala desde el borde de abajo, como los iconos del Dock.</summary>
    private static void Grow(FrameworkElement element, float scale)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3((float)TileSize / 2, (float)TileSize, 0);
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
            var distance = index < 0 ? int.MaxValue : Math.Abs(i - index);
            Grow(_tiles[i].Root, distance == 0 ? HoverScale : distance == 1 ? NeighbourScale : 1f);
            Motion.SetLift(_tiles[i].Root, distance == 0 ? -3 : 0);
        }

        if (index >= 0)
        {
            var tile = _tiles[index];
            _label.Text = tile.Page == NotchPage.Home ? Loc.Get("windows_home") : Loc.Get(tile.Page.TitleKey);
            // El nombre se centra sobre la baldosa (las baldosas van centradas en la barra).
            var rowWidth = _tiles.Count * TileSize + (_tiles.Count - 1) * TileGap;
            _labelShift.X = -rowWidth / 2 + index * (TileSize + TileGap) + TileSize / 2;
            _label.Opacity = 1;
        }
        else _label.Opacity = 0;

        var home = _hover == "home";
        if (!_app.Settings.Mascot) _mascot.Cells = MascotFrames.Block();
        else if (home) _mascot.Cells = MascotFrames.HappyStill();
        else if (index >= 0) _mascot.Cells = MascotFrames.Looking(_tiles[index].Look, 0);
        else _mascot.Cells = MascotFrames.Awake();
    }
}
