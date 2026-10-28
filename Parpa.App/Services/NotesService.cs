namespace Parpa.Services;

public sealed record Note(Guid Id, string Text, DateTime Created);

/// <summary>Notas rápidas (%APPDATA%\Parpa\notes.json), la más reciente primero.</summary>
public sealed class NotesService
{
    private readonly JsonStore<Note> _store = new("notes");

    public IReadOnlyList<Note> Items => _store.Items;

    public event Action? Changed;

    public void Add(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _store.Items.Insert(0, new Note(Guid.NewGuid(), text, DateTime.Now));
        _store.Save();
        Changed?.Invoke();
    }

    public void Remove(Guid id)
    {
        if (_store.Items.RemoveAll(n => n.Id == id) == 0) return;
        _store.Save();
        Changed?.Invoke();
    }
}
