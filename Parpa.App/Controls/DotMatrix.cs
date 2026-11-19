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
            if (m._onBrush is not null)
            {
                // Cambio de color suave (la mascota pasa a gris al dormirse y vuelve al rosa al despertar).
                if (m.IsLoaded && !Motion.Reduced && m._onBrush.Color != m.OnColor)
                {
                    var fade = m._onBrush.Compositor.CreateColorKeyFrameAnimation();
                    fade.InsertKeyFrame(1f, m.OnColor);
                    fade.Duration = TimeSpan.FromMilliseconds(320);
                    m._onBrush.StartAnimation("Color", fade);
                }
                else
                {
                    m._onBrush.StopAnimation("Color");
                    m._onBrush.Color = m.OnColor;
                }
            }
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
    /// <summary>Forma unida ya dibujada: su sprite, su geometría y dónde quedó (para emparejarla con la siguiente).</summary>
    private sealed class Piece(CompositionSpriteShape shape, CompositionRoundedRectangleGeometry geometry)
    {
        public CompositionSpriteShape Shape { get; } = shape;
        public CompositionRoundedRectangleGeometry Geometry { get; } = geometry;
        public Blob At { get; set; }
    }

    /// <summary>Píldora: centro, tamaño (largo × grosor) y giro en grados alrededor de su centro.</summary>
    private readonly record struct Blob(Vector2 Center, Vector2 Box, float Angle);

    private readonly List<Piece> _shapes = new();
    private int _shapeKey;

    /// <summary>
    /// Formas de los puntos encendidos unidos. Columns: cada tramo de columna es una barra (ecualizador).
    /// Blobs: cada bloque contiguo (4 vecinos) es una píldora, y los puntos sueltos que se tocan en diagonal se unen
    /// con un trazo girado; así los ojos contentos de la mascota son dos trazos en "^" y no tres puntos sueltos.
    /// </summary>
    private List<Blob> Blobs(bool[]? cells)
    {
        var blobs = new List<Blob>();
        if (cells is null) return blobs;
        var count = Math.Min(cells.Length, Columns * Rows);
        var size = (float)DotSize;
        var gap = (float)Gap;
        var step = size + gap;
        Blob Rect(int c, int r, int w, int h)
        {
            var box = new Vector2(w * size + (w - 1) * gap, h * size + (h - 1) * gap);
            return new Blob(new Vector2(c * step, r * step) + box / 2, box, 0);
        }
        bool Lit(int c, int r) => c >= 0 && c < Columns && r >= 0 && r < Rows && r * Columns + c < count && cells[r * Columns + c];

        if (Merge == DotMerge.Columns)
        {
            for (var c = 0; c < Columns; c++)
            {
                var r = 0;
                while (r < Rows)
                {
                    if (!Lit(c, r)) { r++; continue; }
                    var start = r;
                    while (Lit(c, r)) r++;
                    blobs.Add(Rect(c, start, 1, r - start));
                }
            }
            return blobs;
        }

        // Bloques: componentes conexas (4 vecinos) → su rectángulo envolvente. Los de un solo punto se apartan.
        var seen = new bool[count];
        var stack = new Stack<int>();
        var singles = new List<(int C, int R)>();
        for (var i = 0; i < count; i++)
        {
            if (!cells[i] || seen[i]) continue;
            int minC = int.MaxValue, minR = int.MaxValue, maxC = -1, maxR = -1, n = 0;
            stack.Push(i);
            seen[i] = true;
            while (stack.Count > 0)
            {
                var j = stack.Pop();
                n++;
                int c = j % Columns, r = j / Columns;
                minC = Math.Min(minC, c); maxC = Math.Max(maxC, c); minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
                foreach (var k in new[] { c > 0 ? j - 1 : -1, c < Columns - 1 ? j + 1 : -1, j - Columns, j + Columns })
                    if (k >= 0 && k < count && cells[k] && !seen[k]) { seen[k] = true; stack.Push(k); }
            }
            if (n == 1) singles.Add((minC, minR));
            else blobs.Add(Rect(minC, minR, maxC - minC + 1, maxR - minR + 1));
        }

        // Puntos sueltos: un trazo por cada pareja en diagonal; los que no tienen pareja quedan como punto.
        var set = singles.ToHashSet();
        var joined = new HashSet<(int, int)>();
        foreach (var (c, r) in singles)
            foreach (var dc in new[] { -1, 1 })
            {
                if (!set.Contains((c + dc, r + 1))) continue;
                var a = new Vector2(c * step + size / 2, r * step + size / 2);
                var b = new Vector2((c + dc) * step + size / 2, (r + 1) * step + size / 2);
                var angle = MathF.Atan2(b.Y - a.Y, b.X - a.X) * 180f / MathF.PI;
                if (angle > 90) angle -= 180; // mismo trazo, giro corto (−90…90) para que no dé vueltas al animarse
                blobs.Add(new Blob((a + b) / 2, new Vector2(Vector2.Distance(a, b) + size, size), angle));
                joined.Add((c, r));
                joined.Add((c + dc, r + 1));
            }
        foreach (var single in singles)
            if (!joined.Contains(single)) blobs.Add(Rect(single.C, single.R, 1, 1));
        return blobs;
    }

    /// <summary>
    /// Dibuja las formas unidas. Cada forma nueva se empareja con la existente más cercana y se transforma hasta su
    /// nueva posición, tamaño y giro (parpadeo, mirada, ojos contentos, barras del ecualizador); las que sobran se
    /// encogen hasta desaparecer y las que faltan crecen desde su sitio. Con animaciones reducidas, sin transición.
    /// </summary>
    private void ApplyMerged(bool[]? cells)
    {
        var blobs = Blobs(cells);
        var hash = new HashCode();
        foreach (var b in blobs) hash.Add(b);
        var key = hash.ToHashCode();
        if (key == _shapeKey && _shapes.Count == blobs.Count) return;
        _shapeKey = key;

        var compositor = _visual!.Compositor;
        _onBrush ??= compositor.CreateColorBrush(OnColor);
        var animate = !Motion.Reduced && _shapes.Count > 0;
        var duration = TimeSpan.FromMilliseconds(Merge == DotMerge.Columns ? 90 : 200);

        // Emparejado voraz por cercanía: primero las parejas más cercanas.
        var pairs = new List<(float D, int Old, int New)>();
        for (var o = 0; o < _shapes.Count; o++)
            for (var n = 0; n < blobs.Count; n++)
                pairs.Add((Vector2.DistanceSquared(_shapes[o].At.Center, blobs[n].Center), o, n));
        pairs.Sort((x, y) => x.D.CompareTo(y.D));
        var oldFor = new int[blobs.Count];
        Array.Fill(oldFor, -1);
        var usedOld = new bool[_shapes.Count];
        foreach (var (_, o, n) in pairs)
        {
            if (usedOld[o] || oldFor[n] >= 0) continue;
            usedOld[o] = true;
            oldFor[n] = o;
        }

        var next = new List<Piece>(blobs.Count);
        for (var n = 0; n < blobs.Count; n++)
        {
            if (oldFor[n] >= 0)
            {
                var piece = _shapes[oldFor[n]];
                Place(piece, blobs[n], animate ? duration : null);
                next.Add(piece);
                continue;
            }
            var geometry = compositor.CreateRoundedRectangleGeometry();
            var shape = compositor.CreateSpriteShape(geometry);
            shape.FillBrush = _onBrush;
            var created = new Piece(shape, geometry);
            Place(created, blobs[n], null);
            _visual.Shapes.Add(shape);
            if (animate) Grow(shape, 0f, 1f, duration);
            next.Add(created);
        }

        for (var o = 0; o < _shapes.Count; o++)
        {
            if (usedOld[o]) continue;
            var dying = _shapes[o].Shape;
            if (!animate)
            {
                _visual.Shapes.Remove(dying);
                continue;
            }
            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            Grow(dying, 1f, 0f, duration);
            batch.End();
            var visual = _visual;
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            batch.Completed += (_, _) => dispatcher.TryEnqueue(() => visual.Shapes.Remove(dying));
        }
        _shapes.Clear();
        _shapes.AddRange(next);
    }

    /// <summary>Lleva una forma a su píldora (animada si se da duración).</summary>
    private static void Place(Piece piece, Blob blob, TimeSpan? duration)
    {
        var half = blob.Box / 2;
        var radius = new Vector2(Math.Min(blob.Box.X, blob.Box.Y) / 2);
        var offset = blob.Center - half;
        if (duration is { } d)
        {
            Animate(piece.Shape, "Offset", offset, d);
            Animate(piece.Shape, "CenterPoint", half, d);
            AnimateScalar(piece.Shape, "RotationAngleInDegrees", blob.Angle, d);
            Animate(piece.Geometry, "Size", blob.Box, d);
            Animate(piece.Geometry, "CornerRadius", radius, d);
        }
        else
        {
            piece.Shape.Offset = offset;
            piece.Shape.CenterPoint = half;
            piece.Shape.RotationAngleInDegrees = blob.Angle;
            piece.Geometry.Size = blob.Box;
            piece.Geometry.CornerRadius = radius;
        }
        piece.At = blob;
    }

    /// <summary>Crece o encoge una forma desde su centro.</summary>
    private static void Grow(CompositionSpriteShape shape, float from, float to, TimeSpan duration)
    {
        var c = shape.Compositor;
        var a = c.CreateVector2KeyFrameAnimation();
        a.InsertKeyFrame(0f, new Vector2(from));
        a.InsertKeyFrame(1f, new Vector2(to), Ease(c));
        a.Duration = duration;
        shape.StartAnimation("Scale", a);
    }

    private static CompositionEasingFunction Ease(Compositor c) =>
        c.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0f), new Vector2(0.2f, 1f));

    private static void Animate(CompositionObject target, string property, Vector2 to, TimeSpan duration)
    {
        var a = target.Compositor.CreateVector2KeyFrameAnimation();
        a.InsertKeyFrame(1f, to, Ease(target.Compositor));
        a.Duration = duration;
        target.StartAnimation(property, a);
    }

    private static void AnimateScalar(CompositionObject target, string property, float to, TimeSpan duration)
    {
        var a = target.Compositor.CreateScalarKeyFrameAnimation();
        a.InsertKeyFrame(1f, to, Ease(target.Compositor));
        a.Duration = duration;
        target.StartAnimation(property, a);
    }
}

/// <summary>Cómo se unen los puntos encendidos de una <see cref="DotMatrix"/>.</summary>
public enum DotMerge { None, Blobs, Columns }
