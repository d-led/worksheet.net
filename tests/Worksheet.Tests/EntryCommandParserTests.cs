// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

public sealed class EntryCommandParserTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    [Theory]
    [InlineData("Website (login) 09:00 1h30m", "Website", "login", "09:00", 90)]
    [InlineData("Website (reports) 10:30-12:00", "Website", "reports", "10:30", 90)]
    [InlineData("Water the garden 09:00-13:30", "Water the garden", null, "09:00", 270)]
    [InlineData("Water the garden 9:0-13:3", "Water the garden", null, "09:00", 243)]
    [InlineData("Water the garden 09:00 13:30", "Water the garden", null, "09:00", 270)]
    [InlineData("Water the garden 9:0 13:3", "Water the garden", null, "09:00", 243)]
    [InlineData("Sprint 7 09:00 1h", "Sprint 7", null, "09:00", 60)]
    [InlineData("Deep work 9:30pm 45m", "Deep work", null, "21:30", 45)]
    [InlineData("Reading 14:00 2h 30m", "Reading", null, "14:00", 150)]
    [InlineData("Website () 09:00 1h", "Website", null, "09:00", 60)]
    [InlineData("Website ( ) 09:00 1h", "Website", null, "09:00", 60)]
    public void Reads_project_topic_and_span(
        string line,
        string project,
        string? topic,
        string start,
        int minutes)
    {
        var parse = EntryCommandParser.Parse(line, Today);

        Assert.True(parse.Succeeded, parse.Error);
        Assert.Equal(project, parse.Command!.Project);
        Assert.Equal(topic, parse.Command.Topic);
        Assert.Equal(TimeOnly.Parse(start), parse.Command.Start);
        Assert.Equal(TimeSpan.FromMinutes(minutes), parse.Command.Duration);
    }

    [Fact]
    public void A_bare_start_is_an_entry_with_no_length_yet()
    {
        var parse = EntryCommandParser.Parse("Reading 14:00", Today);

        Assert.True(parse.Succeeded, parse.Error);
        Assert.Equal(TimeSpan.Zero, parse.Command!.Duration);
    }

    [Fact]
    public void Defaults_to_the_day_being_edited()
    {
        var parse = EntryCommandParser.Parse("Reading 14:00 1h", Today);

        Assert.Equal(Today, parse.Command!.Date);
    }

    [Fact]
    public void Reads_a_leading_date()
    {
        var parse = EntryCommandParser.Parse("3.11. Reading 14:00 1h", Today);

        Assert.Equal(new DateOnly(2026, 11, 3), parse.Command!.Date);
    }

    [Fact]
    public void Reads_a_leading_date_with_a_year()
    {
        var parse = EntryCommandParser.Parse("24.12.2025. Reading 14:00 1h", Today);

        Assert.Equal(new DateOnly(2025, 12, 24), parse.Command!.Date);
    }

    [Fact]
    public void Reads_a_note_after_a_hash()
    {
        var parse = EntryCommandParser.Parse("Reading 14:00 45m # the LiteDB paper", Today);

        Assert.Equal("the LiteDB paper", parse.Command!.Note);
        Assert.Equal("Reading", parse.Command.Project);
    }

    [Fact]
    public void Refuses_an_empty_line()
    {
        var parse = EntryCommandParser.Parse("   ", Today);

        Assert.False(parse.Succeeded);
        Assert.Equal(EntryCommandParser.EmptyInputMessage, parse.Error);
    }

    [Fact]
    public void Refuses_a_line_without_a_time()
    {
        var parse = EntryCommandParser.Parse("Website (login)", Today);

        Assert.False(parse.Succeeded);
        Assert.Equal(EntryCommandParser.MissingTimeMessage, parse.Error);
    }

    [Fact]
    public void Refuses_a_line_without_a_project()
    {
        var parse = EntryCommandParser.Parse("09:00 1h", Today);

        Assert.False(parse.Succeeded);
        Assert.Equal(EntryCommandParser.MissingProjectMessage, parse.Error);
    }

    [Fact]
    public void Refuses_a_range_that_does_not_advance()
    {
        var parse = EntryCommandParser.Parse("Website 13:30-13:30", Today);

        Assert.False(parse.Succeeded);
        Assert.Equal("13:30 is not after 13:30; a range must end on the same day.", parse.Error);
    }

    [Fact]
    public void Refuses_an_impossible_clock()
    {
        var parse = EntryCommandParser.Parse("Website 25:00 1h", Today);

        Assert.False(parse.Succeeded);
        Assert.Equal("'25:00' is not a time; write it like 09:00.", parse.Error);
    }

    [Fact]
    public void Refuses_a_duration_that_is_not_a_duration()
    {
        var parse = EntryCommandParser.Parse("Website 09:00 3x", Today);

        Assert.False(parse.Succeeded);
        Assert.Equal("No time found. Finish the line with \"09:00 1h30m\" or \"09:00-13:30\".", parse.Error);
    }

    [Theory]
    [InlineData("P 12:00am 1h", 0, 0)]
    [InlineData("P 12:30pm 1h", 12, 30)]
    [InlineData("P 1:05PM 1h", 13, 5)]
    [InlineData("P 23:59 1h", 23, 59)]
    [InlineData("P 0:00-8:00", 0, 0)]
    // The framework reads 0:00am as midnight; a zero hour with a designator is not worth a rule.
    [InlineData("P 0:00am 1h", 0, 0)]
    // A colon and one digit is what a person types in a hurry: padded, in 24 hours unless a
    // designator says otherwise, so 4:30 is half past four in the morning and 12:5 is 12:05.
    [InlineData("P 12:0 2h", 12, 0)]
    [InlineData("P 4:0 1h", 4, 0)]
    [InlineData("P 4:30 1h", 4, 30)]
    [InlineData("P 12:5 1h", 12, 5)]
    [InlineData("P 9:5 1h", 9, 5)]
    [InlineData("P 7:0am 1h", 7, 0)]
    [InlineData("P 7:0pm 1h", 19, 0)]
    public void Reads_clocks_in_every_accepted_shape(string line, int hour, int minute)
    {
        var parse = EntryCommandParser.Parse(line, Today);

        Assert.True(parse.Succeeded, parse.Error);
        Assert.Equal(new TimeOnly(hour, minute), parse.Command!.Start);
    }

    [Theory]
    [InlineData("P 24:00 1h", "'24:00' is not a time; write it like 09:00.")]
    [InlineData("P 9:60 1h", "'9:60' is not a time; write it like 09:00.")]
    [InlineData("P 13:00pm 1h", "'13:00pm' is not a time; write it like 09:00.")]
    public void Refuses_impossible_clocks_with_the_offending_text(string line, string error)
    {
        var parse = EntryCommandParser.Parse(line, Today);

        Assert.False(parse.Succeeded);
        Assert.Equal(error, parse.Error);
    }

    [Fact]
    public void Refuses_a_date_that_does_not_exist()
    {
        var parse = EntryCommandParser.Parse("31.2. Website 09:00 1h", Today);

        Assert.False(parse.Succeeded);
        Assert.Equal("'31.2.' is not a real date.", parse.Error);
    }
}
