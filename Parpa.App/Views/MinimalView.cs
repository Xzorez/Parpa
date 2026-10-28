using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Parpa.Controls;
using Parpa.Services;
using Parpa.ViewModels;

namespace Parpa.Views;

/// <summary>
/// Modo minimal (24 px): una línea fina con el glifo de la actividad en puntos de 1,5 px (#8d8d92).
/// En reposo queda vacía.
/// </summary>
public sealed partial class MinimalView : Grid
{
    private readonly DotMatrix _glyph = new()
    {
        DotSize = 1.5,
        Gap = 1,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        OnBrush = Theme.Brush("secondary"),
    };

    public MinimalView()
    {
        Height = 24;
        Children.Add(_glyph);
    }

    public void Show(NotchActivity? activity, NotchGeometry geometry)
    {
        Width = geometry.Width;
        _glyph.Visibility = activity is null ? Visibility.Collapsed : Visibility.Visible;
        if (activity is not null) _glyph.Glyph = activity.Glyph;
    }
}
