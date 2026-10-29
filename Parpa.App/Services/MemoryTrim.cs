using Microsoft.UI.Dispatching;
using Parpa.Interop;

namespace Parpa.Services;

/// <summary>
/// Objetivo &lt; 100 MB: cuando Parpa se queda en reposo (tras arrancar o al cerrarse el notch) se recoge la basura
/// y se devuelve a Windows la memoria que ya no se está tocando.
/// <list type="bullet">
/// <item>GC: el montón administrado apenas crece (~3 MB), así que el GC casi nunca se ejecuta solo y los objetos
/// de Windows (WinRT) que ya no se usan seguían reteniendo su memoria nativa.</item>
/// <item>Working set: el arranque y el notch abierto tocan muchas páginas (XAML, JIT, texto) que en reposo no
/// vuelven a usarse. Si hacen falta, Windows las recupera al momento desde la lista en espera.</item>
/// </list>
/// </summary>
public sealed class MemoryTrim
{
    /// <summary>Margen para que termine lo que haya en marcha (animación de cierre, sonido).</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(5);

    private readonly DispatcherQueueTimer _timer;
    private readonly Func<bool> _isIdle;

    /// <param name="isIdle">Si sigue en reposo al vencer el margen (p. ej. el notch no se ha vuelto a abrir).</param>
    public MemoryTrim(Func<bool> isIdle)
    {
        _isIdle = isIdle;
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.IsRepeating = false;
        _timer.Interval = Settle;
        _timer.Tick += (_, _) => { if (_isIdle()) Trim(); };
    }

    /// <summary>
    /// Cada minuto (en el tic del reloj, sin despertar más veces): GC de gen0/gen1, &lt; 1 ms con este montón.
    /// Libera la memoria nativa de los objetos de Windows que se crean al leer avisos y medios.
    /// </summary>
    public void Collect() => GC.Collect(1);

    /// <summary>Recorta tras el margen (se reinicia si se vuelve a pedir antes).</summary>
    public void Schedule()
    {
        _timer.Stop();
        _timer.Start();
    }

    private static void Trim()
    {
        GC.Collect();
        Win32.EmptyWorkingSet(Win32.GetCurrentProcess());
    }
}
