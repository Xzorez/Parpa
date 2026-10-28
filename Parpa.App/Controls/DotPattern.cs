using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI;

namespace Parpa.Controls;

/// <summary>
/// Fondo de puntos de los estados vacíos (CSS: <c>radial-gradient(circle, color 1px, transparent 1.3px)</c>
/// con <c>background-size</c> de <see cref="Spacing"/>). Recorta a sus esquinas redondeadas.
/// Va detrás del contenido: ponerlo como primer hijo de un Grid.
/// </summary>
public sealed partial class DotPattern : Grid
{
    public static readonly DependencyProperty DotColorProperty = DependencyProperty.Register(
        nameof(DotColor), typeof(Color), typeof(DotPattern), new PropertyMetadata(Color.FromArgb(255, 0x3a, 0x3a, 0x3f), (d, _) => ((DotPattern)d).Rebuild()));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(DotPattern), new PropertyMetadata(7.0, (d, _) => ((DotPattern)d).Rebuild()));

    private ShapeVisual? _visual;

    public DotPattern()
    {
        IsHitTestVisible = false;
        SizeChanged += (_, _) => Rebuild();
    }

    public Color DotColor { get => (Color)GetValue(DotColorProperty); set => SetValue(DotColorProperty, value); }

    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    private void Rebuild()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _visual ??= compositor.CreateShapeVisual();
        _visual.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        _visual.Shapes.Clear();

        var brush = compositor.CreateColorBrush(DotColor);
        var step = (float)Spacing;
        // Como CSS: el primer punto está en el centro de la primera celda.
        for (var y = step / 2; y < ActualHeight; y += step)
        {
            for (var x = step / 2; x < ActualWidth; x += step)
            {
                var dot = compositor.CreateEllipseGeometry();
                dot.Center = new Vector2(x, y);
                dot.Radius = new Vector2(1.15f);
                var shape = compositor.CreateSpriteShape(dot);
                shape.FillBrush = brush;
                _visual.Shapes.Add(shape);
            }
        }

        var radius = (float)CornerRadius.TopLeft;
        var clip = compositor.CreateRoundedRectangleGeometry();
        clip.Size = _visual.Size;
        clip.CornerRadius = new Vector2(radius);
        _visual.Clip = compositor.CreateGeometricClip(clip);
        ElementCompositionPreview.SetElementChildVisual(this, _visual);
    }
}
