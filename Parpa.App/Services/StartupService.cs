using Microsoft.Win32;

namespace Parpa.Services;

/// <summary>Iniciar con Windows: HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Parpa (sin permisos de administrador).</summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Parpa";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var command = $"\"{Environment.ProcessPath}\"";
                if (key.GetValue(ValueName) as string != command) key.SetValue(ValueName, command);
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Log.Error("Inicio con Windows: no se pudo cambiar", ex);
        }
    }
}
