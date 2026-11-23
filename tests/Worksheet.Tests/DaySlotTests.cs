// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>The minutes of a day, and what one stretch of them takes away from another.</summary>
public sealed class DaySlotTests
{
    private static DaySlot Slot(string from, string to) => new(Clocks.At(from).ToTimeSpan(), Clocks.At(to).ToTimeSpan());

    [Fact]
    public void A_stretch_nothing_is_taken_from_keeps_all_its_minutes() =>
        Assert.Equal([Slot("09:00", "10:00")], Slot("09:00", "10:00").Minus(Slot("11:00", "12:00")));

    [Fact]
    public void Stretches_that_only_touch_leave_each_other_whole()
    {
        Assert.Equal([Slot("09:00", "10:00")], Slot("09:00", "10:00").Minus(Slot("10:00", "11:00")));
        Assert.Equal([Slot("10:00", "11:00")], Slot("10:00", "11:00").Minus(Slot("09:00", "10:00")));
    }

    [Fact]
    public void Minutes_taken_from_the_middle_leave_the_two_ends() =>
        Assert.Equal(
            [Slot("09:00", "11:00"), Slot("11:30", "12:00")],
            Slot("09:00", "12:00").Minus(Slot("11:00", "11:30")));

    [Fact]
    public void Minutes_taken_from_the_front_leave_the_tail() =>
        Assert.Equal([Slot("10:30", "12:00")], Slot("09:00", "12:00").Minus(Slot("09:00", "10:30")));

    [Fact]
    public void Minutes_taken_from_the_end_leave_the_head() =>
        Assert.Equal([Slot("09:00", "10:30")], Slot("09:00", "12:00").Minus(Slot("10:30", "12:00")));

    [Fact]
    public void Minutes_that_cover_the_stretch_leave_nothing() =>
        Assert.Empty(Slot("10:00", "11:00").Minus(Slot("09:00", "12:00")));

    [Fact]
    public void A_stretch_taken_away_from_itself_leaves_nothing() =>
        Assert.Empty(Slot("09:00", "12:00").Minus(Slot("09:00", "12:00")));

    [Fact]
    public void A_stretch_that_runs_to_midnight_reads_its_end_as_midnight() =>
        Assert.Equal(TimeOnly.MinValue, new DaySlot(TimeSpan.FromHours(23), TimeSpan.FromDays(1)).EndsAt);
}
