using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace Parpa.Services;

/// <summary>
/// Uso de CPU, GPU y RAM para la tarjeta SISTEMA de Inicio. Solo mide mientras Inicio está a la vista
/// (<see cref="Start"/> / <see cref="Stop"/>), una vez por segundo y fuera del hilo de la UI.
/// <list type="bullet">
/// <item>CPU: GetSystemTimes (tiempo ocupado entre dos lecturas).</item>
/// <item>GPU: contadores "GPU Engine" de PDH, como el Administrador de tareas: se suma cada motor
/// entre procesos y se queda el más ocupado. <c>null</c> si el sistema no los ofrece.</item>
/// <item>RAM: GlobalMemoryStatusEx (memoria física en uso).</item>
/// </list>
/// </summary>
public sealed class SystemStats : IDisposable
{
    private const uint PDH_FMT_DOUBLE = 0x00000200;
    private const int PDH_MORE_DATA = unchecked((int)0x800007D2);
    private const string GpuCounter = @"\GPU Engine(*)\Utilization Percentage";

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _timer;
    private IntPtr _query;
    private IntPtr _gpu;
    private bool _gpuFailed;
    private long _idle, _total;
    private int _busy; // 1 mientras se mide en segundo plano

    public SystemStats()
    {
        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Sample();
    }

    /// <summary>Último uso medido, en %. GPU <c>null</c> = no disponible.</summary>
    public (int? Cpu, int? Gpu, int? Ram) Current { get; private set; }

    /// <summary>Nueva medida, en el hilo de la UI.</summary>
    public event Action? Changed;

    public void Start()
    {
        if (_timer.IsRunning) return;
        Sample(); // la CPU y la GPU necesitan dos lecturas: la primera solo deja la base
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void Sample()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        Task.Run(() =>
        {
            try
            {
                var stats = (Cpu(), Gpu(), Ram());
                _dispatcher.TryEnqueue(() =>
                {
                    Current = stats;
                    Changed?.Invoke();
                });
            }
            finally
            {
                Volatile.Write(ref _busy, 0);
            }
        });
    }

    private int? Cpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
        var total = kernel + user; // el tiempo de kernel ya incluye el inactivo
        var (dIdle, dTotal) = (idle - _idle, total - _total);
        var first = _total == 0;
        (_idle, _total) = (idle, total);
        if (first || dTotal <= 0) return Current.Cpu;
        return Math.Clamp((int)Math.Round(100.0 * (dTotal - dIdle) / dTotal), 0, 100);
    }

    private int? Ram()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status) ? (int)status.dwMemoryLoad : null;
    }

    private int? Gpu()
    {
        if (_gpuFailed) return null;
        if (_query == IntPtr.Zero)
        {
            if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0 || PdhAddEnglishCounter(_query, GpuCounter, IntPtr.Zero, out _gpu) != 0)
            {
                Log.Info("Sistema: sin contadores de GPU");
                _gpuFailed = true;
                return null;
            }
            PdhCollectQueryData(_query);
            return null;
        }
        if (PdhCollectQueryData(_query) != 0) return Current.Gpu;

        uint size = 0;
        if (PdhGetFormattedCounterArray(_gpu, PDH_FMT_DOUBLE, ref size, out _, IntPtr.Zero) != PDH_MORE_DATA) return 0;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArray(_gpu, PDH_FMT_DOUBLE, ref size, out var count, buffer) != 0) return Current.Gpu;
            // Instancias "pid_123_luid_0x0_0xABC_phys_0_eng_3_engtype_3D": se suma por motor (luid…eng_N).
            var engines = new Dictionary<string, double>();
            const int itemSize = 24; // PDH_FMT_COUNTERVALUE_ITEM_W en 64 bits: nombre (8) + estado (4 + 4) + valor (8)
            for (var i = 0; i < count; i++)
            {
                var item = buffer + i * itemSize;
                if (Marshal.ReadInt32(item, 8) != 0) continue; // CStatus: valor no válido
                var name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item)) ?? string.Empty;
                var value = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, 16));
                var start = name.IndexOf("_luid_", StringComparison.Ordinal);
                var end = name.IndexOf("_engtype_", StringComparison.Ordinal);
                var engine = start >= 0 && end > start ? name[start..end] : name;
                engines[engine] = engines.GetValueOrDefault(engine) + value;
            }
            return engines.Count == 0 ? 0 : Math.Clamp((int)Math.Round(engines.Values.Max()), 0, 100);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_query != IntPtr.Zero) PdhCloseQuery(_query);
        _query = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern int PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhGetFormattedCounterArrayW")]
    private static extern int PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr items);

    [DllImport("pdh.dll")]
    private static extern int PdhCloseQuery(IntPtr query);
}
