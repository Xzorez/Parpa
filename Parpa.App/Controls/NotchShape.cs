using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Parpa.Services;
using Parpa.ViewModels;
using Windows.UI;

namespace Parpa.Controls;

/// <summary>
/// Forma del notch: cuerpo con esquinas inferiores redondeadas + dos orejas cóncavas de 14×14 arriba.
/// Ancho, alto y radio viven en un PropertySet de Composition y se animan en el hilo del compositor;
/// orejas, relleno y recorte del contenido se enlazan a él con ExpressionAnimations.
/// </summary>
public sealed partial class NotchShape : Grid
{
    private const float Ear = 14f;
    private const float EarOverlap = 2f; // las orejas se meten 2 px bajo el cuerpo para que no haya costura

    private readonly Compositor _compositor;
    private readonly CompositionPropertySet _props;
    private readonly CompositionColorBrush _fill;
    private readonly Grid _content = new();
    private readonly List<FrameworkElement> _views = new();
    private FrameworkElement? _current;
    private int _transitionId;

    /// <summary>Fin de la animación de forma más reciente (para ajustar la región de la ventana).</summary>
    public event Action? TransitionCompleted;

    public NotchShape()
    {
        _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _props = _compositor.CreatePropertySet();
        _props.InsertScalar("W", 0);
        _props.InsertScalar("H", 0);
        _props.InsertScalar("R", 0);
        _props.InsertScalar("CX", 0);
        _fill = _compositor.CreateColorBrush(Tokens.Current.Dark["bg"]);

        var shapeHost = new Border();
        Children.Add(shapeHost);
        Children.Add(_content);
        ElementCompositionPreview.SetElementChildVisual(shapeHost, BuildShape());

        // overflow: hidden — el contenido se recorta al cuerpo durante la animación.
        ElementCompositionPreview.GetElementVisual(_content).Clip = _compositor.CreateGeometricClip(CreateBodyGeometry());

        SizeChanged += (_, e) => _props.InsertScalar("CX", (float)e.NewSize.Width / 2);
    }

    public Color Fill
    {
        get => _fill.Color;
        set => _fill.Color = value;
    }

    /// <summary>Añade una vista. Su ancho (Width) debe estar fijado: se coloca centrada en el notch.</summary>
    /// <param name="anchorLeft">
    /// <c>true</c>: la vista va pegada al borde izquierdo del cuerpo mientras crece (vistas abiertas, como el prototipo);
    /// <c>false</c>: se queda centrada (reposo, compacto).
    /// </param>
    public void AddView(FrameworkElement view, bool anchorLeft)
    {
        view.HorizontalAlignment = HorizontalAlignment.Center;
        view.VerticalAlignment = VerticalAlignment.Top;
        view.Visibility = Visibility.Collapsed;
        _content.Children.Add(view);
        _views.Add(view);

        if (!anchorLeft) return;
        ElementCompositionPreview.SetIsTranslationEnabled(view, true);
        var follow = _compositor.CreateExpressionAnimation("Vector3((wv - props.W) * 0.5, 0, 0)");
        follow.SetReferenceParameter("props", _props);
        follow.SetScalarParameter("wv", (float)view.Width);
        ElementCompositionPreview.GetElementVisual(view).StartAnimation("Translation", follow);
    }

    /// <summary>Cambia la forma a <paramref name="target"/> y hace cross-fade a <paramref name="view"/>.</summary>
    /// <param name="motion"><c>null</c> = sin animación (arranque).</param>
    public void Show(FrameworkElement view, NotchGeometry target, Tokens.Motion? motion)
    {
        var previous = _current;
        _current = view;
        var id = ++_transitionId;

        if (motion is not { } m)
        {
            _props.InsertScalar("W", (float)target.Width);
            _props.InsertScalar("H", (float)target.Height);
            _props.InsertScalar("R", (float)target.Radius);
            foreach (var v in _views)
            {
                v.Visibility = v == view ? Visibility.Visible : Visibility.Collapsed;
                ElementCompositionPreview.GetElementVisual(v).Opacity = 1;
            }
            TransitionCompleted?.Invoke();
            return;
        }

        var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        var easing = _compositor.CreateCubicBezierEasingFunction(m.P1, m.P2);
        AnimateProp("W", target.Width, m.Duration, easing);
        AnimateProp("H", target.Height, m.Duration, easing);
        AnimateProp("R", target.Radius, m.Duration, easing);

        if (previous != view)
        {
            // El contenido nuevo entra tras ~40 % del cambio de tamaño; el anterior se apaga en ese 40 %.
            var lead = m.Duration * 0.4;
            view.Visibility = Visibility.Visible;
            var fadeIn = _compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(0f, 0f);
            fadeIn.InsertKeyFrame(1f, 1f, _compositor.CreateCubicBezierEasingFunction(new Vector2(0f, 0f), new Vector2(.58f, 1f)));
            fadeIn.Duration = m.Duration - lead;
            fadeIn.DelayTime = lead;
            fadeIn.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            ElementCompositionPreview.GetElementVisual(view).StartAnimation("Opacity", fadeIn);

            if (previous is not null)
            {
                var fadeOut = _compositor.CreateScalarKeyFrameAnimation();
                fadeOut.InsertKeyFrame(1f, 0f, _compositor.CreateLinearEasingFunction());
                fadeOut.Duration = lead;
                ElementCompositionPreview.GetElementVisual(previous).StartAnimation("Opacity", fadeOut);
            }
        }

        batch.End();
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        batch.Completed += (_, _) => dispatcher.TryEnqueue(() =>
        {
            if (id != _transitionId) return; // otra transición ya tomó el relevo
            foreach (var v in _views)
                if (v != _current) v.Visibility = Visibility.Collapsed;
            TransitionCompleted?.Invoke();
        });
    }

    private void AnimateProp(string name, double to, TimeSpan duration, CompositionEasingFunction easing)
    {
        var anim = _compositor.CreateScalarKeyFrameAnimation();
        anim.InsertKeyFrame(1f, (float)to, easing);
        anim.Duration = duration;
        _props.StartAnimation(name, anim);
    }

    private ContainerVisual BuildShape()
    {
        var root = _compositor.CreateContainerVisual();
        root.RelativeSizeAdjustment = Vector2.One;
        root.Clip = _compositor.CreateInsetClip(); // oculta la parte del cuerpo que sube por encima de y = 0

        // Sombra del tema claro: copias del cuerpo desplazadas 1, 2 y 3 px con negro muy tenue.
        foreach (var (dy, alpha) in ShadowSteps)
        {
            var brush = _compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
            _shadowBrushes.Add((brush, alpha));
            var layer = _compositor.CreateShapeVisual();
            layer.RelativeSizeAdjustment = Vector2.One;
            layer.Offset = new Vector3(0, dy, 0);
            var sprite = _compositor.CreateSpriteShape(CreateBodyGeometry());
            sprite.FillBrush = brush;
            layer.Shapes.Add(sprite);
            root.Children.InsertAtTop(layer);
        }

        var body = _compositor.CreateShapeVisual();
        body.RelativeSizeAdjustment = Vector2.One;
        body.Shapes.Add(_compositor.CreateSpriteShape(CreateBodyGeometry()));
        ((CompositionSpriteShape)body.Shapes[0]).FillBrush = _fill;
        root.Children.InsertAtTop(body);

        root.Children.InsertAtTop(CreateEar(left: true));
        root.Children.InsertAtTop(CreateEar(left: false));
        return root;
    }

    /// <summary>Desplazamiento y opacidad de cada paso de la sombra (≈ 0 1px 0 rgba(0,0,0,.06), suavizada).</summary>
    private static readonly (float Dy, byte Alpha)[] ShadowSteps = [(3, 8), (2, 12), (1, 16)];

    private readonly List<(CompositionColorBrush Brush, byte Alpha)> _shadowBrushes = new();

    /// <summary>
    /// Sombra del tema claro. El diseño pide además <c>0 8px 18px rgba(0,0,0,.14)</c>, pero la ventana solo existe
    /// dentro de la forma + hoverSens (para dejar pasar los clics) y una DropShadow de Composition rompe su
    /// transparencia, así que se dibuja una sombra corta que cabe en ese margen.
    /// </summary>
    public void SetShadow(bool on)
    {
        foreach (var (brush, alpha) in _shadowBrushes)
            brush.Color = Color.FromArgb(on ? alpha : (byte)0, 0, 0, 0);
    }

    /// <summary>
    /// Rectángulo redondeado desplazado −R hacia arriba: solo se ven sus esquinas inferiores.
    /// Mismo enlace para el relleno y para el recorte del contenido.
    /// </summary>
    private CompositionRoundedRectangleGeometry CreateBodyGeometry()
    {
        var geometry = _compositor.CreateRoundedRectangleGeometry();
        Bind(geometry, "Offset", "Vector2(props.CX - props.W * 0.5, -props.R)");
        Bind(geometry, "Size", "Vector2(props.W, props.H + props.R)");
        Bind(geometry, "CornerRadius", "Vector2(props.R, props.R)");
        return geometry;
    }

    /// <summary>
    /// Oreja 16×14 = cuadrado menos un cuarto de círculo de radio 14 en su esquina exterior inferior.
    /// Se dibuja como un anillo (radio interior 14) recortado al cuadrado.
    /// </summary>
    private ShapeVisual CreateEar(bool left)
    {
        const float ringThickness = 10f; // radio exterior 24 > diagonal del cuadrado (≈ 21,3)
        var ear = _compositor.CreateShapeVisual();
        ear.Size = new Vector2(Ear + EarOverlap, Ear);
        ear.Clip = _compositor.CreateInsetClip();

        var ring = _compositor.CreateEllipseGeometry();
        ring.Center = new Vector2(left ? 0 : Ear + EarOverlap, Ear);
        ring.Radius = new Vector2(Ear + ringThickness / 2);
        var shape = _compositor.CreateSpriteShape(ring);
        shape.StrokeBrush = _fill;
        shape.StrokeThickness = ringThickness;
        ear.Shapes.Add(shape);

        Bind(ear, "Offset", left
            ? $"Vector3(props.CX - props.W * 0.5 - {Ear}, 0, 0)"
            : $"Vector3(props.CX + props.W * 0.5 - {EarOverlap}, 0, 0)");
        return ear;
    }

    private void Bind(CompositionObject target, string property, string expression)
    {
        var anim = _compositor.CreateExpressionAnimation(expression);
        anim.SetReferenceParameter("props", _props);
        target.StartAnimation(property, anim);
    }
}
