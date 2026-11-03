// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>What a correction is allowed to be. Each refusal names the problem it found.</summary>
public sealed class EntryComposerRevisionTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private static Core.Ledger NewLedger() => Core.Ledger.Open(new InMemoryJournal());

    private static Entry Log(Core.Ledger ledger, string line)
    {
        var parse = EntryCommandParser.Parse(line, Day);
        Assert.True(parse.Succeeded, parse.Error);

        var composed = new EntryComposer(ledger).Compose(parse.Command!);
        Assert.True(composed.Created, string.Join(" ", composed.Problems));

        return composed.Entry!;
    }

    [Fact]
    public void A_revision_to_a_project_the_ledger_does_not_know_is_refused()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website 09:00 1h");

        var result = new EntryComposer(ledger).Revise(before, before with { ProjectId = Guid.NewGuid() });

        Assert.Equal("That project is no longer known.", Assert.Single(result.Problems));
    }

    [Fact]
    public void A_revision_to_a_topic_the_ledger_does_not_know_is_refused()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website (login) 09:00 1h");

        var result = new EntryComposer(ledger).Revise(before, before with { TopicId = Guid.NewGuid() });

        Assert.Equal("That topic is no longer known.", Assert.Single(result.Problems));
    }

    [Fact]
    public void A_revision_to_another_day_is_refused()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website 09:00 1h");

        var result = new EntryComposer(ledger).Revise(before, before with { Date = Day.AddDays(1) });

        Assert.Equal("An entry cannot be moved to another day; log a new one instead.", Assert.Single(result.Problems));
    }

    [Fact]
    public void A_negative_duration_is_refused()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website 09:00 1h");

        var result = new EntryComposer(ledger).Revise(before, before with { Duration = TimeSpan.FromMinutes(-1) });

        Assert.Equal("A duration cannot be negative.", Assert.Single(result.Problems));
    }

    [Fact]
    public void An_unchanged_revision_writes_nothing()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website 09:00 1h");
        var version = ledger.Version;

        var result = new EntryComposer(ledger).Revise(before, before);

        Assert.True(result.Created);
        Assert.Equal(before, result.Entry);
        Assert.Equal(version, ledger.Version);
    }

    [Fact]
    public void A_refused_revision_leaves_the_journal_alone()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website 09:00 1h");
        var version = ledger.Version;

        new EntryComposer(ledger).Revise(before, before with { Duration = TimeSpan.FromHours(30) });

        Assert.Equal(version, ledger.Version);
        Assert.Equal(TimeSpan.FromMinutes(60), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void An_empty_note_is_stored_as_no_note()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website 09:00 1h");

        var result = new EntryComposer(ledger).Revise(before, before with { Note = "   " });

        Assert.Null(result.Entry!.Note);
        Assert.Null(ledger.State.Entries[before.Id].Note);
    }

    [Fact]
    public void A_note_is_trimmed()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website 09:00 1h");

        new EntryComposer(ledger).Revise(before, before with { Note = "  called the client  " });

        Assert.Equal("called the client", ledger.State.Entries[before.Id].Note);
    }

    [Fact]
    public void Retracting_something_that_is_not_there_is_harmless()
    {
        var ledger = NewLedger();
        var version = ledger.Version;

        var retracted = new EntryComposer(ledger).Retract(EntryId.Create());

        Assert.False(retracted.Created);
        Assert.Equal("That entry is no longer on the day.", Assert.Single(retracted.Problems));
        Assert.Equal(version, ledger.Version);
    }

    [Fact]
    public void A_frozen_day_refuses_a_correction_and_keeps_the_entry_as_it_was()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website 09:00 1h");
        new DayCalendar(ledger).Declare(Day, DayKind.Holiday);

        var result = new EntryComposer(ledger).Revise(before, before with { Note = "called the client" });

        Assert.False(result.Created);
        Assert.Contains("is marked Holiday", Assert.Single(result.Problems));
        Assert.Null(ledger.State.Entries[before.Id].Note);
    }

    [Fact]
    public void A_frozen_day_refuses_a_retraction()
    {
        var ledger = NewLedger();
        var before = Log(ledger, "Website 09:00 1h");
        new DayCalendar(ledger).Declare(Day, DayKind.Sick);

        var result = new EntryComposer(ledger).Retract(before.Id);

        Assert.False(result.Created);
        Assert.Single(ledger.State.Entries);
    }
}
