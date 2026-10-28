using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Parpa.Controls;

/// <summary>
/// Píldora: equivalente a <c>border-radius: 999px</c> de CSS.
/// En XAML un CornerRadius mayor que la mitad del alto se reparte en ambos ejes y sale un óvalo;
/// esto fija el radio a la mitad del alto real (extremos semicirculares, lados rectos).
/// Uso: <c>&lt;Border c:Pill.IsPill="True"&gt;</c>.
/// </summary>
public static class Pill
{
    public static readonly DependencyProperty IsPillProperty = DependencyProperty.RegisterAttached(
        "IsPill", typeof(bool), typeof(Pill), new PropertyMetadata(false, OnIsPillChanged));

    public static bool GetIsPill(Border border) => (bool)border.GetValue(IsPillProperty);

    public static void SetIsPill(Border border, bool value) => border.SetValue(IsPillProperty, value);

    private static void OnIsPillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border border) return;
        border.SizeChanged -= OnSizeChanged;
        if (e.NewValue is true)
        {
            border.SizeChanged += OnSizeChanged;
            Apply(border);
        }
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => Apply((Border)sender);

    private static void Apply(Border border) => border.CornerRadius = new CornerRadius(border.ActualHeight / 2);
}
