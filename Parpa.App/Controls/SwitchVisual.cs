using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Shapes;
using Parpa.Services;

namespace Parpa.Controls;

/// <summary>
/// Interruptor estilo iOS: pista redondeada verde encendida / gris apagada y perilla blanca casi del alto
/// de la pista (se ignora el tamaño de perilla pedido: alto − 4). La perilla se desliza con rebote y la pista
/// cambia de color con transición.
/// </summary>
public static class SwitchVisual
{
    private sealed record State(Ellipse Knob, double Travel);

    private static Microsoft.UI.Xaml.Media.SolidColorBrush Track(bool on) =>
        on ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Tokens.Current.Accent(2)) : Theme.Brush("border");

    public static Grid Create(bool on, double width, double height, double knob)
    {
        knob = height - 4;
        var inset = (height - knob) / 2;
        var track = Motion.SmoothBackground(new Grid
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Background = Track(on),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var dot = new Ellipse
        {
            Width = knob,
            Height = knob,
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(inset, inset, 0, 0),
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
        ElementCompositionPreview.GetElementVisual(state.Knob).Properties
            .InsertVector3("Translation", new Vector3(on ? (float)state.Travel : 0, 0, 0));
    }
}
