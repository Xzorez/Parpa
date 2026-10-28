using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Shapes;
using Parpa.Services;

namespace Parpa.Controls;

/// <summary>
/// Interruptor del diseño: pista redondeada (#f2f2f0 encendido / #2a2a2e apagado) con perilla
/// (#0b0b0c / #8d8d92). Ajustes: 40×22 y perilla 14; menú de bandeja: 30×16 y perilla 10.
/// La perilla se desliza con rebote (<c>cubic-bezier(.3,1.4,.5,1)</c>, 0,2 s) y la pista cambia de color con transición.
/// </summary>
public static class SwitchVisual
{
    private sealed record State(Ellipse Knob, double Travel);

    public static Grid Create(bool on, double width, double height, double knob)
    {
        var inset = (height - knob) / 2;
        var track = Motion.SmoothBackground(new Grid
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Background = Theme.Brush(on ? "primary" : "border"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var dot = new Ellipse
        {
            Width = knob,
            Height = knob,
            Fill = Theme.Brush(on ? "bg" : "secondary"),
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
        track.Background = Theme.Brush(on ? "primary" : "border");
        state.Knob.Fill = Theme.Brush(on ? "bg" : "secondary");
        ElementCompositionPreview.GetElementVisual(state.Knob).Properties
            .InsertVector3("Translation", new Vector3(on ? (float)state.Travel : 0, 0, 0));
    }
}
