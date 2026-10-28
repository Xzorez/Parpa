using System.Text.RegularExpressions;
using Microsoft.UI.Dispatching;

namespace Parpa.Services;

/// <param name="Time">Hora "HH:mm" opcional (se escribe al final: "Responder a Ana 21:00").</param>
/// <param name="DoneOn">Día en que se marcó hecha (las hechas de otros días se limpian al arrancar).</param>
public sealed record TaskItem(Guid Id, string Text, string? Time, bool Done, DateTime Created, DateTime? DoneOn = null);

/// <summary>
/// Tareas de hoy (%APPDATA%\Parpa\tasks.json). Avisa a la hora de cada tarea pendiente con un
/// temporizador al siguiente vencimiento (no sondea).
/// </summary>
public sealed partial class TasksService
{
    private readonly JsonStore<TaskItem> _store = new("tasks");
    private readonly DispatcherQueueTimer _dueTimer;
    private readonly HashSet<Guid> _notified = new();

    public IReadOnlyList<TaskItem> Items => _store.Items;

    public int Pending => _store.Items.Count(t => !t.Done);

    public event Action? Changed;

    /// <summary>Una tarea pendiente llega a su hora.</summary>
    public event Action<TaskItem>? Due;

    public TasksService()
    {
        // Las hechas de días anteriores ya no son "de hoy".
        var removed = _store.Items.RemoveAll(t => t.Done && t.DoneOn?.Date < DateTime.Today);
        if (removed > 0) _store.Save();

        _dueTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _dueTimer.IsRepeating = false;
        _dueTimer.Tick += (_, _) => CheckDue();
        ScheduleDue();
    }

    /// <summary>Próxima tarea pendiente (primero las que tienen hora, por hora).</summary>
    public TaskItem? Next => _store.Items.Where(t => !t.Done)
        .OrderBy(t => t.Time is null).ThenBy(t => t.Time, StringComparer.Ordinal).ThenBy(t => t.Created)
        .FirstOrDefault();

    public void Add(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        string? time = null;
        var match = TrailingTime().Match(text);
        if (match.Success)
        {
            time = $"{int.Parse(match.Groups[1].Value):00}:{match.Groups[2].Value}";
            text = text[..match.Index].Trim();
        }
        _store.Items.Add(new TaskItem(Guid.NewGuid(), text, time, false, DateTime.Now));
        Commit();
    }

    public void Toggle(Guid id)
    {
        var index = _store.Items.FindIndex(t => t.Id == id);
        if (index < 0) return;
        var task = _store.Items[index];
        _store.Items[index] = task with { Done = !task.Done, DoneOn = task.Done ? null : DateTime.Now };
        Commit();
    }

    /// <summary>Pospone 10 min: la hora pasa a ahora + 10.</summary>
    public void Snooze(Guid id, TimeSpan delay)
    {
        var index = _store.Items.FindIndex(t => t.Id == id);
        if (index < 0) return;
        _notified.Remove(id);
        _store.Items[index] = _store.Items[index] with { Time = (DateTime.Now + delay).ToString("HH:mm") };
        Commit();
    }

    private void Commit()
    {
        _store.Save();
        ScheduleDue();
        Changed?.Invoke();
    }

    private static DateTime? DueAt(TaskItem task) =>
        task.Time is { } t && TimeSpan.TryParse(t, out var time) ? DateTime.Today + time : null;

    private void ScheduleDue()
    {
        _dueTimer.Stop();
        var next = _store.Items.Where(t => !t.Done && !_notified.Contains(t.Id))
            .Select(DueAt).Where(d => d > DateTime.Now).Min();
        if (next is null) return;
        _dueTimer.Interval = next.Value - DateTime.Now + TimeSpan.FromMilliseconds(200);
        _dueTimer.Start();
    }

    private void CheckDue()
    {
        var now = DateTime.Now;
        foreach (var task in _store.Items.Where(t => !t.Done && !_notified.Contains(t.Id)).ToList())
        {
            if (DueAt(task) is not { } at || at > now || now - at > TimeSpan.FromMinutes(1)) continue;
            _notified.Add(task.Id);
            Due?.Invoke(task);
        }
        ScheduleDue();
    }

    [GeneratedRegex(@"\s([01]?\d|2[0-3])[:.]([0-5]\d)$")]
    private static partial Regex TrailingTime();
}
