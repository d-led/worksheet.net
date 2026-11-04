// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>One appended change, as the store keeps it.</summary>
public sealed record JournalRecord(long Version, LedgerEvent Change, DateTimeOffset RecordedAt, ChangeOrigin Origin);

/// <summary>The append-only port. The implementation decides how it is stored.</summary>
public interface IEventJournal
{
    /// <summary>Version of the last appended change, or zero for an empty journal.</summary>
    long Version { get; }

    /// <summary>Changes with a version greater than <paramref name="versionExclusive"/>, oldest first.</summary>
    IReadOnlyList<JournalRecord> ReadFrom(long versionExclusive);

    /// <summary>Appends all changes or none of them.</summary>
    /// <exception cref="JournalConcurrencyException">Someone else appended first.</exception>
    void Append(IReadOnlyList<LedgerEvent> changes, long expectedVersion, ChangeOrigin origin);
}

/// <summary>
/// A snapshot is the fold at a version. Reading one is a cache hit, not a separate truth.
/// Ownership: <see cref="LoadLatest"/> hands the state to the caller, who folds further into it; a
/// store must therefore return a fresh instance each time, and <see cref="Save"/> must keep a copy
/// rather than the instance it was given.
/// </summary>
public interface ISnapshotStore
{
    LedgerState? LoadLatest();

    void Save(LedgerState state);
}

/// <summary>How often to rewrite the fold. Zero disables snapshots.</summary>
public sealed record SnapshotPolicy(int Every)
{
    public static SnapshotPolicy Disabled => new(0);

    public static SnapshotPolicy EveryChanges(int changes) => new(changes);

    public bool IsDue(long version, long lastSnapshotVersion) =>
        Every > 0 && version - lastSnapshotVersion >= Every;
}

public sealed class JournalConcurrencyException(long expectedVersion, long actualVersion)
    : Exception($"The journal is at version {actualVersion}, not {expectedVersion}. Reload and try again.")
{
    public long ExpectedVersion { get; } = expectedVersion;

    public long ActualVersion { get; } = actualVersion;
}

public sealed class JournalIntegrityException(string message) : Exception(message);

/// <summary>
/// An observer that runs an action, which is the whole of what a window needs from the stream.
/// </summary>
public sealed class Watch(Action<LedgerState> changed) : IObserver<LedgerState>
{
    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext(LedgerState value) => changed(value);
}

/// <summary>
/// The ledger: a fold of a journal, plus the only way to change it. Opening reads a snapshot if
/// there is one and replays the tail; committing appends and then folds.
/// </summary>
public sealed class Ledger : IObservable<LedgerState>
{
    private readonly IEventJournal _journal;
    private readonly ISnapshotStore? _snapshots;
    private readonly SnapshotPolicy _policy;
    private readonly Stack<LedgerEvent> _redo = new();
    private readonly List<IObserver<LedgerState>> _observers = [];
    private long _snapshottedVersion;

    private Ledger(
        IEventJournal journal,
        ISnapshotStore? snapshots,
        SnapshotPolicy policy,
        LedgerState state,
        long snapshottedVersion)
    {
        _journal = journal;
        _snapshots = snapshots;
        _policy = policy;
        State = state;
        _snapshottedVersion = snapshottedVersion;
    }

    public LedgerState State { get; }

    public long Version => State.Version;

    /// <summary>
    /// The fold as a stream: the state as it stands when a call subscribes, then once for every
    /// change. A window that subscribes learns of a change the moment it lands, whoever made it —
    /// the person at the keyboard, an agent over MCP, an undo — so nothing is refreshed by hand.
    /// Changes are published on the thread that commits them, which is the one that reads the fold.
    /// </summary>
    public IDisposable Subscribe(IObserver<LedgerState> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        _observers.Add(observer);
        observer.OnNext(State);

        return new Unsubscriber(this, observer);
    }

    public bool CanUndo => Version > 0;

    /// <summary>
    /// True while changes are available to redo. Redo is a cursor over undo, not a second history:
    /// any new user change forgets what was undone, which is what people expect from an editor.
    /// </summary>
    public bool CanRedo => _redo.Count > 0;

    public static Ledger Open(IEventJournal journal, ISnapshotStore? snapshots = null, SnapshotPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(journal);

        var snapshot = snapshots?.LoadLatest();
        var state = Replay(journal, snapshot);

        EnsureConsistent(state, journal);

        return new Ledger(journal, snapshots, policy ?? SnapshotPolicy.Disabled, state, snapshot?.Version ?? 0);
    }

    private static LedgerState Replay(IEventJournal journal, LedgerState? snapshot)
    {
        var state = snapshot ?? LedgerState.Create();

        foreach (var record in journal.ReadFrom(state.Version))
        {
            state.Apply(record.Change);
        }

        return state;
    }

    private static void EnsureConsistent(LedgerState state, IEventJournal journal)
    {
        if (state.Version != journal.Version)
        {
            throw new JournalIntegrityException(
                $"The fold reached version {state.Version} but the journal holds {journal.Version}. The store is inconsistent.");
        }
    }

    public LedgerState Commit(params LedgerEvent[] changes) => Commit(changes, ChangeOrigin.User);

    public LedgerState Commit(IReadOnlyList<LedgerEvent> changes, ChangeOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            return State;
        }

        _journal.Append(changes, State.Version, origin);
        State.Apply(changes);

        if (origin is not (ChangeOrigin.Undo or ChangeOrigin.Redo))
        {
            _redo.Clear();
        }

        TakeSnapshotIfDue();

        Published();

        return State;
    }

    private void Published()
    {
        foreach (var observer in _observers.ToArray())
        {
            observer.OnNext(State);
        }
    }

    private sealed class Unsubscriber(Ledger ledger, IObserver<LedgerState> observer) : IDisposable
    {
        public void Dispose() => ledger._observers.Remove(observer);
    }

    /// <summary>
    /// Appends the inverse of the most recent change and remembers it for redo. The inverse is a
    /// change like any other — appended, dated, undone by its own inverse — so the journal is still
    /// the whole story, and nothing is ever overwritten or dropped.
    /// </summary>
    public bool TryUndo(out string? reason)
    {
        var last = _journal.ReadFrom(Version - 1).FirstOrDefault();

        if (last is null)
        {
            reason = "There is nothing to undo.";
            return false;
        }

        var inverse = UndoPlanner.Reverse(last.Change);

        if (inverse is null)
        {
            reason = $"{last.Change.GetType().Name} cannot be undone.";
            return false;
        }

        _redo.Push(last.Change);
        Commit([inverse], ChangeOrigin.Undo);
        reason = null;
        return true;
    }

    /// <summary>Appends the change that the last undo took away.</summary>
    public bool TryRedo(out string? reason)
    {
        if (_redo.Count == 0)
        {
            reason = "There is nothing to redo.";
            return false;
        }

        var next = _redo.Peek();
        Commit([next], ChangeOrigin.Redo);
        _redo.Pop();
        reason = null;
        return true;
    }

    private void TakeSnapshotIfDue()
    {
        if (_snapshots is null || !_policy.IsDue(State.Version, _snapshottedVersion))
        {
            return;
        }

        _snapshots.Save(State);
        _snapshottedVersion = State.Version;
    }
}
