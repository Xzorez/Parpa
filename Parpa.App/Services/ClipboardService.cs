using System.Runtime.InteropServices;

namespace Parpa.Services;

public sealed record ClipEntry(string Text, DateTime Time);

/// <summary>
/// Historial del portapapeles (texto). Escucha WM_CLIPBOARDUPDATE con AddClipboardFormatListener,
/// que llega aunque Parpa no esté en primer plano. Respeta las marcas con las que los gestores de
/// contraseñas piden no guardar lo copiado. Límite <c>clipLimit</c> (50).
/// </summary>
public sealed class ClipboardService
{
    public const uint WM_CLIPBOARDUPDATE = 0x031D;
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    private static readonly uint ExcludeFormat = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint HistoryFormat = RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private static readonly uint ViewerIgnoreFormat = RegisterClipboardFormat("Clipboard Viewer Ignore");

    private readonly IntPtr _hwnd;
    private readonly ParpaSettings _s;
    private readonly JsonStore<ClipEntry> _store = new("clipboard");

    public IReadOnlyList<ClipEntry> Items => _store.Items;

    /// <summary>Algo nuevo en el historial (para el aviso "COPIADO").</summary>
    public event Action<ClipEntry>? Copied;

    public event Action? Changed;

    public ClipboardService(IntPtr hwnd, ParpaSettings settings)
    {
        _hwnd = hwnd;
        _s = settings;
    }

    public void Start()
    {
        if (!AddClipboardFormatListener(_hwnd)) Log.Info("Portapapeles: no se pudo escuchar");
    }

    /// <summary>Llamado desde el WndProc al recibir WM_CLIPBOARDUPDATE.</summary>
    public void OnClipboardUpdate()
    {
        // Lo que copia Parpa (volver a copiar del historial) no se repite.
        GetWindowThreadProcessId(GetClipboardOwner(), out var ownerPid);
        if (ownerPid == Environment.ProcessId) return;
        if (IsPrivate() || IsExcludedApp()) return;

        var text = ReadText()?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        Add(text);
    }

    /// <summary>Guarda texto en el historial (también enlaces soltados en el notch).</summary>
    public void Add(string text, bool announce = true)
    {
        _store.Items.RemoveAll(e => e.Text == text);
        var entry = new ClipEntry(text, DateTime.Now);
        _store.Items.Insert(0, entry);
        var limit = Math.Max(1, _s.ClipLimit);
        if (_store.Items.Count > limit) _store.Items.RemoveRange(limit, _store.Items.Count - limit);
        _store.Save();
        Changed?.Invoke();
        if (announce) Copied?.Invoke(entry);
    }

    public void Clear()
    {
        _store.Items.Clear();
        _store.Save();
        Changed?.Invoke();
    }

    /// <summary>Pone texto en el portapapeles de Windows (sin volver a anotarlo).</summary>
    public bool Copy(string text)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(_hwnd))
            {
                try
                {
                    EmptyClipboard();
                    var bytes = (text.Length + 1) * 2;
                    var memory = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                    var target = GlobalLock(memory);
                    Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                    Marshal.WriteInt16(target, text.Length * 2, 0);
                    GlobalUnlock(memory);
                    return SetClipboardData(CF_UNICODETEXT, memory) != IntPtr.Zero;
                }
                finally
                {
                    CloseClipboard();
                }
            }
            Thread.Sleep(20); // otra app tiene el portapapeles abierto
        }
        return false;
    }

    /// <summary>"No guardar desde": la app en primer plano al copiar está excluida en Ajustes (1Password, Bitwarden…).</summary>
    private bool IsExcludedApp()
    {
        var excluded = _s.ClipExclude.Where(e => e.Value).Select(e => e.Key.Replace(" ", string.Empty)).ToList();
        if (excluded.Count == 0) return false;
        GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            var name = process.ProcessName.Replace(" ", string.Empty);
            return excluded.Any(e => name.Contains(e, StringComparison.OrdinalIgnoreCase));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    private static bool IsPrivate()
    {
        if (IsClipboardFormatAvailable(ExcludeFormat) || IsClipboardFormatAvailable(ViewerIgnoreFormat)) return true;
        if (!IsClipboardFormatAvailable(HistoryFormat)) return false;
        // CanIncludeInClipboardHistory = 0 → no guardar.
        return ReadData(HistoryFormat, p => Marshal.ReadInt32(p)) == 0;
    }

    private string? ReadText() =>
        IsClipboardFormatAvailable(CF_UNICODETEXT) ? ReadData(CF_UNICODETEXT, p => Marshal.PtrToStringUni(p)) : null;

    private static T? ReadData<T>(uint format, Func<IntPtr, T> read)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    var handle = GetClipboardData(format);
                    if (handle == IntPtr.Zero) return default;
                    var pointer = GlobalLock(handle);
                    try { return pointer == IntPtr.Zero ? default : read(pointer); }
                    finally { GlobalUnlock(handle); }
                }
                finally
                {
                    CloseClipboard();
                }
            }
            Thread.Sleep(20);
        }
        return default;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
}
