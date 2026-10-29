using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using Parpa.Services;
using Windows.UI.ViewManagement;

namespace Parpa.Controls;

/// <summary>
/// Movimiento común con las curvas del prototipo: rebote suave <c>cubic-bezier(.3,1.4,.5,1)</c> para
/// formas y escalas, y transiciones de color de 0,2 s. Con animaciones reducidas (ajuste <c>anim</c> o
/// "Efectos de animación" de Windows) todo es instantáneo. Se ejecuta en el compositor: 0 % CPU en reposo.
/// </summary>
public static class Motion
{
    private static readonly UISettings UiSettings = new();

    public static readonly TimeSpan Quick = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan Pop = TimeSpan.FromMilliseconds(260);

    /// <summary>Animaciones reducidas (ajuste, Windows o ahorro de batería): nada de rebotes ni desplazamientos.</summary>
    public static bool Reduced =>
        SettingsService.Current?.S.Anim == "reduced" || !UiSettings.AnimationsEnabled || PowerService.SaverActive;

    private static Compositor CompositorOf(UIElement element) => ElementCompositionPreview.GetElementVisual(element).Compositor;

    /// <summary>Rebote suave del diseño.</summary>
    public static CompositionEasingFunction Spring(Compositor c) =>
        c.CreateCubicBezierEasingFunction(new Vector2(.3f, 1.4f), new Vector2(.5f, 1f));

    /// <summary>ease-out para entradas.</summary>
    public static CompositionEasingFunction EaseOut(Compositor c) =>
        c.CreateCubicBezierEasingFunction(new Vector2(.2f, .8f), new Vector2(.2f, 1f));

    /// <summary>Fondo que cambia de color con transición (hover, selección) en Border, Grid y StackPanel.</summary>
    public static T SmoothBackground<T>(T element) where T : FrameworkElement
    {
        if (Reduced) return element;
        var transition = new BrushTransition { Duration = Quick };
        switch (element)
        {
            case Border border: border.BackgroundTransition = transition; break;
            case Panel panel: panel.BackgroundTransition = transition; break;
        }
        return element;
    }

    /// <summary>
    /// Escala, desplazamiento y opacidad animados al cambiarlos (implícitas de Composition con rebote):
    /// después basta con <see cref="SetScale"/>, <see cref="SetLift"/> u <see cref="SetOpacity"/>.
    /// </summary>
    public static void EnableImplicit(UIElement element, TimeSpan? duration = null)
    {
        if (Reduced) return;
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var c = visual.Compositor;
        var collection = c.CreateImplicitAnimationCollection();
        foreach (var (property, scalar) in new[] { ("Scale", false), ("Translation", false), ("Opacity", true) })
        {
            KeyFrameAnimation animation = scalar ? c.CreateScalarKeyFrameAnimation() : c.CreateVector3KeyFrameAnimation();
            if (animation is ScalarKeyFrameAnimation s) s.InsertExpressionKeyFrame(1f, "this.FinalValue", EaseOut(c));
            else ((Vector3KeyFrameAnimation)animation).InsertExpressionKeyFrame(1f, "this.FinalValue", Spring(c));
            animation.Duration = duration ?? Pop;
            animation.Target = property;
            collection[property] = animation;
        }
        visual.ImplicitAnimations = collection;
    }

    /// <summary>Escala desde el centro del elemento (animada si se activó <see cref="EnableImplicit"/>).</summary>
    public static void SetScale(FrameworkElement element, float scale)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);
        visual.Scale = new Vector3(scale, scale, 1);
    }

    /// <summary>Desplazamiento vertical (p. ej. −4 px en hover).</summary>
    public static void SetLift(UIElement element, float y)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        ElementCompositionPreview.GetElementVisual(element).Properties.InsertVector3("Translation", new Vector3(0, y, 0));
    }

    public static void SetOpacity(UIElement element, float opacity) =>
        ElementCompositionPreview.GetElementVisual(element).Opacity = opacity;

    /// <summary>Al pulsar, el elemento se hunde un poco (0,96) y vuelve con rebote al soltar.</summary>
    public static void PressFeedback(FrameworkElement element)
    {
        if (Reduced) return;
        EnableImplicit(element, TimeSpan.FromMilliseconds(180));
        element.PointerPressed += (_, _) => SetScale(element, 0.96f);
        element.PointerReleased += (_, _) => SetScale(element, 1f);
        element.PointerCanceled += (_, _) => SetScale(element, 1f);
        element.PointerCaptureLost += (_, _) => SetScale(element, 1f);
        element.PointerExited += (_, _) => SetScale(element, 1f);
    }

    /// <summary>
    /// Entrada escalonada de una lista: cada elemento aparece desde 6 px más abajo con fundido,
    /// 220 ms y 22 ms entre uno y otro (como la entrada de las burbujas).
    /// </summary>
    public static void StaggerIn(IEnumerable<UIElement> items, float fromY = 6, int stepMs = 22, int max = 12)
    {
        if (Reduced) return;
        var i = 0;
        foreach (var item in items)
        {
            if (i >= max) break;
            FadeUp(item, fromY, TimeSpan.FromMilliseconds(i * stepMs), TimeSpan.FromMilliseconds(220));
            i++;
        }
    }

    /// <summary>Aparece desde <paramref name="fromY"/> px (o <paramref name="fromX"/>) con fundido.</summary>
    public static void FadeUp(UIElement element, float fromY, TimeSpan delay, TimeSpan duration, float fromX = 0)
    {
        if (Reduced) return;
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var c = visual.Compositor;

        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f, EaseOut(c));
        fade.Duration = duration;
        fade.DelayTime = delay;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Opacity", fade);

        var move = c.CreateVector3KeyFrameAnimation();
        move.InsertKeyFrame(0f, new Vector3(fromX, fromY, 0));
        move.InsertKeyFrame(1f, Vector3.Zero, EaseOut(c));
        move.Duration = duration;
        move.DelayTime = delay;
        move.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Translation", move);
    }

    /// <summary>Entrada de ventana: fundido y escala 0,98 → 1 en 260 ms.</summary>
    public static void WindowIn(FrameworkElement root)
    {
        if (Reduced) return;
        root.Loaded += (_, _) =>
        {
            var visual = ElementCompositionPreview.GetElementVisual(root);
            var c = visual.Compositor;
            visual.CenterPoint = new Vector3((float)root.ActualWidth / 2, (float)root.ActualHeight / 2, 0);
            var scale = c.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0f, new Vector3(0.98f, 0.98f, 1));
            scale.InsertKeyFrame(1f, Vector3.One, EaseOut(c));
            scale.Duration = Pop;
            visual.StartAnimation("Scale", scale);
            var fade = c.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, EaseOut(c));
            fade.Duration = Pop;
            visual.StartAnimation("Opacity", fade);
        };
    }

    /// <summary>
    /// Anima Width/Height de un elemento XAML (barras de nivel, indicador de pasos). Corre en el hilo de la UI
    /// solo durante la transición.
    /// </summary>
    public static void AnimateSize(FrameworkElement element, double? width, double? height, TimeSpan duration)
    {
        if (Reduced || !element.IsLoaded)
        {
            if (width is { } w) element.Width = w;
            if (height is { } h) element.Height = h;
            return;
        }
        var storyboard = new Storyboard();
        void Add(string property, double from, double to)
        {
            if (double.IsNaN(from)) from = to;
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }
        if (width is { } toW && Math.Abs(element.Width - toW) > 0.1) Add("Width", element.Width, toW);
        if (height is { } toH && Math.Abs(element.Height - toH) > 0.1) Add("Height", element.Height, toH);
        if (storyboard.Children.Count == 0) return;
        storyboard.Completed += (_, _) =>
        {
            if (width is { } fw) element.Width = fw;
            if (height is { } fh) element.Height = fh;
        };
        storyboard.Begin();
    }

    /// <summary>
    /// Deja ver primero la animación (interruptor, segmento) y aplica el cambio después,
    /// porque aplicarlo redibuja la lista entera.
    /// </summary>
    public static void AfterTransition(Action commit)
    {
        if (Reduced)
        {
            commit();
            return;
        }
        var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.IsRepeating = false;
        timer.Interval = TimeSpan.FromMilliseconds(170);
        timer.Tick += (_, _) => commit();
        timer.Start();
    }
}
