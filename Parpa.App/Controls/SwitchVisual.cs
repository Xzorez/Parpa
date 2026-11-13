using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Shapes;
using Parpa.Services;

namespace Parpa.Controls;

/// <summary>
/// Interruptor estilo Akira: pista con borde de tinta de 1,5 px; encendido, rellena de tinta con la perilla
/// del color del fondo; apagado, hueca con la perilla de tinta (se ignora el tamaño de perilla pedido: alto − 7).
/// La perilla se desliza con rebote y la pista cambia de color con transición.
/// </summary>
public static class SwitchVisual
{
    private sealed record State(Ellipse Knob, double Travel);

    private static Microsoft.UI.Xaml.Media.SolidColorBrush Track(bool on) =>
        on ? Theme.Brush("primary") : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private static Microsoft.UI.Xaml.Media.SolidColorBrush Knob(bool on) => Theme.Brush(on ? "bg" : "primary");

    public static Grid Create(bool on, double width, double height, double knob)
    {
        knob = height - 7;
        var inset = (height - knob) / 2;
        var track = Motion.SmoothBackground(new Grid
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Background = Track(on),
            BorderBrush = Theme.Brush("primary"),
            BorderThickness = new Thickness(1.5),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var dot = new Ellipse
        {
            Width = knob,
            Height = knob,
            Fill = Knob(on),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(inset - 1.5, 0, 0, 0), // el borde ya ocupa 1,5 px
        };
        track.Children.Add(dot);

        // La posición va en Translation (animable); se fija antes de activar las implícitas para que no anime al crear.
        var travel = width - knob - 2 * inset;
        ElementCompositionPreview.SetIsTranslationEnabled(dot, true);
        ElementCompositionPreview.GetElementVisual(dot).Properties.InsertVector3("Translation", new Vector3(on ? (float)travel : 0, 0, 0));
        Motion.EnableImplicit(dot, Motion.Quick);
        track.Tag = new State(dot, travel);
        Motion.PressFeedback(track);
        return track;
    }

    /// <summary>Cambia el interruptor ya dibujado (con animación).</summary>
    public static void Flip(Grid track, bool on)
    {
        if (track.Tag is not State state) return;
        track.Background = Track(on);
        state.Knob.Fill = Knob(on);
        ElementCompositionPreview.GetElementVisual(state.Knob).Properties
            .InsertVector3("Translation", new Vector3(on ? (float)state.Travel : 0, 0, 0));
    }
}
