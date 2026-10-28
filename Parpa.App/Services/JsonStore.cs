using System.Text.Json;
using Microsoft.UI.Dispatching;

namespace Parpa.Services;

/// <summary>
/// Lista persistida en %APPDATA%\Parpa\&lt;nombre&gt;.json (notas, tareas, portapapeles, bandeja).
/// Guarda con un pequeño retraso para agrupar cambios seguidos.
/// </summary>
public sealed class JsonStore<T>
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly string _path;
    private readonly DispatcherQueueTimer _saveTimer;

    public List<T> Items { get; }

    public JsonStore(string name)
    {
        _path = Path.Combine(DataFiles.UserDir, name + ".json");
        Items = Load();
        _saveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _saveTimer.IsRepeating = false;
        _saveTimer.Interval = SaveDelay;
        _saveTimer.Tick += (_, _) => SaveNow();
    }

    private List<T> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(_path), DataFiles.JsonOptions) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Log.Error($"Datos: no se pudo leer {Path.GetFileName(_path)}", ex);
        }
        return new();
    }

    /// <summary>Programa el guardado (se agrupan los cambios de medio segundo).</summary>
    public void Save()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        try
        {
            Directory.CreateDirectory(DataFiles.UserDir);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Items, DataFiles.JsonOptions));
            File.Move(temp, _path, overwrite: true);
        }
        catch (IOException ex)
        {
            Log.Error($"Datos: no se pudo guardar {Path.GetFileName(_path)}", ex);
        }
    }
}
