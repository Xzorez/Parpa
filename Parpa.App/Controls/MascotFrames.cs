namespace Parpa.Controls;

/// <summary>Fotogramas de la mascota (matriz 13×5). Fase 1: despierta y quieta; la animación llega en la fase 2.</summary>
public static class MascotFrames
{
    public const int Columns = 13;
    public const int Rows = 5;

    /// <summary>Despierta: dos ojos de 2×3 en las columnas 3 y 8.</summary>
    /// <param name="look">Desplazamiento de la mirada (−1, 0, +1).</param>
    /// <param name="blink">Parpadeo: solo queda la fila central.</param>
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

    private static void Set(bool[] cells, int row, int col)
    {
        if (row is >= 0 and < Rows && col is >= 0 and < Columns) cells[row * Columns + col] = true;
    }
}
