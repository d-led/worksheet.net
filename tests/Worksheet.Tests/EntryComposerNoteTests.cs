// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>
/// A note of its own: it belongs to the work the day ended with, because that is what a note
/// written after the fact is about.
/// </summary>
public sealed class EntryComposerNoteTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private readonly Ledger _ledger = Core.Ledger.Open(new InMemoryJournal());
    private readonly EntryComposer _composer;

    public EntryComposerNoteTests() => _composer = new EntryComposer(_ledger);

    private Entry Logged(string line)
    {
        var parse = EntryCommandParser.Parse(line, Day);

        Assert.True(parse.Succeeded, parse.Error);

        var composed = _composer.Compose(parse.Command!);

        Assert.True(composed.Created, string.Join(" ", composed.Problems));

        return composed.Entry!;
    }

    [Fact]
    public void The_note_lands_on_the_range_the_day_ended_with()
    {
        var morning = Logged("Website 09:00 1h");
        var afternoon = Logged("Reading 14:00 2h");

        var noted = _composer.Note(Day, "the LiteDB paper");

        Assert.True(noted.Created, string.Join(" ", noted.Problems));
        Assert.Equal("the LiteDB paper", _ledger.State.Entries[afternoon.Id].Note);
        Assert.Null(_ledger.State.Entries[morning.Id].Note);
    }

    [Fact]
    public void A_note_is_a_correction_that_can_be_undone()
    {
        Logged("Reading 14:00 2h");

        _composer.Note(Day, "the LiteDB paper");
        Assert.True(_ledger.TryUndo(out _));

        Assert.Null(Assert.Single(_ledger.State.Entries).Value.Note);
    }

    [Fact]
    public void A_day_with_nothing_on_it_has_nothing_to_note()
    {
        var noted = _composer.Note(Day, "the paper");

        Assert.False(noted.Created);
        Assert.Equal(NoteLine.NothingToNoteMessage, Assert.Single(noted.Problems));
    }

    [Fact]
    public void A_note_on_a_frozen_day_is_refused_like_any_other_correction()
    {
        Logged("Reading 14:00 2h");
        new DayCalendar(_ledger).Declare(Day, DayKind.Sick);

        Assert.False(_composer.Note(Day, "the paper").Created);
    }
}
