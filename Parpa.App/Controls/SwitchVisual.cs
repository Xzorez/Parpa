using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Parpa.Services;

namespace Parpa.Controls;

/// <summary>
/// Interruptor del diseño: pista redondeada (#f2f2f0 encendido / #2a2a2e apagado) con perilla
/// (#0b0b0c / #8d8d92). Ajustes: 40×22 y perilla 14; menú de bandeja: 30×16 y perilla 10.
/// </summary>
public static class SwitchVisual
{
    public static Grid Create(bool on, double width, double height, double knob)
    {
        var inset = (height - knob) / 2;
        var track = new Grid
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Background = Theme.Brush(on ? "primary" : "border"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        track.Children.Add(new Ellipse
        {
            Width = knob,
            Height = knob,
            Fill = Theme.Brush(on ? "bg" : "secondary"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(on ? width - knob - inset : inset, inset, 0, 0),
        });
        return track;
    }
}
