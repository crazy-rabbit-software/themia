using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Office;

/// <summary>[S-7077] One account's problem in an office load: its id, what went wrong, when the copy it left on screen is from.</summary>
public readonly record struct OfficeProblem(string Account, FetchStatus Status, DateTimeOffset? At);

/// <summary>[S-7077] What an office load found before it asked anything.</summary>
public enum OfficeLoadState
{
    /// <summary>No signed-in account offers what the widget reads: the card's Set up.</summary>
    NoAccounts,

    /// <summary>The accounts the widget names are all signed out.</summary>
    AccountsGone,

    /// <summary>Asked: the data and the problems met on the way are in the load.</summary>
    Asked,
}

/// <summary>[S-7077] One load of an office widget's data: what it found, the data, whether any account answered, the problems met, the accounts' labels.</summary>
public sealed record OfficeLoad<T>(OfficeLoadState State, T? Data, bool AnyData, IReadOnlyList<OfficeProblem> Problems, IReadOnlyDictionary<string, string> Labels)
    where T : class
{
    /// <summary>A load that asked nothing (no account, or every named one gone).</summary>
    public static OfficeLoad<T> Of(OfficeLoadState state) => new(state, null, false, [], new Dictionary<string, string>());
}

/// <summary>
/// [S-7077] The office widgets' one state rule, pure: the screen a load calls for (the kit's <see cref="KitScreen"/>), the
/// worst problem among the accounts' (one only the user can mend first, in ⚙ › Accounts), and the stale rule's outcomes
/// (<see cref="KitOutcome"/>: each account whose last sync could not improve on what is held, by its label, stamped).
/// </summary>
public static class OfficeScreens
{
    /// <summary>The problem a widget with nothing to show says: one only the user can mend before any other, else the first met.</summary>
    public static OfficeProblem? Worst(IReadOnlyList<OfficeProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        return problems.Count == 0 ? null : problems.OrderByDescending(p => OfficeProblems.NeedsUser(p.Status)).First();
    }

    /// <summary>
    /// [S-7077] The screen for <paramref name="load"/> (null: none has come back yet; <paramref name="failed"/>: the last load
    /// threw, its sentence): loading, the card's Set up, the accounts gone, the worst problem of a load that brought nothing,
    /// else the items.
    /// </summary>
    public static KitScreen Decide<T>(Locale locale, OfficeLoad<T>? load, SyncSubject subject, string? failed = null)
        where T : class
    {
        if (failed is not null)
        {
            return new KitScreen(KitScreenKind.Problem, failed, false, []);
        }

        if (load is null)
        {
            return new KitScreen(KitScreenKind.Loading, FetchState.Message(locale, FetchStatus.Loading), false, []);
        }

        switch (load.State)
        {
            case OfficeLoadState.NoAccounts:
                return new KitScreen(KitScreenKind.NoAccounts, string.Empty, false, []);

            case OfficeLoadState.AccountsGone:
                return new KitScreen(KitScreenKind.AccountsGone, OfficeProblems.Message(FetchStatus.NotConfigured, subject, locale), true, []);
        }

        if (!load.AnyData && Worst(load.Problems) is { } worst)
        {
            return new KitScreen(KitScreenKind.Problem, OfficeProblems.Message(worst.Status, subject, locale), OfficeProblems.NeedsUser(worst.Status), []);
        }

        return new KitScreen(KitScreenKind.Items, string.Empty, load.Problems.Any(p => OfficeProblems.NeedsUser(p.Status)), []);
    }

    /// <summary>[S-3651] [S-7042] The stale rule's outcomes: one per account whose last sync could not improve on what is held, named by its label.</summary>
    public static IReadOnlyList<KitOutcome> Outcomes(IReadOnlyList<OfficeProblem> problems, IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(problems);
        ArgumentNullException.ThrowIfNull(labels);
        return [.. problems.Select(p => new KitOutcome(labels.TryGetValue(p.Account, out var label) ? label : p.Account, p.Status, HeldAt: p.At))];
    }
}

/// <summary>
/// [S-7077] An office widget's one feed (the calendar, the week planner, the contacts, the meetings): its loads one at a
/// time (one asked for meanwhile runs after it, forced if any ask was), what the last said as the kit's screen
/// (<see cref="OfficeScreens"/>), the data of the last load that had any, and the stale rule's outcomes. Pure: the view
/// owns one, draws after <see cref="Changed"/> and never sees an exception (a load that throws is the screen's problem).
/// </summary>
public sealed class OfficeFeed<T>
    where T : class
{
    private const string Scope = "office";

    private readonly Func<bool, CancellationToken, Task<OfficeLoad<T>>> _load;
    private readonly SyncSubject _subject;
    private readonly Func<Locale, string> _failed;
    private OfficeLoad<T>? _last;
    private bool _threw;
    private bool _again;
    private bool _againForced;
    private bool _stopped;

    /// <param name="load">One load: <c>force</c> asks past the stored copy's freshness.</param>
    /// <param name="subject">What its sentences are about.</param>
    /// <param name="failed">The sentence of a load that threw.</param>
    public OfficeFeed(Func<bool, CancellationToken, Task<OfficeLoad<T>>> load, SyncSubject subject, Func<Locale, string> failed)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(failed);
        _load = load;
        _subject = subject;
        _failed = failed;
    }

    /// <summary>A load went out or came back: draw again.</summary>
    public event Action? Changed;

    /// <summary>A load is out (the status's ring).</summary>
    public bool Busy { get; private set; }

    /// <summary>The data of the last load that had any to show, or null.</summary>
    public T? Data { get; private set; }

    /// <summary>The last load (null before the first comes back).</summary>
    public OfficeLoad<T>? Last => _last;

    /// <summary>[S-7077] The screen the last load calls for, worded in <paramref name="locale"/>.</summary>
    public KitScreen Screen(Locale locale) => OfficeScreens.Decide(locale, _last, _subject, _threw ? _failed(locale) : null);

    /// <summary>[S-7042] The stale rule's outcomes while the items are on screen; none otherwise.</summary>
    public IReadOnlyList<KitOutcome> Outcomes
        => !_threw && _last is { State: OfficeLoadState.Asked } last && (last.AnyData || last.Problems.Count == 0)
            ? OfficeScreens.Outcomes(last.Problems, last.Labels)
            : [];

    /// <summary>Stop: no load starts or reports after this.</summary>
    public void Stop() => _stopped = true;

    /// <summary>Start again after <see cref="Stop"/>.</summary>
    public void Start() => _stopped = false;

    /// <summary>[S-7077] Hold <paramref name="load"/> as if it had come back (a render's fixture, a sample).</summary>
    public void Hold(OfficeLoad<T> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        Take(load);
        Changed?.Invoke();
    }

    /// <summary>One load at a time; one asked for meanwhile runs after it. Never throws: a failure is the problem screen.</summary>
    public async Task RefreshAsync(bool force)
    {
        if (_stopped)
        {
            return;
        }

        if (Busy)
        {
            _again = true;
            _againForced |= force;
            return;
        }

        Busy = true;
        Changed?.Invoke();

        try
        {
            var load = await _load(force, CancellationToken.None).ConfigureAwait(true);
            if (_stopped)
            {
                return;
            }

            Take(load);
        }
        catch (Exception ex)
        {
            // Reached from a ticker callback and a menu click: nothing above may see it.
            Log.Error(Scope, $"could not read the {_subject}", ex);
            _threw = true;
        }
        finally
        {
            Busy = false;
        }

        if (_stopped)
        {
            return;
        }

        Changed?.Invoke();

        if (_again)
        {
            var forced = _againForced;
            _again = false;
            _againForced = false;
            await RefreshAsync(forced).ConfigureAwait(true);
        }
    }

    private void Take(OfficeLoad<T> load)
    {
        _threw = false;
        _last = load;
        if (load.State == OfficeLoadState.Asked && (load.AnyData || load.Problems.Count == 0))
        {
            Data = load.Data;
        }
        else if (load.State != OfficeLoadState.Asked)
        {
            Data = null;
        }
    }
}
