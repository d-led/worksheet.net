// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>The journal port without a store, so ledger behaviour is testable on its own.</summary>
internal sealed class InMemoryJournal : IEventJournal
{
    private readonly List<JournalRecord> _records = [];

    public long Version => _records.Count == 0 ? 0 : _records[^1].Version;

    /// <summary>The lowest version asked for since opening, which shows where a snapshot ended.</summary>
    public long? LastReadFrom { get; private set; }

    public IReadOnlyList<JournalRecord> ReadFrom(long versionExclusive)
    {
        LastReadFrom = versionExclusive;
        return [.. _records.Where(record => record.Version > versionExclusive)];
    }

    public void Append(IReadOnlyList<LedgerEvent> changes, long expectedVersion, ChangeOrigin origin)
    {
        if (Version != expectedVersion)
        {
            throw new JournalConcurrencyException(expectedVersion, Version);
        }

        foreach (var change in changes)
        {
            _records.Add(new JournalRecord(Version + 1, change, DateTimeOffset.UnixEpoch, origin));
        }
    }
}

internal sealed class InMemorySnapshots : ISnapshotStore
{
    public LedgerState? Latest { get; private set; }

    public int Saves { get; private set; }

    public LedgerState? LoadLatest() => Latest is null ? null : LedgerState.Restore(Latest.ToSnapshot());

    public void Save(LedgerState state)
    {
        Latest = LedgerState.Restore(state.ToSnapshot());
        Saves++;
    }
}

internal sealed class BrokenSnapshots(long version) : ISnapshotStore
{
    public LedgerState? LoadLatest() => LedgerState.Restore(new LedgerSnapshot(version, [], [], []));

    public void Save(LedgerState state) => throw new NotSupportedException();
}
