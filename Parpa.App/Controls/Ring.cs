using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Parpa.Controls;

/// <summary>
/// Anillo de progreso con el valor en el centro (CPU, GPU, RAM en Inicio; estilo anillos de actividad):
/// pista tenue y arco de color con extremos redondeados que empieza arriba y gira en el sentido del reloj.
/// </summary>
public sealed partial class Ring : Grid
{
    private readonly Path _arc;
    private readonly TextBlock _value;
    private readonly double _size;
    private readonly double _stroke;
    private double _fraction = -1;

    public Ring(double size, double stroke, Color color, Brush track)
    {
        _size = size;
        _stroke = stroke;
        Width = size;
        Height = size;
        Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = track,
            StrokeThickness = stroke,
        });
        _arc = new Path
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = stroke,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        Children.Add(_arc);
        _value = new TextBlock
        {
            FontFamily = Ui.Font("FontDisplay"),
            FontSize = Math.Round(size * 0.26),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Services.Theme.Brush("primary"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Children.Add(_value);
    }

    /// <summary>Porcentaje 0–100, o null si no se sabe (anillo vacío y «—»).</summary>
    public void Set(int? percent)
    {
        _value.Text = percent is { } p ? $"{p}" : "—";
        var fraction = Math.Clamp((percent ?? 0) / 100.0, 0, 1);
        if (Math.Abs(fraction - _fraction) < 0.001) return;
        _fraction = fraction;
        _arc.Data = Arc(fraction);
    }

    private Geometry? Arc(double fraction)
    {
        if (fraction <= 0) return null;
        var r = (_size - _stroke) / 2;
        var c = _size / 2;
        // Un arco completo no se puede dibujar con un solo segmento: se queda justo por debajo.
        var angle = Math.Min(fraction, 0.9999) * 2 * Math.PI;
        var start = new Point(c, c - r);
        var end = new Point(c + r * Math.Sin(angle), c - r * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(r, r),
            IsLargeArc = angle > Math.PI,
            SweepDirection = SweepDirection.Clockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
