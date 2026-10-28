namespace Parpa.Controls;

/// <summary>
/// Mascota procedural 13×5, en ticks de 100 ms (como el prototipo).
/// Modos (curMode): despierta (mira a los lados, parpadea), dormida (ojos cerrados + "z") y contenta ("^" que botan).
/// La personalidad (<c>mood</c>) decide el ciclo cuando no pasa nada.
/// </summary>
public static class MascotFrames
{
    public const int Columns = 13;
    public const int Rows = 5;

    public enum Mode { Awake, Sleep, Happy }

    public readonly record struct Frame(bool[] Cells, Mode Mode)
    {
        /// <summary>Clave para saber si dos fotogramas se ven igual.</summary>
        public int Key
        {
            get
            {
                var hash = new HashCode();
                hash.Add(Mode);
                foreach (var cell in Cells) hash.Add(cell);
                return hash.ToHashCode();
            }
        }
    }

    /// <summary>
    /// curiosa: ciclo del prototipo (6 s despierta mirando a los lados, 6 s dormida, 3 s contenta).
    /// tranquila: siempre despierta, sin mirar a los lados. dormilona: 6 s despierta, 6 s dormida.
    /// Animaciones reducidas: despierta y quieta.
    /// </summary>
    public static Frame At(long tick, string mood, bool reduced)
    {
        if (reduced) return new Frame(Awake(0, false), Mode.Awake);
        var mode = mood switch
        {
            "tranquila" => Mode.Awake,
            "dormilona" => tick % 120 < 60 ? Mode.Awake : Mode.Sleep,
            _ => (tick % 150) switch { < 60 => Mode.Awake, < 120 => Mode.Sleep, _ => Mode.Happy },
        };
        return mode switch
        {
            Mode.Sleep => new Frame(Sleep(tick), mode),
            Mode.Happy => new Frame(Happy(tick), mode),
            _ => new Frame(Awake(mood == "curiosa" ? Look(tick) : 0, tick % 45 < 2), mode),
        };
    }

    /// <summary>Mirada en ciclos de 8 s: derecha (4–5,5 s), izquierda (6,2–7,2 s).</summary>
    private static int Look(long tick) => (tick % 80) switch { >= 40 and < 55 => 1, >= 62 and < 72 => -1, _ => 0 };

    /// <summary>Despierta: dos ojos de 2×3 en las columnas 3 y 8. Parpadeo: solo la fila central.</summary>
    public static bool[] Awake(int look = 0, bool blink = false)
    {
        var cells = new bool[Columns * Rows];
        foreach (var x in new[] { 3, 8 })
        {
            for (var r = 1; r <= 3; r++)
            {
                if (blink && r != 2) continue;
                Set(cells, r, x + look);
                Set(cells, r, x + 1 + look);
            }
        }
        return cells;
    }

    /// <summary>Mira hacia una burbuja del menú: desplazamiento horizontal −2…2 y una fila abajo si está debajo.</summary>
    public static bool[] Looking(int column, int row)
    {
        var cells = new bool[Columns * Rows];
        foreach (var x in new[] { 3, 8 })
            for (var r = 1; r <= 3; r++)
            {
                Set(cells, r + row, x + column);
                Set(cells, r + row, x + 1 + column);
            }
        return cells;
    }

    /// <summary>Sin mascota (ajuste desactivado): bloque de puntos 5×5 con las esquinas recortadas.</summary>
    public static bool[] Block()
    {
        var cells = new bool[Columns * Rows];
        for (var r = 0; r < Rows; r++)
            for (var c = 4; c <= 8; c++)
                if (!((r == 0 || r == 4) && (c == 4 || c == 8))) Set(cells, r, c);
        return cells;
    }

    /// <summary>Contenta y quieta ("^ ^"), para estados vacíos.</summary>
    public static bool[] HappyStill() => Happy(0);

    /// <summary>Contenta, abajo o una fila arriba (para botar al ritmo que se quiera).</summary>
    public static bool[] HappyAt(bool up) => Happy(up ? 3 : 0);

    /// <summary>Apagada (ojos cerrados, sin "z"): permiso de avisos denegado.</summary>
    public static bool[] Off()
    {
        var cells = new bool[Columns * Rows];
        foreach (var x in new[] { 3, 4, 8, 9 }) Set(cells, 3, x);
        return cells;
    }

    /// <summary>Dormida: ojos cerrados en la fila 3 y una "z" que sube por las columnas 11–12.</summary>
    private static bool[] Sleep(long tick)
    {
        var cells = new bool[Columns * Rows];
        foreach (var x in new[] { 3, 4, 8, 9 }) Set(cells, 3, x);
        var z = (int)(tick % 25 / 5);
        Set(cells, 3 - z, 11 + z % 2);
        return cells;
    }

    /// <summary>Contenta: ojos en "^" que botan una fila cada 0,3 s.</summary>
    private static bool[] Happy(long tick)
    {
        var cells = new bool[Columns * Rows];
        var b = tick % 6 < 3 ? 0 : -1;
        foreach (var x in new[] { 2, 8 })
        {
            Set(cells, 2 + b, x);
            Set(cells, 1 + b, x + 1);
            Set(cells, 2 + b, x + 2);
        }
        return cells;
    }

    private static void Set(bool[] cells, int row, int col)
    {
        if (row is >= 0 and < Rows && col is >= 0 and < Columns) cells[row * Columns + col] = true;
    }
}
