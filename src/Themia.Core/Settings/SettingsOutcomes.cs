namespace Themia.Core.Settings;

/// <summary>
/// [S-3572] [S-3576] How something a line of ⚙ started went, said under that line: an export saved,
/// an import refused, the Store asked. <see cref="Busy"/> draws the ring before the words (it is still
/// being done); <see cref="Failed"/> draws them in the critical colour.
/// </summary>
public sealed record MenuOutcome(string Text, bool Busy = false, bool Failed = false);

/// <summary>
/// [S-3572] [S-3576] What stands under the lines of ⚙ › Themia Pro and Back up &amp; reset, by the
/// line's key: one for the app, so every display's menu says the same. Kept while a settings menu
/// is open; emptied when the last one closes (<see cref="Clear"/>), so ⚙ opened again says nothing
/// of what was done before, as Karpo's does.
/// </summary>
public sealed class SettingsOutcomes
{
    private readonly Dictionary<string, MenuOutcome> _said = new(StringComparer.Ordinal);

    /// <summary>Raised when what stands under a line changed: the open menus draw it.</summary>
    public event Action? Changed;

    /// <summary>What stands under the line of <paramref name="row"/>, or null.</summary>
    public MenuOutcome? Of(string row) => _said.GetValueOrDefault(row);

    /// <summary>Whether something started from <paramref name="row"/> is still being done.</summary>
    public bool Busy(string row) => Of(row) is { Busy: true };

    /// <summary>Say <paramref name="outcome"/> under the line of <paramref name="row"/> (null takes it away), and draw it.</summary>
    public void Say(string row, MenuOutcome? outcome)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (outcome is null)
        {
            _said.Remove(row);
        }
        else
        {
            _said[row] = outcome;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// The last menu closed: nothing stands under any line any more, but what is still being done
    /// (the Store's dialog still up): its line says so when ⚙ opens again, and how it ended once it has.
    /// </summary>
    public void Clear()
    {
        var done = _said.Where(s => !s.Value.Busy).Select(s => s.Key).ToList();
        if (done.Count == 0)
        {
            return;
        }

        foreach (var row in done)
        {
            _said.Remove(row);
        }

        Changed?.Invoke();
    }
}
