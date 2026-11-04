// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

public sealed class LedgerTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);
    private static readonly Guid Project = Guid.NewGuid();

    private static Entry EntryAt(string start, int minutes) =>
        new()
        {
            Id = EntryId.Create(),
            ProjectId = Project,
            Date = Day,
            Start = TimeOnly.Parse(start),
            Duration = TimeSpan.FromMinutes(minutes),
        };

    private static Ledger Opened(InMemoryJournal journal, ISnapshotStore? snapshots = null, SnapshotPolicy? policy = null) =>
        Core.Ledger.Open(journal, snapshots, policy);

    [Fact]
    public void A_subscriber_hears_the_state_it_finds_and_every_change_after_it()
    {
        var ledger = Opened(new InMemoryJournal());
        var heard = new List<long>();

        using var until = ledger.Subscribe(new Watch(state => heard.Add(state.Version)));

        ledger.Commit(new ProjectOpened(Project, "Website"));
        ledger.Commit(new EntryLogged(EntryAt("09:00", 90)));

        Assert.Equal([0, 1, 2], heard);
    }

    [Fact]
    public void Undo_puts_the_ledger_back_where_it_was()
    {
        var ledger = Opened(new InMemoryJournal());
        ledger.Commit(new ProjectOpened(Project, "Website"));
        ledger.Commit(new EntryLogged(EntryAt("09:00", 90)));

        var undone = ledger.TryUndo(out var reason);

        Assert.True(undone, reason);
        Assert.Empty(ledger.State.Entries);
        Assert.Equal(TimeSpan.Zero, ledger.State.TotalOn(Day));
    }

    [Fact]
    public void Undo_reverses_only_the_most_recent_change()
    {
        var ledger = Opened(new InMemoryJournal());
        ledger.Commit(new ProjectOpened(Project, "Website"));
        ledger.Commit(new EntryLogged(EntryAt("09:00", 90)));
        ledger.Commit(new EntryLogged(EntryAt("11:00", 30)));

        ledger.TryUndo(out _);

        var remaining = Assert.Single(ledger.State.EntriesOn(Day));
        Assert.Equal(new TimeOnly(9, 0), remaining.Start);
    }

    [Fact]
    public void Undo_is_itself_a_change_so_it_can_be_undone()
    {
        var ledger = Opened(new InMemoryJournal());
        ledger.Commit(new ProjectOpened(Project, "Website"));
        ledger.Commit(new EntryLogged(EntryAt("09:00", 90)));

        ledger.TryUndo(out _);
        ledger.TryUndo(out _);

        Assert.Equal(TimeSpan.FromMinutes(90), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void Undo_says_why_when_there_is_nothing_to_undo()
    {
        var ledger = Opened(new InMemoryJournal());

        var undone = ledger.TryUndo(out var reason);

        Assert.False(undone);
        Assert.Equal("There is nothing to undo.", reason);
    }

    [Fact]
    public void Undo_refuses_a_change_that_has_no_inverse()
    {
        var ledger = Opened(new InMemoryJournal());
        ledger.Commit(new ProjectOpened(Project, "Website"));

        var undone = ledger.TryUndo(out var reason);

        Assert.False(undone);
        Assert.Equal("ProjectOpened cannot be undone.", reason);
    }

    [Fact]
    public void Opening_replays_only_what_the_snapshot_does_not_cover()
    {
        var journal = new InMemoryJournal();
        var snapshots = new InMemorySnapshots();
        var policy = SnapshotPolicy.EveryChanges(2);

        var first = Opened(journal, snapshots, policy);
        first.Commit(new ProjectOpened(Project, "Website"), new EntryLogged(EntryAt("09:00", 90)));

        var second = Opened(journal, snapshots, policy);
        second.Commit(new EntryLogged(EntryAt("11:00", 30)));

        var reopened = Opened(journal, snapshots, policy);

        Assert.Equal(1, snapshots.Saves);
        Assert.Equal(2, journal.LastReadFrom);
        Assert.Equal(TimeSpan.FromMinutes(120), reopened.State.TotalOn(Day));
        Assert.Equal(3, reopened.Version);
    }

    [Fact]
    public void Redo_brings_back_what_undo_took_away()
    {
        var ledger = Opened(new InMemoryJournal());
        ledger.Commit(new ProjectOpened(Project, "Website"));
        ledger.Commit(new EntryLogged(EntryAt("09:00", 90)));
        ledger.TryUndo(out _);

        var redone = ledger.TryRedo(out var reason);

        Assert.True(redone, reason);
        Assert.Equal(TimeSpan.FromMinutes(90), ledger.State.TotalOn(Day));
        Assert.False(ledger.CanRedo);
    }

    [Fact]
    public void A_new_change_after_an_undo_forgets_what_could_be_redone()
    {
        var ledger = Opened(new InMemoryJournal());
        ledger.Commit(new ProjectOpened(Project, "Website"));
        ledger.Commit(new EntryLogged(EntryAt("09:00", 90)));
        ledger.TryUndo(out _);

        ledger.Commit(new EntryLogged(EntryAt("11:00", 30)));
        var redone = ledger.TryRedo(out var reason);

        Assert.False(ledger.CanRedo);
        Assert.False(redone);
        Assert.Equal("There is nothing to redo.", reason);
    }

    [Fact]
    public void A_redo_can_still_be_undone()
    {
        var ledger = Opened(new InMemoryJournal());
        ledger.Commit(new ProjectOpened(Project, "Website"));
        ledger.Commit(new EntryLogged(EntryAt("09:00", 90)));
        ledger.TryUndo(out _);
        ledger.TryRedo(out _);

        var undone = ledger.TryUndo(out var reason);

        Assert.True(undone, reason);
        Assert.Equal(TimeSpan.Zero, ledger.State.TotalOn(Day));
    }

    [Fact]
    public void Redo_says_why_when_there_is_nothing_to_redo()
    {
        var ledger = Opened(new InMemoryJournal());

        var redone = ledger.TryRedo(out var reason);

        Assert.False(redone);
        Assert.Equal("There is nothing to redo.", reason);
        Assert.False(ledger.CanUndo);
    }

    [Fact]
    public void Opening_refuses_a_snapshot_that_is_ahead_of_the_journal()
    {
        var journal = new InMemoryJournal();

        var opening = () => Opened(journal, new BrokenSnapshots(version: 7));

        Assert.Throws<JournalIntegrityException>(opening);
    }
}
