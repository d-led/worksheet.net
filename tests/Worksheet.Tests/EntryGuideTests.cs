// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

public sealed class EntryGuideTests
{
    /// <summary>Where the day stopped: the start a line may leave out.</summary>
    private static readonly TimeOnly Stopped = new(10, 0);

    [Fact]
    public void Nothing_typed_is_the_project_step() => Assert.Equal(EntryStep.Project, EntryGuide.Next(null));

    [Theory]
    [InlineData("", EntryStep.Project)]
    [InlineData("   ", EntryStep.Project)]
    [InlineData("web", EntryStep.Topic)]
    [InlineData("web (", EntryStep.Topic)]
    [InlineData("web (login)", EntryStep.Start)]
    [InlineData("web (login) 09:00", EntryStep.Span)]
    [InlineData("web 09:00", EntryStep.Span)]
    [InlineData("web 09:00-09:00", EntryStep.Span)]
    [InlineData("web 09:00 1h", EntryStep.Complete)]
    [InlineData("web 09:00-10:00", EntryStep.Complete)]
    [InlineData("web (login) 09:00 1h30m", EntryStep.Complete)]
    // A clock written in a hurry still counts as a clock, so the length comes next.
    [InlineData("web (login) 12:0", EntryStep.Span)]
    [InlineData("web 4:30 1h", EntryStep.Complete)]
    public void Says_what_to_write_next(string typed, EntryStep expected)
    {
        Assert.Equal(expected, EntryGuide.Next(typed));
    }

    [Theory]
    [InlineData(EntryStep.Project, "project")]
    [InlineData(EntryStep.Topic, "topic in (brackets), optional")]
    [InlineData(EntryStep.Start, "start, e.g. 09:00")]
    [InlineData(EntryStep.Span, "length 1h30m, or an end 09:00-11:00")]
    [InlineData(EntryStep.Complete, "press Enter")]
    public void Every_step_has_a_sentence(EntryStep step, string expected)
    {
        Assert.Equal(expected, EntryGuide.Label(step));
    }

    [Theory]
    [InlineData("web till 13:30", EntryStep.Complete)]
    [InlineData("web for 1h30m", EntryStep.Complete)]
    [InlineData("web (login) until 13:30", EntryStep.Complete)]
    [InlineData("web till", EntryStep.Span)]
    [InlineData("web for", EntryStep.Span)]
    public void A_shorthand_is_read_once_the_day_has_an_end_to_continue_from(string typed, EntryStep expected)
    {
        Assert.Equal(expected, EntryGuide.Next(typed, Stopped));
    }

    [Fact]
    public void Without_an_end_to_continue_from_the_start_is_what_a_shorthand_is_missing()
    {
        Assert.Equal(EntryStep.Start, EntryGuide.Next("web till 13:30"));
        Assert.Equal(EntryStep.Start, EntryGuide.Next("web for 1h30m"));
        Assert.Equal("start, e.g. 09:00", EntryGuide.Label(EntryStep.Start, "web till 13:30"));
    }

    [Fact]
    public void A_line_with_its_own_start_is_never_mistaken_for_a_shorthand()
    {
        Assert.Equal(EntryStep.Span, EntryGuide.Next("web 09:00"));
        Assert.Equal(EntryStep.Complete, EntryGuide.Next("web 09:00 1h"));
    }

    [Fact]
    public void A_name_closed_with_a_dash_is_ready_for_its_start()
    {
        Assert.Equal(EntryStep.Start, EntryGuide.Next("web-"));
        Assert.Equal(EntryStep.Start, EntryGuide.Next("web (login) -"));
    }

    [Fact]
    public void The_start_step_offers_the_shorthands_beside_the_clock()
    {
        Assert.Equal(
            "start 09:00, or from 10:00 \"till 11:30\" / \"for 1h30m\"",
            EntryGuide.Label(EntryStep.Start, "web (login) ", Stopped));
    }

    [Theory]
    [InlineData("web till", "an end, e.g. 11:30")]
    [InlineData("web until", "an end, e.g. 11:30")]
    [InlineData("web for", "a length, e.g. 1h30m")]
    public void The_span_step_says_which_half_of_it_the_shorthand_still_needs(string typed, string expected)
    {
        Assert.Equal(expected, EntryGuide.Label(EntryStep.Span, typed, Stopped));
    }

    [Fact]
    public void The_example_end_reads_later_than_the_start_it_continues()
    {
        Assert.Equal(
            "start 09:00, or from 17:30 \"till 19:00\" / \"for 1h30m\"",
            EntryGuide.Label(EntryStep.Start, "web (login) ", new TimeOnly(17, 30)));
    }

    [Fact]
    public void A_day_with_no_room_left_for_an_example_offers_only_the_length()
    {
        Assert.Equal(
            "start 09:00, or from 22:30 \"for 1h30m\"",
            EntryGuide.Label(EntryStep.Start, "web (login) ", new TimeOnly(22, 30)));
    }

    [Fact]
    public void An_end_example_with_no_day_to_build_one_from_is_a_plain_clock() =>
        Assert.Equal("an end, e.g. 13:30", EntryGuide.Label(EntryStep.Span, "web till"));
}
