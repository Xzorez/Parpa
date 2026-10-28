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

    private sealed record Bubble(NotchPage Page, Grid Root, Grid Floater, Border Circle, DotMatrix Glyph, TextBlock Label, (int Column, int Row) Look);

    public LauncherView(NotchServices app)
    {
        _app = app;
        Height = AreaHeight;
        Children.Add(_canvas);

        _mascot = new DotMatrix { Columns = 13, Rows = 5, DotSize = 4, Gap = 1.5, OnBrush = Theme.Brush("primary"), OffBrush = Theme.Brush("glyphOff") };
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
        Motion.SmoothBackground(_mascotPill);
        Motion.EnableImplicit(_mascotPill, TimeSpan.FromMilliseconds(250));
        _mascotPill.PointerEntered += (_, _) => SetHover("home");
        _mascotPill.PointerExited += (_, _) => SetHover(null);
        _mascotPill.Tapped += (_, e) => { e.Handled = true; _app.Vm.ShowPage(NotchPage.Home); };

        _homeLabel = BubbleLabel();
        SizeChanged += (_, _) => Layout();
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    /// <summary>Etiqueta bajo la burbuja: mono 500 9 px, tracking .14em; aparece con fundido en hover.</summary>
    private static TextBlock BubbleLabel() => new()
    {
        FontFamily = Res<FontFamily>("FontMono"),
        FontSize = 9,
        FontWeight = Microsoft.UI.Text.FontWeights.Medium,
        CharacterSpacing = 140,
        Opacity = 0,
        OpacityTransition = Motion.Reduced ? null : new ScalarTransition { Duration = Motion.Quick },
    };

    public void OnShown()
    {
        _hover = null;
        Build();
        Layout();
        AnimateIn();
    }

    /// <summary>Al salir del menú se para la flotación (no queda nada animándose en segundo plano).</summary>
    public void OnHidden()
    {
        _hover = null;
        foreach (var bubble in _bubbles)
            ElementCompositionPreview.GetElementVisual(bubble.Floater).StopAnimation("Translation");
    }

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
        var circle = Motion.SmoothBackground(new Border
        {
            CornerRadius = new CornerRadius(BubbleSize / 2),
            Background = Res<Brush>("CardBrush"),
            BorderThickness = new Thickness(1.5),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        });
        var glyph = new DotMatrix
        {
            Glyph = page.Glyph, DotSize = 3, Gap = 1.5,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        // root: posición, entrada y hover. floater: la flotación continua (no se pisan las animaciones).
        var root = new Grid { Width = BubbleSize, Height = BubbleSize };
        var floater = new Grid();
        root.Children.Add(floater);
        floater.Children.Add(circle);
        floater.Children.Add(glyph);
        Motion.EnableImplicit(root, TimeSpan.FromMilliseconds(220));

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
            floater.Children.Add(new Border
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

        root.PointerEntered += (_, _) => { SoundService.Play("tick"); SetHover(page.Id); };
        root.PointerExited += (_, _) => SetHover(null);
        root.Tapped += (_, e) => { e.Handled = true; _app.Vm.ShowPage(page); };
        return new Bubble(page, root, floater, circle, glyph, label, look);
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

    /// <summary>
    /// Las burbujas salen de la mascota hacia su sitio (escala 0,6→1 + opacidad, 300 ms, cada 30 ms)
    /// y luego flotan ±2,5 px en ciclos de ~5,6 s con fases distintas, como el prototipo.
    /// </summary>
    private void AnimateIn()
    {
        var pill = ElementCompositionPreview.GetElementVisual(_mascotPill);
        pill.CenterPoint = new Vector3(54, 31, 0);
        if (Motion.Reduced)
        {
            pill.Scale = Vector3.One;
            foreach (var bubble in _bubbles)
            {
                var v = ElementCompositionPreview.GetElementVisual(bubble.Root);
                v.Scale = Vector3.One;
                v.Opacity = 1;
            }
            return;
        }

        var compositor = pill.Compositor;
        var pillIn = compositor.CreateVector3KeyFrameAnimation();
        pillIn.InsertKeyFrame(0f, new Vector3(0.6f, 0.6f, 1));
        pillIn.InsertKeyFrame(1f, Vector3.One, Motion.Spring(compositor));
        pillIn.Duration = TimeSpan.FromMilliseconds(300);
        pill.StartAnimation("Scale", pillIn);

        for (var i = 0; i < _bubbles.Count; i++)
        {
            var bubble = _bubbles[i];
            var offset = (Point)bubble.Root.Tag;
            var visual = ElementCompositionPreview.GetElementVisual(bubble.Root);
            visual.CenterPoint = new Vector3((float)BubbleSize / 2, (float)BubbleSize / 2, 0);
            var delay = TimeSpan.FromMilliseconds(30 * i + 40);
            var duration = TimeSpan.FromMilliseconds(360);

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0f, new Vector3(0.6f, 0.6f, 1));
            scale.InsertKeyFrame(1f, Vector3.One, Motion.Spring(compositor));
            scale.Duration = duration;
            scale.DelayTime = delay;
            scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            visual.StartAnimation("Scale", scale);

            // Desde el centro (la mascota) hasta su sitio.
            ElementCompositionPreview.SetIsTranslationEnabled(bubble.Root, true);
            var travel = compositor.CreateVector3KeyFrameAnimation();
            travel.InsertKeyFrame(0f, new Vector3((float)-offset.X * 0.7f, (float)-offset.Y * 0.7f, 0));
            travel.InsertKeyFrame(1f, Vector3.Zero, Motion.Spring(compositor));
            travel.Duration = duration;
            travel.DelayTime = delay;
            travel.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            visual.StartAnimation("Translation", travel);

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, Motion.EaseOut(compositor));
            fade.Duration = TimeSpan.FromMilliseconds(240);
            fade.DelayTime = delay;
            fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            visual.StartAnimation("Opacity", fade);

            // Flotación: 2,5 px arriba y abajo, fase distinta por burbuja.
            ElementCompositionPreview.SetIsTranslationEnabled(bubble.Floater, true);
            var floater = ElementCompositionPreview.GetElementVisual(bubble.Floater);
            var sine = compositor.CreateCubicBezierEasingFunction(new Vector2(.37f, 0f), new Vector2(.63f, 1f));
            var bob = compositor.CreateVector3KeyFrameAnimation();
            bob.InsertKeyFrame(0f, Vector3.Zero);
            bob.InsertKeyFrame(0.25f, new Vector3(0, -2.5f, 0), sine);
            bob.InsertKeyFrame(0.75f, new Vector3(0, 2.5f, 0), sine);
            bob.InsertKeyFrame(1f, Vector3.Zero, sine);
            bob.Duration = TimeSpan.FromSeconds(5.6);
            bob.DelayTime = TimeSpan.FromMilliseconds(400 + 950 * i);
            bob.IterationBehavior = AnimationIterationBehavior.Forever;
            floater.StartAnimation("Translation", bob);
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
        // Todo con transición: escala/elevación con rebote (implícitas), fondos y opacidades en 0,2 s.
        foreach (var bubble in _bubbles)
        {
            var isHover = _hover == bubble.Page.Id;
            bubble.Circle.Background = isHover ? new SolidColorBrush(HoverFill) : Res<Brush>("CardBrush");
            bubble.Circle.BorderBrush = isHover ? Res<Brush>("PrimaryBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            bubble.Glyph.OnColor = Theme.Get(isHover ? "primary" : "soft");
            bubble.Label.Opacity = isHover ? 1 : 0;
            Motion.SetOpacity(bubble.Root, _hover is not null && !isHover ? 0.45f : 1f);
            Motion.SetScale(bubble.Root, isHover ? 1.14f : 1f);
            Motion.SetLift(bubble.Root, isHover ? -4 : 0);
        }

        var home = _hover == "home";
        _mascotPill.Background = home ? new SolidColorBrush(HoverFill) : Res<Brush>("CardBrush");
        _homeLabel.Opacity = home ? 1 : 0;
        Motion.SetScale(_mascotPill, home ? 1.06f : 1f);

        if (!_app.Settings.Mascot) _mascot.Cells = MascotFrames.Block();
        else if (home) _mascot.Cells = MascotFrames.HappyStill();
        else if (_bubbles.FirstOrDefault(b => b.Page.Id == _hover) is { } target) _mascot.Cells = MascotFrames.Looking(target.Look.Column, target.Look.Row);
        else _mascot.Cells = MascotFrames.Awake();
    }

    private readonly record struct Point(double X, double Y);
}
