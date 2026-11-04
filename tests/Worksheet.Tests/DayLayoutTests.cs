// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

public sealed class DayLayoutTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);
    private static readonly Guid Project = Guid.NewGuid();

    private static Entry At(string start, int minutes) =>
        new()
        {
            Id = EntryId.Create(),
            ProjectId = Project,
            Date = Day,
            Start = Clocks.At(start),
            Duration = TimeSpan.FromMinutes(minutes),
        };

    [Fact]
    public void Entries_come_out_in_start_order_whatever_order_they_went_in()
    {
        var layout = DayLayout.Of([At("13:00", 30), At("08:30", 60), At("10:00", 15)]);

        Assert.Equal(
            new[] { "08:30", "10:00", "13:00" },
            layout.Entries.Select(entry => Clocks.Text(entry.Start)));
    }

    [Fact]
    public void A_hole_between_two_entries_is_a_gap()
    {
        var layout = DayLayout.Of([At("09:00", 60), At("10:30", 60)]);

        var gap = Assert.Single(layout.Gaps);
        Assert.Equal(new TimeSpan(10, 0, 0), gap.Start);
        Assert.Equal(new TimeSpan(10, 30, 0), gap.End);
        Assert.Equal(TimeSpan.FromMinutes(30), gap.Duration);
        Assert.Equal(new TimeOnly(10, 0), gap.StartsAt);
    }

    [Fact]
    public void Touching_entries_leave_no_gap()
    {
        var layout = DayLayout.Of([At("09:00", 60), At("10:00", 60)]);

        Assert.Empty(layout.Gaps);
    }

    [Fact]
    public void The_night_before_the_first_entry_is_not_a_gap()
    {
        var layout = DayLayout.Of([At("08:30", 60)]);

        Assert.Empty(layout.Gaps);
        Assert.Equal(TimeSpan.FromMinutes(60), layout.Total);
        Assert.Equal(TimeSpan.Zero, layout.GapTotal);
    }

    [Fact]
    public void Totals_cover_entries_and_gaps_separately()
    {
        var layout = DayLayout.Of([At("09:00", 60), At("10:30", 30), At("12:00", 90)]);

        Assert.Equal(TimeSpan.FromMinutes(180), layout.Total);
        // 10:00-10:30 and 11:00-12:00.
        Assert.Equal(TimeSpan.FromMinutes(90), layout.GapTotal);
    }

    [Fact]
    public void An_entry_ending_at_midnight_creates_no_gap_after_it()
    {
        var layout = DayLayout.Of([At("23:00", 60)]);

        Assert.Empty(layout.Gaps);
        Assert.Equal(TimeSpan.FromMinutes(60), layout.Total);
    }

    [Fact]
    public void No_entries_means_no_rows_and_no_gaps()
    {
        var layout = DayLayout.Of([]);

        Assert.Empty(layout.Entries);
        Assert.Empty(layout.Gaps);
        Assert.Equal(TimeSpan.Zero, layout.Total);
    }
}
