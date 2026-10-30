using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace Parpa.Services;

/// <summary>
/// Capturas nuevas: Windows guarda las de Win+Mayús+S, Impr Pant y Win+Impr Pant en Imágenes › Capturas de pantalla
/// (si "Guardar capturas automáticamente" de Recortes está activado, como viene de serie). Se mira esa carpeta.
/// </summary>
public sealed class ScreenshotWatcher : IDisposable
{
    private static readonly Guid ScreenshotsFolder = new("b7bede81-df94-4682-a7d8-57a52620b86f");
    private static readonly TimeSpan WriteWait = TimeSpan.FromSeconds(5);

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;

    /// <summary>Ruta de la captura, ya escrita del todo (en el hilo de la interfaz).</summary>
    public event Action<string>? Taken;

    public void Start()
    {
        var folder = Folder();
        if (folder is null || !Directory.Exists(folder))
        {
            Log.Info("Capturas: no hay carpeta de capturas");
            return;
        }
        _watcher = new FileSystemWatcher(folder) { NotifyFilter = NotifyFilters.FileName, IncludeSubdirectories = false };
        _watcher.Created += (_, e) => OnFile(e.FullPath);
        _watcher.Renamed += (_, e) => OnFile(e.FullPath); // algunas apps escriben un temporal y lo renombran
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose() => _watcher?.Dispose();

    private void OnFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg")) return;
        lock (_seen)
        {
            if (!_seen.Add(path)) return;
        }
        _ = RaiseWhenWrittenAsync(path);
    }

    /// <summary>Recortes crea el archivo y lo sigue escribiendo un momento: se espera a poder abrirlo sin nadie más.</summary>
    private async Task RaiseWhenWrittenAsync(string path)
    {
        var until = DateTime.UtcNow + WriteWait;
        while (DateTime.UtcNow < until)
        {
            try
            {
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                _dispatcher.TryEnqueue(() => Taken?.Invoke(path));
                return;
            }
            catch (IOException)
            {
                await Task.Delay(150);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private static string? Folder()
    {
        if (SHGetKnownFolderPath(ScreenshotsFolder, 0, IntPtr.Zero, out var pointer) != 0) return null;
        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);
}
