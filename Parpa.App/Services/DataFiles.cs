using System.Text.Json;
using System.Text.Json.Nodes;

namespace Parpa.Services;

/// <summary>Rutas de los JSON de diseño (Data/) y de la carpeta de usuario (%APPDATA%\Parpa).</summary>
public static class DataFiles
{
    public static string DataDir { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

    public static string UserDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Parpa");

    public static JsonNode ReadData(string fileName) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, fileName)))
        ?? throw new InvalidDataException(fileName);

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
