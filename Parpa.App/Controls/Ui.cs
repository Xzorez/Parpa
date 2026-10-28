using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Parpa.Services;

namespace Parpa.Controls;

/// <summary>Piezas comunes de las ventanas de Ajustes y bienvenida (tipografía, segmentado, monitores).</summary>
public static class Ui
{
    public static FontFamily Font(string key) => (FontFamily)Application.Current.Resources[key];

    /// <summary>Geist (texto).</summary>
    public static TextBlock Text(string text, double size, string color = "primary", bool semibold = false) => new()
    {
        Text = text,
        FontFamily = Font("FontUi"),
        FontSize = size,
        FontWeight = semibold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        Foreground = Theme.Brush(color),
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>Geist Mono (etiquetas en mayúsculas), tracking en milésimas de em.</summary>
    public static TextBlock Mono(string text, double size, string color = "secondary", int tracking = 80, bool medium = false) => new()
    {
        Text = text,
        FontFamily = Font("FontMono"),
        FontSize = size,
        CharacterSpacing = tracking,
        FontWeight = medium ? Microsoft.UI.Text.FontWeights.Medium : Microsoft.UI.Text.FontWeights.Normal,
        Foreground = Theme.Brush(color),
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Doto (números).</summary>
    public static TextBlock Doto(string text, double size, bool black = false, string color = "primary") => new()
    {
        Text = text,
        FontFamily = Font("FontDisplay"),
        FontSize = size,
        FontWeight = black ? Microsoft.UI.Text.FontWeights.Black : Microsoft.UI.Text.FontWeights.Bold,
        Foreground = Theme.Brush(color),
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static SolidColorBrush Clear => new(Microsoft.UI.Colors.Transparent);

    /// <summary>
    /// Control segmentado: fondo #0b0b0c, padding 3, radio 8, gap 2; opciones mono 500 10 px,
    /// padding 6×10, radio 5; la elegida en claro.
    /// </summary>
    public static StackPanel Segmented<T>(IEnumerable<(T Value, string Label)> options, T current, Action<T> pick, double padX = 10, double padY = 6)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Padding = new Thickness(3),
            Background = Theme.Brush("bg"),
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (var (value, label) in options)
        {
            var selected = EqualityComparer<T>.Default.Equals(value, current);
            var text = Mono(label, 10, selected ? "bg" : "secondary", 60, medium: true);
            var option = new Border
            {
                Padding = new Thickness(padX, padY, padX, padY),
                CornerRadius = new CornerRadius(5),
                Background = selected ? Theme.Brush("primary") : Clear,
                Child = text,
            };
            option.Tapped += (_, e) =>
            {
                e.Handled = true;
                if (selected) return;
                SoundService.Play("click");
                pick(value);
            };
            panel.Children.Add(option);
        }
        return panel;
    }

    /// <summary>
    /// Monitores a escala con el elegido marcado (anillo claro) y la muesca del notch arriba
    /// en su posición (22 / 50 / 78 % + desplazamiento). Tocar uno mueve el notch allí.
    /// </summary>
    public static StackPanel MonitorPicker(ParpaSettings s, double maxWidth, double maxHeight, double numberSize, Action<int> pick)
    {
        var monitors = MonitorService.All();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
        var widest = monitors.Max(m => m.Bounds.Width);
        var tallest = monitors.Max(m => m.Bounds.Height);
        var scale = Math.Min(maxWidth / widest, maxHeight / tallest);
        var current = Math.Clamp(s.Monitor, 1, monitors.Count);
        var pct = s.Pos switch { "left" => 0.22, "right" => 0.78, _ => 0.5 };

        foreach (var monitor in monitors)
        {
            var selected = monitor.Number == current;
            var width = Math.Round(monitor.Bounds.Width * scale);
            var height = Math.Round(monitor.Bounds.Height * scale);
            var screen = new Grid
            {
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(5),
                Background = Theme.Brush("matrixOff"),
                BorderBrush = Theme.Brush(selected ? "primary" : "border"),
                BorderThickness = new Thickness(2),
            };
            screen.Children.Add(new DotPattern { DotColor = Tokens.ParseHex("#2c2c31"), Spacing = 6, CornerRadius = new CornerRadius(4) });
            var number = Doto(monitor.Number.ToString(), numberSize, black: true, selected ? "primary" : "dotPattern");
            number.HorizontalAlignment = HorizontalAlignment.Center;
            screen.Children.Add(number);
            if (selected)
            {
                screen.Children.Add(new Border
                {
                    Width = 26,
                    Height = 6,
                    CornerRadius = new CornerRadius(0, 0, 4, 4),
                    Background = Theme.Brush("primary"),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(Math.Clamp(width * pct + s.Offset * scale - 13 - 2, 0, width - 30), 0, 0, 0),
                });
            }

            var item = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Bottom };
            item.Children.Add(screen);
            var label = Mono($"{monitor.Bounds.Width}×{monitor.Bounds.Height}", 9);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            item.Children.Add(label);
            var number1 = monitor.Number;
            item.Tapped += (_, e) =>
            {
                e.Handled = true;
                SoundService.Play("click");
                pick(number1);
            };
            row.Children.Add(item);
        }
        return row;
    }
}
