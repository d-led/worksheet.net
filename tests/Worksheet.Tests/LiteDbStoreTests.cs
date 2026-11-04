// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

public sealed class LiteDbStoreTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 11, 3);
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Topic = Guid.NewGuid();

    private readonly TempDirectory _temp = new("worksheet-tests");

    private string FilePath => Path.Combine(_temp.Path, "ledger.db");

    public void Dispose() => _temp.Dispose();

    private static Entry TheEntry() =>
        new()
        {
            Id = EntryId.Create(),
            ProjectId = Project,
            TopicId = Topic,
            Date = Day,
            Start = new TimeOnly(9, 0),
            Duration = TimeSpan.FromMinutes(90),
            Note = "first light",
        };

    [Fact]
    public void Every_kind_of_change_survives_the_round_trip()
    {
        var entry = TheEntry();
        LedgerEvent[] changes =
        [
            new ProjectOpened(Project, "Website"),
            new TopicAdded(Topic, Project, "login"),
            new EntryLogged(entry),
            new EntryRevised(entry, entry with { Duration = TimeSpan.FromMinutes(120) }),
            new EntryRetracted(entry with { Duration = TimeSpan.FromMinutes(120) }),
        ];

        using (var store = new LiteDbStore(FilePath))
        {
            store.Journal.Append(changes, expectedVersion: 0, ChangeOrigin.User);
        }

        using var reopened = new LiteDbStore(FilePath);
        var records = reopened.Journal.ReadFrom(0);

        Assert.Equal(changes, records.Select(record => record.Change));
        Assert.Equal(5, reopened.Journal.Version);
    }

    [Fact]
    public void A_stale_append_is_refused()
    {
        using var store = new LiteDbStore(FilePath);
        store.Journal.Append([new ProjectOpened(Project, "Website")], expectedVersion: 0, ChangeOrigin.User);

        var append = () => store.Journal.Append([new ProjectOpened(Guid.NewGuid(), "Webshop")], expectedVersion: 0, ChangeOrigin.User);

        var refusal = Assert.Throws<JournalConcurrencyException>(append);
        Assert.Equal(1, refusal.ActualVersion);
        Assert.Equal(1, store.Journal.Version);
    }

    [Fact]
    public void The_latest_snapshot_wins()
    {
        using var store = new LiteDbStore(FilePath);
        var state = LedgerState.Create();
        state.Apply(new ProjectOpened(Project, "Website"));
        state.Apply(new EntryLogged(TheEntry()));

        store.Snapshots.Save(state);
        store.Snapshots.Save(state);

        var loaded = store.Snapshots.LoadLatest();

        Assert.NotNull(loaded);
        Assert.Equal(state.Version, loaded.Version);
        Assert.Equal(TimeSpan.FromMinutes(90), loaded.TotalOn(Day));
        Assert.Equal("Website", loaded.ProjectName(Project));
    }

    [Fact]
    public void The_ledger_is_still_there_after_the_process_closes()
    {
        var parse = EntryCommandParser.Parse("Website (login) 09:00 1h30m", Day);
        Assert.True(parse.Succeeded, parse.Error);

        using (var first = LocalLedger.Open(_temp.Path, SnapshotPolicy.Disabled))
        {
            new EntryComposer(first.Ledger).Compose(parse.Command!);
        }

        using var second = LocalLedger.Open(_temp.Path, SnapshotPolicy.Disabled);

        Assert.Equal(TimeSpan.FromMinutes(90), second.Ledger.State.TotalOn(Day));
        Assert.Equal("Website", Assert.Single(second.Ledger.State.Projects).Value.Name);
        Assert.Equal("login", Assert.Single(second.Ledger.State.Topics).Value.Name);
    }

    [Fact]
    public void A_snapshot_and_a_full_replay_agree()
    {
        var parse = EntryCommandParser.Parse("Website (login) 09:00 1h30m", Day);
        Assert.True(parse.Succeeded, parse.Error);

        using (var first = LocalLedger.Open(_temp.Path, SnapshotPolicy.EveryChanges(1)))
        {
            new EntryComposer(first.Ledger).Compose(parse.Command!);
        }

        using var replayed = LocalLedger.Open(_temp.Path, SnapshotPolicy.Disabled);

        Assert.Equal(TimeSpan.FromMinutes(90), replayed.Ledger.State.TotalOn(Day));
        Assert.Equal(3, replayed.Ledger.Version);
    }

    [Fact]
    public void A_copy_of_the_file_opens_as_the_same_ledger()
    {
        var parse = EntryCommandParser.Parse("Website (login) 09:00 1h30m", Day);
        Assert.True(parse.Succeeded, parse.Error);

        var restored = Path.Combine(_temp.Path, "restored");
        Directory.CreateDirectory(restored);

        using (var original = LocalLedger.Open(_temp.Path, SnapshotPolicy.Disabled))
        {
            new EntryComposer(original.Ledger).Compose(parse.Command!);
            original.CopyTo(Path.Combine(restored, "ledger.db"));
        }

        using var copy = LocalLedger.Open(restored, SnapshotPolicy.Disabled);

        Assert.Equal(TimeSpan.FromMinutes(90), copy.Ledger.State.TotalOn(Day));
        Assert.Equal("Website", Assert.Single(copy.Ledger.State.Projects).Value.Name);
    }
}
