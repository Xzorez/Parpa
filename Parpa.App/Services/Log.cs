namespace Parpa.Services;

/// <summary>Registro local de errores en %APPDATA%\Parpa\parpa.log (sin telemetría: no sale del equipo).</summary>
public static class Log
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine(DataFiles.UserDir, "parpa.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string context, Exception ex) => Write("ERROR", $"{context}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DataFiles.UserDir);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes) File.Delete(FilePath);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // El registro nunca debe tumbar la app.
        }
    }
}
