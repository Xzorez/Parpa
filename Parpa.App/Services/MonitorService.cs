using Parpa.Interop;

namespace Parpa.Services;

public readonly record struct MonitorInfo(int Number, Win32.RECT Bounds, double Scale, bool IsPrimary);

/// <summary>
/// Monitores en orden (por nombre de dispositivo \\.\DISPLAYn). El ajuste <c>monitor</c> es la posición
/// en esa lista empezando en 1 (los nombres de dispositivo no tienen por qué empezar en 1: DISPLAY6, DISPLAY7…).
/// </summary>
public static class MonitorService
{
    public static IReadOnlyList<MonitorInfo> All()
    {
        var list = new List<MonitorInfo>();
        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref Win32.RECT _, IntPtr _) =>
        {
            var info = new Win32.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFOEX>() };
            if (!Win32.GetMonitorInfo(hMonitor, ref info)) return true;
            var digits = new string(info.szDevice.Where(char.IsDigit).ToArray());
            var number = int.TryParse(digits, out var n) ? n : list.Count + 1;
            Win32.GetDpiForMonitor(hMonitor, Win32.MDT_EFFECTIVE_DPI, out var dpi, out _);
            list.Add(new MonitorInfo(number, info.rcMonitor, dpi == 0 ? 1 : dpi / 96.0, (info.dwFlags & 1) != 0));
            return true;
        }, IntPtr.Zero);
        return list.OrderBy(m => m.Number).Select((m, i) => m with { Number = i + 1 }).ToList();
    }

    /// <summary>Monitor elegido (1 = primero); si ya no existe, el principal.</summary>
    public static MonitorInfo Get(int number)
    {
        var all = All();
        return number >= 1 && number <= all.Count ? all[number - 1] : all.First(m => m.IsPrimary);
    }
}
