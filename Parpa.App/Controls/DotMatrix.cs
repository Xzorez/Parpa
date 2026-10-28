using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Parpa.Services;
using Windows.UI;

namespace Parpa.Controls;

/// <summary>
/// Matriz de puntos redondos (glifos 7×7 de glyphs.json, mascota 13×5…), dibujada con Composition:
/// sin elementos XAML por punto y sin coste mientras no cambia.
/// </summary>
public sealed partial class DotMatrix : Grid
{
    public static readonly DependencyProperty GlyphProperty = Register(nameof(Glyph), typeof(string), null);
    public static readonly DependencyProperty ColumnsProperty = Register(nameof(Columns), typeof(int), 7);
    public static readonly DependencyProperty RowsProperty = Register(nameof(Rows), typeof(int), 7);
    public static readonly DependencyProperty DotSizeProperty = Register(nameof(DotSize), typeof(double), 2.0);
    public static readonly DependencyProperty GapProperty = Register(nameof(Gap), typeof(double), 1.0);
    public static readonly DependencyProperty OnColorProperty = Register(nameof(OnColor), typeof(Color), Color.FromArgb(255, 0xf2, 0xf2, 0xf0));
    public static readonly DependencyProperty OffColorProperty = Register(nameof(OffColor), typeof(Color), Color.FromArgb(0, 0, 0, 0));

    private ShapeVisual? _visual;
    private CompositionColorBrush? _onBrush;
    private CompositionColorBrush? _offBrush;
    private CompositionSpriteShape[] _dots = [];
    private bool[]? _cells;

    public DotMatrix()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>Nombre de un glifo de glyphs.json (7×7).</summary>
    public string? Glyph { get => (string?)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public int Rows { get => (int)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public double DotSize { get => (double)GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public Color OnColor { get => (Color)GetValue(OnColorProperty); set => SetValue(OnColorProperty, value); }
    public Color OffColor { get => (Color)GetValue(OffColorProperty); set => SetValue(OffColorProperty, value); }

    /// <summary>Celdas por filas (Columns × Rows). Si se asigna, sustituye a <see cref="Glyph"/>.</summary>
    public bool[]? Cells
    {
        get => _cells;
        set { _cells = value; ApplyCells(); }
    }

    private static DependencyProperty Register(string name, Type type, object? defaultValue) =>
        DependencyProperty.Register(name, type, typeof(DotMatrix), new PropertyMetadata(defaultValue, OnPropertyChanged));

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var m = (DotMatrix)d;
        if (e.Property == OnColorProperty && m._onBrush is not null) m._onBrush.Color = m.OnColor;
        else if (e.Property == OffColorProperty && m._offBrush is not null) m._offBrush.Color = m.OffColor;
        else if (e.Property == GlyphProperty) { m._cells = null; m.ApplyCells(); }
        else m.Rebuild();
    }

    private void Rebuild()
    {
        var size = (float)DotSize;
        var step = (float)(DotSize + Gap);
        Width = Columns * DotSize + (Columns - 1) * Gap;
        Height = Rows * DotSize + (Rows - 1) * Gap;
        if (!IsLoaded) return;

        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _onBrush ??= compositor.CreateColorBrush(OnColor);
        _offBrush ??= compositor.CreateColorBrush(OffColor);
        _visual ??= compositor.CreateShapeVisual();
        _visual.Size = new Vector2((float)Width, (float)Height);
        _visual.Shapes.Clear();

        _dots = new CompositionSpriteShape[Columns * Rows];
        for (var i = 0; i < _dots.Length; i++)
        {
            var geometry = compositor.CreateEllipseGeometry();
            geometry.Center = new Vector2(size / 2);
            geometry.Radius = new Vector2(size / 2);
            var dot = compositor.CreateSpriteShape(geometry);
            dot.Offset = new Vector2(i % Columns * step, i / Columns * step);
            _visual.Shapes.Add(dot);
            _dots[i] = dot;
        }
        ElementCompositionPreview.SetElementChildVisual(this, _visual);
        ApplyCells();
    }

    private void ApplyCells()
    {
        if (_dots.Length == 0) return;
        var cells = _cells ?? (Glyph is { Length: > 0 } name ? Glyphs.Get(name) : null);
        for (var i = 0; i < _dots.Length; i++)
            _dots[i].FillBrush = cells is not null && i < cells.Length && cells[i] ? _onBrush : _offBrush;
    }
}
