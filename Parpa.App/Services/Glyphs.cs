namespace Parpa.Services;

/// <summary>Glifos de puntos 7×7 de Data/glyphs.json (<c>X</c> = encendido).</summary>
public static class Glyphs
{
    private static readonly Dictionary<string, bool[]> Cells = new();

    public static int Size { get; private set; } = 7;

    public static void Load()
    {
        var root = DataFiles.ReadData("glyphs.json");
        var on = root["on"]!.GetValue<string>()[0];
        Size = int.Parse(root["grid"]!.GetValue<string>().Split('x')[0]);
        foreach (var (name, rows) in root["glyphs"]!.AsObject())
            Cells[name] = rows!.AsArray().SelectMany(r => r!.GetValue<string>()).Select(c => c == on).ToArray();
    }

    public static bool[] Get(string name) =>
        Cells.TryGetValue(name, out var cells) ? cells : throw new KeyNotFoundException($"Glifo «{name}» no existe en glyphs.json");
}
