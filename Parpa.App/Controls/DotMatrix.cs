using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
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
    public static readonly DependencyProperty CountProperty = Register(nameof(Count), typeof(int), -1);
    public static readonly DependencyProperty OnBrushProperty = Register(nameof(OnBrush), typeof(SolidColorBrush), null);
    public static readonly DependencyProperty OffBrushProperty = Register(nameof(OffBrush), typeof(SolidColorBrush), null);
    public static readonly DependencyProperty MergeProperty = Register(nameof(Merge), typeof(DotMerge), DotMerge.None);

    private (SolidColorBrush Brush, long Token)? _onWatch;
    private (SolidColorBrush Brush, long Token)? _offWatch;
    private ShapeVisual? _visual;
    private CompositionColorBrush? _onBrush;
    private CompositionColorBrush? _offBrush;
    private CompositionSpriteShape[] _dots = [];
    private bool[] _lit = [];
    private bool[]? _cells;
    private TextBlock? _icon;

    /// <summary>
    /// Iconos de Segoe Fluent Icons que sustituyen a los glifos de puntos con nombre (rediseño estilo isla).
    /// Las celdas sueltas (mascota, ecualizador) siguen siendo puntos.
    /// </summary>
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["musica"] = "", ["llamada"] = "", ["general"] = "", ["brillo"] = "",
        ["bateria"] = "", ["wifi"] = "", ["cascos"] = "", ["luna"] = "",
        ["sonido"] = "", ["cara"] = "", ["linea"] = "", ["juego"] = "",
        ["ojo"] = "", ["menu"] = "", ["notas"] = "", ["tareas"] = "",
        ["micro"] = "", ["descarga"] = "", ["mensaje"] = "", ["claude"] = "",
        ["foco"] = "", ["inicio"] = "", ["copias"] = "", ["bandeja"] = "",
        ["sistema"] = "",
    };

    /// <summary>Tamaño del icono: el 82 % del lado de la matriz (se calcula sin depender de Width, que aún puede ser NaN).</summary>
    private double IconSize => Math.Max(1, Math.Round(Math.Min(Columns * DotSize + (Columns - 1) * Gap, Rows * DotSize + (Rows - 1) * Gap) * 0.82));

    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

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

    /// <summary>Color de los puntos encendidos que sigue a un pincel del tema (cambia con oscuro/claro).</summary>
    public SolidColorBrush? OnBrush { get => (SolidColorBrush?)GetValue(OnBrushProperty); set => SetValue(OnBrushProperty, value); }

    /// <summary>Color de los puntos apagados que sigue a un pincel del tema.</summary>
    public SolidColorBrush? OffBrush { get => (SolidColorBrush?)GetValue(OffBrushProperty); set => SetValue(OffBrushProperty, value); }

    /// <summary>
    /// Une los puntos encendidos en formas redondeadas (estilo isla): <see cref="DotMerge.Blobs"/> une bloques
    /// contiguos (ojos de la mascota) y <see cref="DotMerge.Columns"/> une cada columna en una barra (ecualizador).
    /// Los puntos apagados no se dibujan.
    /// </summary>
    public DotMerge Merge { get => (DotMerge)GetValue(MergeProperty); set => SetValue(MergeProperty, value); }

    /// <summary>Solo se dibujan los primeros N puntos (−1 = todos), p. ej. 25 minutos en filas de 17.</summary>
    public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }

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
        if (e.Property == OnColorProperty)
        {
            if (m._onBrush is not null) m._onBrush.Color = m.OnColor;
            if (m._icon is not null) m._icon.Foreground = new SolidColorBrush(m.OnColor);
        }
        else if (e.Property == OffColorProperty) { if (m._offBrush is not null) m._offBrush.Color = m.OffColor; }
        else if (e.Property == OnBrushProperty) m._onWatch = Watch(m._onWatch, m.OnBrush, c => m.OnColor = c);
        else if (e.Property == OffBrushProperty) m._offWatch = Watch(m._offWatch, m.OffBrush, c => m.OffColor = c);
        else if (e.Property == GlyphProperty) { m._cells = null; m.ApplyCells(); }
        else m.Rebuild();
    }

    /// <summary>Copia el color del pincel y lo sigue cuando cambia (tema).</summary>
    internal static (SolidColorBrush, long)? Watch((SolidColorBrush Brush, long Token)? old, SolidColorBrush? brush, Action<Color> apply)
    {
        if (old is { } o) o.Brush.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, o.Token);
        if (brush is null) return null;
        apply(brush.Color);
        var token = brush.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, (s, _) => apply(((SolidColorBrush)s).Color));
        return (brush, token);
    }

    private void Rebuild()
    {
        var size = (float)DotSize;
        var step = (float)(DotSize + Gap);
        Width = Columns * DotSize + (Columns - 1) * Gap;
        Height = Rows * DotSize + (Rows - 1) * Gap;
        if (_icon is not null) _icon.FontSize = IconSize;
        if (!IsLoaded) return;

        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _onBrush ??= compositor.CreateColorBrush(OnColor);
        _offBrush ??= compositor.CreateColorBrush(OffColor);
        _visual ??= compositor.CreateShapeVisual();
        _visual.Size = new Vector2((float)Width, (float)Height);
        _visual.Shapes.Clear();
        _shapes.Clear();
        _shapeKey = 0;
        if (Merge != DotMerge.None)
        {
            _dots = [];
            ElementCompositionPreview.SetElementChildVisual(this, _visual);
            ApplyCells();
            return;
        }

        _dots = new CompositionSpriteShape[Count >= 0 ? Math.Min(Count, Columns * Rows) : Columns * Rows];
        _lit = new bool[_dots.Length];
        for (var i = 0; i < _dots.Length; i++)
        {
            var geometry = compositor.CreateEllipseGeometry();
            geometry.Center = new Vector2(size / 2);
            geometry.Radius = new Vector2(size / 2);
            var dot = compositor.CreateSpriteShape(geometry);
            dot.Offset = new Vector2(i % Columns * step, i / Columns * step);
            dot.FillBrush = _offBrush;
            _visual.Shapes.Add(dot);
            _dots[i] = dot;
        }
        ElementCompositionPreview.SetElementChildVisual(this, _visual);
        ApplyCells();
    }

    /// <summary>
    /// Solo cambia el pincel de los puntos que cambian: el ecualizador y la mascota redibujan varias veces por segundo
    /// y cada asignación cruza a Composition (CPU y objetos que esperan al GC).
    /// </summary>
    private void ApplyCells()
    {
        if (_cells is null && Glyph is { Length: > 0 } iconName && Icons.TryGetValue(iconName, out var code))
        {
            if (_icon is null)
            {
                _icon = new TextBlock
                {
                    FontFamily = IconFont,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextLineBounds = TextLineBounds.Tight,
                };
                Children.Add(_icon);
            }
            _icon.FontSize = IconSize;
            _icon.Foreground = new SolidColorBrush(OnColor);
            _icon.Text = code;
            _icon.Visibility = Visibility.Visible;
            if (_visual is not null) _visual.IsVisible = false;
            return;
        }
        if (_icon is not null) _icon.Visibility = Visibility.Collapsed;
        if (_visual is not null) _visual.IsVisible = true;
        if (Merge != DotMerge.None)
        {
            if (_visual is not null) ApplyMerged(_cells ?? (Glyph is { Length: > 0 } n ? Glyphs.Get(n) : null));
            return;
        }
        if (_dots.Length == 0 || _onBrush is null) return;
        var cells = _cells ?? (Glyph is { Length: > 0 } name ? Glyphs.Get(name) : null);
        for (var i = 0; i < _dots.Length; i++)
        {
            var lit = cells is not null && i < cells.Length && cells[i];
            if (lit == _lit[i]) continue;
            _lit[i] = lit;
            _dots[i].FillBrush = lit ? _onBrush : _offBrush;
        }
    }
    private readonly List<(CompositionSpriteShape Shape, CompositionRoundedRectangleGeometry Geometry)> _shapes = new();
    private int _shapeKey;

    /// <summary>Rectángulos (columna, fila, ancho, alto en celdas) de los puntos encendidos unidos.</summary>
    private List<(int C, int R, int W, int H)> Rects(bool[]? cells)
    {
        var rects = new List<(int, int, int, int)>();
        if (cells is null) return rects;
        var count = Math.Min(cells.Length, Columns * Rows);
        if (Merge == DotMerge.Columns)
        {
            for (var c = 0; c < Columns; c++)
            {
                var r = 0;
                while (r < Rows)
                {
                    if (!(r * Columns + c < count && cells[r * Columns + c])) { r++; continue; }
                    var start = r;
                    while (r < Rows && r * Columns + c < count && cells[r * Columns + c]) r++;
                    rects.Add((c, start, 1, r - start));
                }
            }
            return rects;
        }
        // Bloques: componentes conexas (4 vecinos) → su rectángulo envolvente.
        var seen = new bool[count];
        var stack = new Stack<int>();
        for (var i = 0; i < count; i++)
        {
            if (!cells[i] || seen[i]) continue;
            int minC = int.MaxValue, minR = int.MaxValue, maxC = -1, maxR = -1;
            stack.Push(i);
            seen[i] = true;
            while (stack.Count > 0)
            {
                var j = stack.Pop();
                int c = j % Columns, r = j / Columns;
                minC = Math.Min(minC, c); maxC = Math.Max(maxC, c); minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
                foreach (var k in new[] { c > 0 ? j - 1 : -1, c < Columns - 1 ? j + 1 : -1, j - Columns, j + Columns })
                    if (k >= 0 && k < count && cells[k] && !seen[k]) { seen[k] = true; stack.Push(k); }
            }
            rects.Add((minC, minR, maxC - minC + 1, maxR - minR + 1));
        }
        return rects;
    }

    /// <summary>
    /// Dibuja las formas unidas. Las que ya existían se animan hasta su nueva posición y tamaño (parpadeo,
    /// mirada, barras del ecualizador); con animaciones reducidas cambian sin transición.
    /// </summary>
    private void ApplyMerged(bool[]? cells)
    {
        var rects = Rects(cells);
        var hash = new HashCode();
        foreach (var r in rects) hash.Add(r);
        var key = hash.ToHashCode();
        if (key == _shapeKey && _shapes.Count == rects.Count) return;
        _shapeKey = key;

        var compositor = _visual!.Compositor;
        _onBrush ??= compositor.CreateColorBrush(OnColor);
        var size = (float)DotSize;
        var step = (float)(DotSize + Gap);
        var animate = !Motion.Reduced && _shapes.Count > 0;
        var duration = TimeSpan.FromMilliseconds(Merge == DotMerge.Columns ? 90 : 140);

        while (_shapes.Count > rects.Count)
        {
            _visual.Shapes.Remove(_shapes[^1].Shape);
            _shapes.RemoveAt(_shapes.Count - 1);
        }
        for (var i = 0; i < rects.Count; i++)
        {
            var (c, r, w, h) = rects[i];
            var offset = new Vector2(c * step, r * step);
            var box = new Vector2(w * size + (w - 1) * (float)Gap, h * size + (h - 1) * (float)Gap);
            var radius = new Vector2(Math.Min(box.X, box.Y) / 2);
            if (i >= _shapes.Count)
            {
                var geometry = compositor.CreateRoundedRectangleGeometry();
                geometry.Size = box;
                geometry.CornerRadius = radius;
                var shape = compositor.CreateSpriteShape(geometry);
                shape.FillBrush = _onBrush;
                shape.Offset = offset;
                _visual.Shapes.Add(shape);
                _shapes.Add((shape, geometry));
                continue;
            }
            var (s, g) = _shapes[i];
            if (animate)
            {
                Animate(s, "Offset", offset, duration);
                Animate(g, "Size", box, duration);
                Animate(g, "CornerRadius", radius, duration);
            }
            else
            {
                s.Offset = offset;
                g.Size = box;
                g.CornerRadius = radius;
            }
        }
    }

    private static void Animate(CompositionObject target, string property, Vector2 to, TimeSpan duration)
    {
        var c = target.Compositor;
        var a = c.CreateVector2KeyFrameAnimation();
        a.InsertKeyFrame(1f, to, c.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0f), new Vector2(0.2f, 1f)));
        a.Duration = duration;
        target.StartAnimation(property, a);
    }
}

/// <summary>Cómo se unen los puntos encendidos de una <see cref="DotMatrix"/>.</summary>
public enum DotMerge { None, Blobs, Columns }
