using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace Parpa.Services;

/// <summary>Descarga en curso: nombre (sin la extensión temporal) y bytes escritos hasta ahora.</summary>
public sealed record DownloadProgress(string Name, long Bytes, int Count);

/// <summary>
/// Descargas del navegador en la carpeta Descargas. Chrome, Edge, Brave y Opera escriben en <c>.crdownload</c>,
/// Firefox en <c>.part</c>; al terminar lo renombran al nombre final. Los navegadores no dejan el tamaño total en
/// el disco, así que se muestra lo que lleva bajado. Solo hay sondeo (cada 0,5 s) mientras hay una descarga.
/// </summary>
public sealed class DownloadWatcher : IDisposable
{
    private static readonly Guid DownloadsFolder = new("374de290-123f-4565-9164-39c4925e467b");
    private static readonly string[] TempExtensions = [".crdownload", ".part", ".partial", ".download", ".opdownload"];
    private static readonly TimeSpan WriteWait = TimeSpan.FromSeconds(10);

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _poll;
    private FileSystemWatcher? _watcher;
    private string? _folder;
    private DownloadProgress? _last;

    /// <summary>Progreso de la descarga más reciente, o null cuando ya no queda ninguna (en el hilo de la interfaz).</summary>
    public event Action<DownloadProgress?>? Progress;

    /// <summary>Ruta de un archivo recién descargado, ya cerrado por el navegador (en el hilo de la interfaz).</summary>
    public event Action<string>? Completed;

    public DownloadWatcher()
    {
        _poll = _dispatcher.CreateTimer();
        _poll.Interval = TimeSpan.FromMilliseconds(500);
        _poll.Tick += (_, _) => Scan();
    }

    public void Start()
    {
        _folder = Folder();
        if (_folder is null || !Directory.Exists(_folder))
        {
            Log.Info("Descargas: no hay carpeta de descargas");
            return;
        }
        _watcher = new FileSystemWatcher(_folder) { NotifyFilter = NotifyFilters.FileName, IncludeSubdirectories = false };
        _watcher.Created += (_, e) => { if (IsTemp(e.FullPath)) _dispatcher.TryEnqueue(Scan); };
        _watcher.Renamed += (_, e) =>
        {
            if (IsTemp(e.FullPath)) _dispatcher.TryEnqueue(Scan);
            else if (IsTemp(e.OldFullPath)) _ = RaiseWhenClosedAsync(e.FullPath);
        };
        _watcher.EnableRaisingEvents = true;
        Scan(); // por si ya había una a medias al arrancar
    }

    public void Dispose()
    {
        _poll.Stop();
        _watcher?.Dispose();
    }

    private static bool IsTemp(string path) =>
        TempExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Mira los temporales de la carpeta: el más nuevo manda; sin ninguno, se para el sondeo.</summary>
    private void Scan()
    {
        if (_folder is null) return;
        FileInfo[] temps;
        try
        {
            temps = new DirectoryInfo(_folder).EnumerateFiles()
                .Where(f => IsTemp(f.FullName) && DateTime.UtcNow - f.LastWriteTimeUtc < TimeSpan.FromMinutes(2))
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToArray();
        }
        catch (IOException)
        {
            return;
        }

        var progress = temps.Length == 0 ? null : new DownloadProgress(NameOf(temps[0]), temps[0].Length, temps.Length);
        if (progress is null) _poll.Stop();
        else _poll.Start();
        if (progress == _last) return;
        _last = progress;
        Progress?.Invoke(progress);
    }

    /// <summary>"Unconfirmed 123.crdownload" no dice nada: se queda en vacío. "foto.jpg.part" → "foto.jpg".</summary>
    private static string NameOf(FileInfo temp)
    {
        var name = Path.GetFileNameWithoutExtension(temp.Name);
        return name.StartsWith("Unconfirmed ", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".com.google", StringComparison.Ordinal)
            ? string.Empty
            : name;
    }

    /// <summary>El navegador renombra y puede seguir con el archivo abierto un momento (antivirus, marca de origen).</summary>
    private async Task RaiseWhenClosedAsync(string path)
    {
        var until = DateTime.UtcNow + WriteWait;
        while (DateTime.UtcNow < until)
        {
            try
            {
                if (!File.Exists(path)) return;
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { }
                _dispatcher.TryEnqueue(() =>
                {
                    Scan();
                    Completed?.Invoke(path);
                });
                return;
            }
            catch (IOException)
            {
                await Task.Delay(250);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private static string? Folder()
    {
        if (SHGetKnownFolderPath(DownloadsFolder, 0, IntPtr.Zero, out var pointer) != 0) return null;
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
