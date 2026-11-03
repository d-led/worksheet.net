// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>
/// Declaring what a day is. A declaration is a change like any other, so undo takes it back; and
/// it is only written when the day's kind actually changes, so the journal stays a record of
/// decisions rather than of clicks.
/// </summary>
public sealed class DayCalendarTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 3);
    private static readonly DateOnly Saturday = new(2026, 11, 7);

    private static Ledger NewLedger(out InMemoryJournal journal)
    {
        journal = new InMemoryJournal();
        return Core.Ledger.Open(journal);
    }

    [Fact]
    public void Declaring_a_sick_day_freezes_the_timetable()
    {
        var ledger = NewLedger(out _);

        new DayCalendar(ledger).Declare(Tuesday, DayKind.Sick);

        Assert.Equal(DayKind.Sick, ledger.State.KindOf(Tuesday));
        Assert.False(ledger.State.TakesEntries(Tuesday));
    }

    [Fact]
    public void Declaring_what_the_calendar_already_says_writes_nothing()
    {
        var ledger = NewLedger(out var journal);
        var version = ledger.Version;

        new DayCalendar(ledger).Declare(Saturday, DayKind.Weekend);

        Assert.Equal(version, journal.Version);
        Assert.Equal(DayKind.Weekend, ledger.State.KindOf(Saturday));
    }

    [Fact]
    public void Declaring_the_weekend_over_a_working_saturday_leaves_nothing_declared()
    {
        var ledger = NewLedger(out _);
        var calendar = new DayCalendar(ledger);

        calendar.Declare(Saturday, DayKind.Working);
        calendar.Declare(Saturday, DayKind.Weekend);

        Assert.Null(ledger.State.DeclaredKindOf(Saturday));
        Assert.Equal(DayKind.Weekend, ledger.State.KindOf(Saturday));
    }

    [Fact]
    public void A_declaration_is_undone_and_redone_like_any_other_change()
    {
        var ledger = NewLedger(out _);
        var calendar = new DayCalendar(ledger);
        calendar.Declare(Saturday, DayKind.Working);

        Assert.True(ledger.TryUndo(out var undoReason), undoReason);
        Assert.Equal(DayKind.Weekend, ledger.State.KindOf(Saturday));

        Assert.True(ledger.TryRedo(out var redoReason), redoReason);
        Assert.True(ledger.State.TakesEntries(Saturday));
    }
}
