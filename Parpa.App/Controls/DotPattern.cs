using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Parpa.Controls;

/// <summary>
/// Antes, fondo de puntos de los estados vacíos (CSS: <c>radial-gradient(circle, color 1px, transparent 1.3px)</c>
/// con <c>background-size</c> de <see cref="Spacing"/>). Recorta a sus esquinas redondeadas.
/// Va detrás del contenido: ponerlo como primer hijo de un Grid.
/// </summary>
public sealed partial class DotPattern : Grid
{
    public static readonly DependencyProperty DotColorProperty = DependencyProperty.Register(
        nameof(DotColor), typeof(Color), typeof(DotPattern), new PropertyMetadata(Color.FromArgb(255, 0x3a, 0x3a, 0x3f), (d, _) => ((DotPattern)d).Rebuild()));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(DotPattern), new PropertyMetadata(7.0, (d, _) => ((DotPattern)d).Rebuild()));

    public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
        nameof(DotBrush), typeof(SolidColorBrush), typeof(DotPattern), new PropertyMetadata(null, (d, _) =>
        {
            var p = (DotPattern)d;
            p._watch = DotMatrix.Watch(p._watch, p.DotBrush, c => p.DotColor = c);
        }));

    private (SolidColorBrush Brush, long Token)? _watch;

    public DotPattern()
    {
        IsHitTestVisible = false;
        SizeChanged += (_, _) => Rebuild();
    }

    public Color DotColor { get => (Color)GetValue(DotColorProperty); set => SetValue(DotColorProperty, value); }

    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Color de los puntos que sigue a un pincel del tema.</summary>
    public SolidColorBrush? DotBrush { get => (SolidColorBrush?)GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    /// <summary>
    /// Estilo isla: en vez de la trama de puntos, un relleno suave del color de los puntos (≈ 22 %),
    /// recortado a las esquinas. Así los estados vacíos, las zonas de soltar y los monitores siguen
    /// teniendo superficie sin el aspecto de matriz.
    /// </summary>
    private void Rebuild()
    {
        var c = DotColor;
        Background = new SolidColorBrush(Color.FromArgb(56, c.R, c.G, c.B));
    }
}
