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
/// Menú de burbujas (área de 192 px). Mascota 108×62 en (50 %, 56 px); hasta 5 burbujas de 54 px
/// en semicírculo (radios 192×80, reparto 2-1-2). Entran con escala 0,6→1 + opacidad, 300 ms, cada 30 ms.
/// </summary>
public sealed partial class LauncherView : Grid, INotchPageView
{
    private const double AreaHeight = 192;
    private const double CenterY = 56;
    private const double RadiusX = 192;
    private const double RadiusY = 80;
    private const double BubbleSize = 54;

    /// <summary>Fondo en hover (prototipo: #1f1f23).</summary>
    private static readonly Color HoverFill = Color.FromArgb(255, 0x1f, 0x1f, 0x23);

    private readonly NotchServices _app;
    private readonly Canvas _canvas = new() { Height = AreaHeight };
    private readonly Border _mascotPill;
    private readonly DotMatrix _mascot;
    private readonly TextBlock _homeLabel;
    private readonly List<Bubble> _bubbles = new();
    private string? _hover;

    private sealed record Bubble(NotchPage Page, Grid Root, Ellipse Circle, DotMatrix Glyph, TextBlock Label, (int Column, int Row) Look);

    public LauncherView(NotchServices app)
    {
        _app = app;
        Height = AreaHeight;
        Children.Add(_canvas);

        _mascot = new DotMatrix { Columns = 13, Rows = 5, DotSize = 4, Gap = 1.5, OnColor = Res<Color>("PrimaryColor"), OffColor = Res<Color>("GlyphOffColor") };
        _mascotPill = new Border
        {
            Width = 108,
            Height = 62,
            CornerRadius = new CornerRadius(31),
            Background = Res<Brush>("CardBrush"),
            Child = _mascot,
        };
        _mascot.HorizontalAlignment = HorizontalAlignment.Center;
        _mascot.VerticalAlignment = VerticalAlignment.Center;
        _mascotPill.PointerEntered += (_, _) => SetHover("home");
        _mascotPill.PointerExited += (_, _) => SetHover(null);
        _mascotPill.Tapped += (_, e) => { e.Handled = true; _app.Vm.ShowPage(NotchPage.Home); };

        _homeLabel = BubbleLabel();
        SizeChanged += (_, _) => Layout();
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    private static TextBlock BubbleLabel() => new()
    {
        FontFamily = Res<FontFamily>("FontMono"),
        FontSize = 9,
        FontWeight = Microsoft.UI.Text.FontWeights.Medium,
        CharacterSpacing = 140,
        Opacity = 0,
    };

    public void OnShown()
    {
        _hover = null;
        Build();
        Layout();
        AnimateIn();
    }

    public void OnHidden() => _hover = null;

    private void Build()
    {
        _canvas.Children.Clear();
        _bubbles.Clear();
        _canvas.Children.Add(_mascotPill);
        _homeLabel.Text = Loc.Get("windows_home");
        _homeLabel.Foreground = Res<Brush>("SecondaryBrush");
        _canvas.Children.Add(_homeLabel);

        var items = _app.Vm.Pages.Where(p => p != NotchPage.Home).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var angle = (items.Count == 1 ? 270 : 180 + i * 180.0 / (items.Count - 1)) * Math.PI / 180;
            var x = Math.Round(Math.Cos(angle) * RadiusX);
            var y = Math.Round(-Math.Sin(angle) * RadiusY);
            var look = ((int)Math.Clamp(Math.Round(x / 95), -2, 2), y > 40 ? 1 : 0);
            var bubble = CreateBubble(items[i], look);
            bubble.Root.Tag = new Point(x, y);
            _canvas.Children.Add(bubble.Root);
            _canvas.Children.Add(bubble.Label);
            _bubbles.Add(bubble);
        }
        UpdateHover();
    }

    private Bubble CreateBubble(NotchPage page, (int, int) look)
    {
        var circle = new Ellipse { Fill = Res<Brush>("CardBrush"), StrokeThickness = 1.5 };
        var glyph = new DotMatrix
        {
            Glyph = page.Glyph, DotSize = 3, Gap = 1.5,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var root = new Grid { Width = BubbleSize, Height = BubbleSize };
        root.Children.Add(circle);
        root.Children.Add(glyph);

        // Insignia: 18 px de alto, radio 9, mín. 18 de ancho, esquina superior derecha (−3, −3).
        var count = page.Id switch
        {
            "clip" => _app.Clipboard.Items.Count,
            "shelf" => _app.Shelf.Items.Count,
            "todo" => _app.Tasks.Pending,
            "notes" => _app.Notes.Items.Count,
            _ => 0,
        };
        if (count > 0)
        {
            var isTodo = page.Id == "todo";
            root.Children.Add(new Border
            {
                MinWidth = 18,
                Height = 18,
                Padding = new Thickness(4, 0, 4, 0),
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(2),
                BorderBrush = Res<Brush>("BgBrush"),
                Background = isTodo ? new SolidColorBrush(Tokens.Current.Accent(_app.Settings.Accent)) : Res<Brush>("PrimaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -3, -3, 0),
                Child = new TextBlock
                {
                    Text = count.ToString(),
                    FontFamily = Res<FontFamily>("FontDisplay"),
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.Black,
                    Foreground = isTodo ? new SolidColorBrush(Microsoft.UI.Colors.White) : Res<Brush>("BgBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
        }

        var label = BubbleLabel();
        label.Text = Loc.Get(page.TitleKey);
        label.Foreground = Res<Brush>("PrimaryBrush");

        root.PointerEntered += (_, _) => SetHover(page.Id);
        root.PointerExited += (_, _) => SetHover(null);
        root.Tapped += (_, e) => { e.Handled = true; _app.Vm.ShowPage(page); };
        return new Bubble(page, root, circle, glyph, label, look);
    }

    private void Layout()
    {
        var cx = ActualWidth / 2;
        if (cx <= 0) return;
        Canvas.SetLeft(_mascotPill, cx - 54);
        Canvas.SetTop(_mascotPill, CenterY - 31);
        _homeLabel.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_homeLabel, cx - _homeLabel.DesiredSize.Width / 2);
        Canvas.SetTop(_homeLabel, 93);

        foreach (var bubble in _bubbles)
        {
            var offset = (Point)bubble.Root.Tag;
            var left = cx + offset.X - BubbleSize / 2;
            var top = CenterY + offset.Y - BubbleSize / 2;
            Canvas.SetLeft(bubble.Root, left);
            Canvas.SetTop(bubble.Root, top);
            bubble.Label.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(bubble.Label, left + BubbleSize / 2 - bubble.Label.DesiredSize.Width / 2);
            Canvas.SetTop(bubble.Label, top + BubbleSize + 7);
        }
    }

    private void AnimateIn()
    {
        var reduced = _app.Settings.Anim == "reduced";
        for (var i = 0; i < _bubbles.Count; i++)
        {
            var visual = ElementCompositionPreview.GetElementVisual(_bubbles[i].Root);
            visual.CenterPoint = new Vector3((float)BubbleSize / 2, (float)BubbleSize / 2, 0);
            if (reduced)
            {
                visual.Scale = Vector3.One;
                visual.Opacity = 1;
                continue;
            }
            var compositor = visual.Compositor;
            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(.3f, 1.4f), new Vector2(.5f, 1f));
            var delay = TimeSpan.FromMilliseconds(30 * i);

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0f, new Vector3(0.6f, 0.6f, 1));
            scale.InsertKeyFrame(1f, Vector3.One, easing);
            scale.Duration = TimeSpan.FromMilliseconds(300);
            scale.DelayTime = delay;
            scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            visual.StartAnimation("Scale", scale);

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f);
            fade.Duration = TimeSpan.FromMilliseconds(300);
            fade.DelayTime = delay;
            fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            visual.StartAnimation("Opacity", fade);
        }
    }

    private void SetHover(string? id)
    {
        _hover = id;
        UpdateHover();
    }

    /// <summary>Hover: burbuja más grande con anillo y etiqueta; el resto se atenúa; la mascota mira hacia ella.</summary>
    private void UpdateHover()
    {
        foreach (var bubble in _bubbles)
        {
            var isHover = _hover == bubble.Page.Id;
            bubble.Circle.Fill = isHover ? new SolidColorBrush(HoverFill) : Res<Brush>("CardBrush");
            bubble.Circle.Stroke = isHover ? Res<Brush>("PrimaryBrush") : null;
            bubble.Glyph.OnColor = Res<Color>(isHover ? "PrimaryColor" : "SoftColor");
            bubble.Label.Opacity = isHover ? 1 : 0;
            bubble.Root.Opacity = _hover is not null && !isHover ? 0.45 : 1;
            bubble.Root.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            bubble.Root.RenderTransform = isHover
                ? new CompositeTransform { ScaleX = 1.14, ScaleY = 1.14, TranslateY = -4 }
                : null;
        }

        var home = _hover == "home";
        _mascotPill.Background = home ? new SolidColorBrush(HoverFill) : Res<Brush>("CardBrush");
        _homeLabel.Opacity = home ? 1 : 0;
        _mascotPill.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        _mascotPill.RenderTransform = home ? new ScaleTransform { ScaleX = 1.06, ScaleY = 1.06 } : null;

        if (!_app.Settings.Mascot) _mascot.Cells = MascotFrames.Block();
        else if (home) _mascot.Cells = MascotFrames.HappyStill();
        else if (_bubbles.FirstOrDefault(b => b.Page.Id == _hover) is { } target) _mascot.Cells = MascotFrames.Looking(target.Look.Column, target.Look.Row);
        else _mascot.Cells = MascotFrames.Awake();
    }

    private readonly record struct Point(double X, double Y);
}
