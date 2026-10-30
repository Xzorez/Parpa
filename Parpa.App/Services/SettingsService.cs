using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Parpa.Services;

/// <summary>
/// %APPDATA%\Parpa\settings.json con el esquema de Data/settings.default.json
/// (<c>s</c> = ajustes, <c>wins</c> = orden/visibilidad de ventanas).
/// </summary>
public sealed class SettingsService
{
    public static SettingsService Current { get; private set; } = null!;

    public string FilePath { get; } = Path.Combine(DataFiles.UserDir, "settings.json");

    public ParpaSettings S { get; private set; } = new();

    public JsonArray Wins { get; private set; } = new();

    public event Action? Changed;

    public static void Load()
    {
        var service = new SettingsService();
        service.Read();
        Current = service;
    }

    private void Read()
    {
        // Los valores por defecto salen del JSON de diseño; el archivo del usuario se superpone clave a clave.
        var merged = DataFiles.ReadData("settings.default.json").AsObject();
        var userExists = File.Exists(FilePath);
        if (userExists)
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllText(FilePath)) is JsonObject user) Merge(merged, user);
            }
            catch (JsonException)
            {
                // Archivo dañado: se conservan los valores por defecto y se reescribe al guardar.
                userExists = false;
            }
        }

        S = merged["s"].Deserialize<ParpaSettings>(DataFiles.JsonOptions) ?? new ParpaSettings();
        S.HomeCards.Remove("FOCO"); // tarjeta retirada en 1.0.4
        Wins = merged["wins"]?.AsArray() ?? new JsonArray();
        if (!userExists) Save();
    }

    /// <summary>Cambia ajustes y los aplica al instante (todas las vistas escuchan <see cref="Changed"/>).</summary>
    public void Update(Action<ParpaSettings> change)
    {
        change(S);
        Save();
    }

    public void Save()
    {
        Directory.CreateDirectory(DataFiles.UserDir);
        var root = new JsonObject
        {
            ["s"] = JsonSerializer.SerializeToNode(S, DataFiles.JsonOptions),
            ["wins"] = Wins.DeepClone(),
        };
        File.WriteAllText(FilePath, root.ToJsonString(DataFiles.JsonOptions));
        Changed?.Invoke();
    }

    /// <summary>Contenido de settings.json (para exportar).</summary>
    public string Export() => File.Exists(FilePath) ? File.ReadAllText(FilePath) : string.Empty;

    /// <summary>
    /// Importa un settings.json exportado. Rellena los objetos actuales (los servicios guardan referencias a ellos).
    /// </summary>
    /// <returns><c>false</c> si el archivo no es válido.</returns>
    public bool Import(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject user || user["s"] is not JsonObject) return false;
            var merged = DataFiles.ReadData("settings.default.json").AsObject();
            Merge(merged, user);
            var imported = merged["s"].Deserialize<ParpaSettings>(DataFiles.JsonOptions);
            if (imported is null) return false;
            imported.HomeCards.Remove("FOCO");
            foreach (var property in typeof(ParpaSettings).GetProperties().Where(p => p.CanWrite))
                property.SetValue(S, property.GetValue(imported));
            Wins.Clear();
            foreach (var entry in merged["wins"]?.AsArray() ?? []) Wins.Add(entry?.DeepClone());
            Save();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            if (value is JsonObject obj && target[key] is JsonObject existing) Merge(existing, obj);
            else target[key] = value?.DeepClone();
        }
    }
}

/// <summary>Bloque <c>s</c> de settings.json. Los nombres coinciden con el prototipo (localStorage "punto.settings.v1").</summary>
public sealed class ParpaSettings
{
    [JsonPropertyName("startup")] public bool Startup { get; set; } = true;
    [JsonPropertyName("keys")] public List<string> Keys { get; set; } = ["WIN", "ALT", "N"];
    [JsonPropertyName("wheel")] public bool Wheel { get; set; } = true;
    [JsonPropertyName("closeOn")] public string CloseOn { get; set; } = "mouse";
    [JsonPropertyName("closeDelay")] public double CloseDelay { get; set; } = 0.4;
    [JsonPropertyName("noFocus")] public bool NoFocus { get; set; } = true;
    [JsonPropertyName("hoverSens")] public double HoverSens { get; set; } = 6;
    [JsonPropertyName("openTime")] public double OpenTime { get; set; } = 250;
    [JsonPropertyName("hideIdle")] public bool HideIdle { get; set; }
    [JsonPropertyName("dnd")] public bool Dnd { get; set; }
    [JsonPropertyName("dndFrom")] public string DndFrom { get; set; } = "23:00";
    [JsonPropertyName("monitor")] public int Monitor { get; set; } = 1;
    [JsonPropertyName("pos")] public string Pos { get; set; } = "center";
    [JsonPropertyName("offset")] public double Offset { get; set; }
    [JsonPropertyName("size")] public double Size { get; set; } = 100;
    [JsonPropertyName("minimal")] public bool Minimal { get; set; }
    [JsonPropertyName("hideFull")] public bool HideFull { get; set; } = true;
    [JsonPropertyName("theme")] public string Theme { get; set; } = "dark";
    [JsonPropertyName("accent")] public int Accent { get; set; }
    [JsonPropertyName("mascot")] public bool Mascot { get; set; } = true;
    [JsonPropertyName("mood")] public string Mood { get; set; } = "curiosa";
    [JsonPropertyName("anim")] public string Anim { get; set; } = "full";
    [JsonPropertyName("clock")] public bool Clock { get; set; } = true;
    [JsonPropertyName("h24")] public bool H24 { get; set; } = true;
    [JsonPropertyName("lang")] public string Lang { get; set; } = "auto";
    [JsonPropertyName("apps")] public Dictionary<string, bool> Apps { get; set; } = new();
    [JsonPropertyName("content")] public string Content { get; set; } = "full";
    [JsonPropertyName("notifDur")] public double NotifDur { get; set; } = 4.5;
    [JsonPropertyName("quickReply")] public bool QuickReply { get; set; } = true;
    /// <summary>Sin uso: Windows no expone la letra de lo que suena (Inicio queda siempre a 176 px). Se conserva por compatibilidad del esquema.</summary>
    [JsonPropertyName("lyrics")] public bool Lyrics { get; set; } = true;
    /// <summary>Tarjeta "No puedo leer tus avisos" ocultada por el usuario en Inicio.</summary>
    [JsonPropertyName("notifCardHidden")] public bool NotifCardHidden { get; set; }
    /// <summary>Apps desde las que no se guarda lo copiado (Ajustes › Ventanas › Portapapeles).</summary>
    [JsonPropertyName("clipExclude")] public Dictionary<string, bool> ClipExclude { get; set; } = new() { ["1Password"] = true, ["Bitwarden"] = true, ["KeePassXC"] = false };
    /// <summary>La bandeja empieza vacía cada vez que se abre Parpa.</summary>
    [JsonPropertyName("shelfClear")] public bool ShelfClear { get; set; }
    /// <summary>Bienvenida completada (no se vuelve a mostrar al arrancar).</summary>
    [JsonPropertyName("welcomeDone")] public bool WelcomeDone { get; set; }
    /// <summary>Avisos en pausa hasta esta hora (menú de bandeja: "Pausar avisos 1 hora").</summary>
    [JsonPropertyName("pausedUntil")] public DateTime? PausedUntil { get; set; }

    /// <summary>No molestar activo: el ajuste o una pausa temporal en curso.</summary>
    [JsonIgnore] public bool DndActive => Dnd || PausedUntil > DateTime.Now;
    [JsonPropertyName("micAlways")] public bool MicAlways { get; set; } = true;
    [JsonPropertyName("homeCards")] public Dictionary<string, bool> HomeCards { get; set; } = new();
    [JsonPropertyName("pomo")] public int Pomo { get; set; } = 25;
    [JsonPropertyName("brk")] public int Brk { get; set; } = 5;
    [JsonPropertyName("longBrk")] public int LongBrk { get; set; } = 15;
    [JsonPropertyName("clipLimit")] public int ClipLimit { get; set; } = 50;
    [JsonPropertyName("actions")] public Dictionary<string, bool> Actions { get; set; } = new();
    [JsonPropertyName("sounds")] public bool Sounds { get; set; } = true;
    [JsonPropertyName("vol")] public int Vol { get; set; } = 60;
    [JsonPropertyName("osd")] public bool Osd { get; set; } = true;
    [JsonPropertyName("battery")] public bool Battery { get; set; } = true;
    [JsonPropertyName("wifi")] public bool Wifi { get; set; } = true;
    [JsonPropertyName("buds")] public bool Buds { get; set; } = true;
    [JsonPropertyName("lowPower")] public bool LowPower { get; set; } = true;
}
