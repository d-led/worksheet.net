// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;
using Worksheet.Core;
using Worksheet.Mcp;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>
/// The tools as an agent meets them: it types a line, reads a day back, asks what a project is
/// worth, and changes the catalogue — and every answer is a sentence it could show a person.
/// </summary>
public sealed class McpToolTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    /// <summary>The moment a call that says "now" arrives at.</summary>
    private static readonly DateTimeOffset At0945 = new(2026, 11, 3, 9, 45, 0, TimeSpan.Zero);

    private readonly Ledger _ledger = Core.Ledger.Open(new InMemoryJournal());
    private readonly WorksheetTools _tools;

    public McpToolTests() => _tools = new WorksheetTools(_ledger);

    private ToolOutcome Call(string name, string arguments)
    {
        using var document = JsonDocument.Parse(arguments);

        return _tools.Run(new ToolCall(name, document.RootElement.Clone()));
    }

    /// <summary>The same ledger, with the clock a call that says "now" is read at.</summary>
    private ToolOutcome CallAt(DateTimeOffset when, string name, string arguments)
    {
        using var document = JsonDocument.Parse(arguments);

        return new WorksheetTools(_ledger, () => when).Run(new ToolCall(name, document.RootElement.Clone()));
    }

    private ToolOutcome LogEntry(string line, DateOnly? on = null) =>
        Call("log_entry", JsonSerializer.Serialize(new { line, date = on is { } day ? Clocks.Text(day) : null }));

    private ToolDefinition Definition(string name) => _tools.Tools.Single(tool => tool.Name == name);

    [Fact]
    public void Every_tool_says_what_it_is_for_in_a_title_and_whether_it_only_reads()
    {
        Assert.All(_tools.Tools, tool => Assert.False(string.IsNullOrWhiteSpace(tool.Title), tool.Name));

        Assert.Equal(ToolAnnotations.Readings, Definition("day").Annotations);
        Assert.Equal(ToolAnnotations.Readings, Definition("totals").Annotations);
        Assert.Equal(ToolAnnotations.Writes, Definition("log_entry").Annotations);
        Assert.Equal(ToolAnnotations.Writes, Definition("log_now").Annotations);
        Assert.Equal(ToolAnnotations.Removes, Definition("remove_project").Annotations);
    }

    [Fact]
    public void Log_entry_says_that_one_call_records_one_range()
    {
        Assert.Contains("One call records one range", Definition("log_entry").Description);
    }

    [Fact]
    public void A_line_is_logged_and_the_answer_names_the_range()
    {
        var outcome = LogEntry("Client portal (login) 08:30 2h30m", Tuesday);

        Assert.Equal("Logged 08:30-11:00 Client portal (login) 2h30m on Tue 3 Nov 2026.", outcome.Text);
        Assert.False(outcome.IsError);
    }

    [Fact]
    public void A_note_of_its_own_notes_the_range_the_day_ended_with()
    {
        LogEntry("Reading 14:00 2h", Tuesday);

        var outcome = LogEntry("# the LiteDB paper", Tuesday);

        Assert.Equal("Noted 14:00-16:00 Reading 2h, Tue 3 Nov 2026.", outcome.Text);
        Assert.Equal("the LiteDB paper", Assert.Single(_ledger.State.Entries).Value.Note);
    }

    [Fact]
    public void A_note_with_nothing_to_note_is_refused_in_words()
    {
        var outcome = LogEntry("# the paper", Tuesday);

        Assert.True(outcome.IsError);
        Assert.Equal(NoteLine.NothingToNoteMessage, outcome.Text);
    }

    [Fact]
    public void A_line_without_its_project_words_is_refused()
    {
        var outcome = Call("log_entry", """{"line":"08:30 2h"}""");

        Assert.True(outcome.IsError);
        Assert.Contains("project", outcome.Text);
    }

    [Fact]
    public void A_line_that_does_not_fit_the_day_says_what_it_hits()
    {
        LogEntry("Client portal 08:30 2h", Tuesday);

        var outcome = LogEntry("Mobile 09:00 1h", Tuesday);

        Assert.Equal("intersects Client portal 08:30-10:30", outcome.Text);
        Assert.True(outcome.IsError);
    }

    [Fact]
    public void A_line_may_name_the_day_it_belongs_to()
    {
        LogEntry("3.11. Reading 14:00 45m", new DateOnly(2026, 11, 4));

        Assert.Contains("14:00-14:45 Reading 45m", Call("day", """{"date":"2026-11-03"}""").Text);
    }

    [Fact]
    public void A_date_that_is_not_a_date_is_refused()
    {
        var outcome = Call("log_entry", """{"line":"Website 09:00 1h","date":"the third"}""");

        Assert.Equal("'the third' is not a date; write it as 2026-11-03.", outcome.Text);
    }

    [Fact]
    public void A_call_that_says_what_is_worked_on_now_logs_it_from_the_clock()
    {
        var outcome = CallAt(At0945, "log_now", """{"project":"Client portal (login)","for":"2h"}""");

        Assert.Equal("Logged 09:45-11:45 Client portal (login) 2h on Tue 3 Nov 2026.", outcome.Text);
        Assert.False(outcome.IsError);
    }

    [Fact]
    public void A_now_call_that_names_an_end_stops_there()
    {
        var outcome = CallAt(At0945, "log_now", """{"project":"Mobile","till":"17:00"}""");

        Assert.Equal("Logged 09:45-17:00 Mobile 7h15m on Tue 3 Nov 2026.", outcome.Text);
    }

    [Fact]
    public void A_note_given_with_a_now_call_is_kept_with_the_range()
    {
        CallAt(At0945, "log_now", """{"project":"Reading","for":"45m","note":"the LiteDB paper, 2h of it"}""");

        Assert.Equal("the LiteDB paper, 2h of it", Assert.Single(_ledger.State.Entries).Value.Note);
    }

    [Fact]
    public void The_project_of_a_now_call_is_taken_whole_even_when_it_ends_in_a_span_word()
    {
        CallAt(At0945, "log_now", """{"project":"Work for","for":"1h"}""");

        Assert.Contains("09:45-10:45 Work for 1h", Call("day", """{"date":"2026-11-03"}""").Text);
    }

    [Fact]
    public void A_now_call_that_runs_into_the_day_says_what_it_hits()
    {
        LogEntry("Client portal 09:00 2h", Tuesday);

        var outcome = CallAt(At0945, "log_now", """{"project":"Mobile","for":"1h"}""");

        Assert.Equal("intersects Client portal 09:00-11:00", outcome.Text);
    }

    [Theory]
    [InlineData(
        """{"project":"Mobile"}""",
        "A range has to end somewhere: give 'for' a length, e.g. 2h, or 'till' a clock, e.g. 17:00.")]
    [InlineData("""{"project":"Mobile","for":"2h","till":"17:00"}""", "Give one of 'for' and 'till', not both.")]
    public void A_now_call_needs_exactly_one_end(string arguments, string refusal)
    {
        var outcome = CallAt(At0945, "log_now", arguments);

        Assert.True(outcome.IsError);
        Assert.Equal(refusal, outcome.Text);
    }

    [Fact]
    public void A_now_call_about_no_project_is_refused_in_words()
    {
        var outcome = CallAt(At0945, "log_now", """{"for":"2h"}""");

        Assert.True(outcome.IsError);
        Assert.Equal("log_now needs the project being worked on, such as 'Client portal (login)'.", outcome.Text);
    }

    [Fact]
    public void The_day_shows_every_range_in_order_and_the_total()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);
        LogEntry("Mobile 11:00 1h", Tuesday);

        Assert.Equal(
            "Tue 3 Nov 2026 — Working day\n08:30-11:00 Client portal (login) 2h30m\n11:00-12:00 Mobile 1h\nTotal 3h30m",
            Call("day", """{"date":"2026-11-03"}""").Text);
    }

    [Fact]
    public void A_day_calls_its_figure_a_total_and_says_what_the_work_was()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);
        LogEntry("Break 11:00 1h", Tuesday);

        Assert.Equal(
            "Tue 3 Nov 2026 — Working day\n08:30-11:00 Client portal (login) 2h30m\n11:00-12:00 Break 1h\nTotal 3h30m · without breaks 2h30m",
            Call("day", """{"date":"2026-11-03"}""").Text);
    }

    [Fact]
    public void An_empty_day_says_there_is_nothing_on_it()
    {
        Assert.Equal(
            "Thu 24 Dec 2026 — Working day\nNothing recorded.",
            Call("day", """{"date":"2026-12-24"}""").Text);
    }

    [Fact]
    public void Totals_add_the_projects_up_longest_first()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);
        LogEntry("Mobile 11:00 1h", Tuesday);

        Assert.Equal(
            "1 Nov 2026 – 30 Nov 2026\nClient portal  2h30m\nMobile  1h\nTotal  3h30m",
            Call("totals", """{"from":"2026-11-01","to":"2026-11-30"}""").Text);
    }

    [Fact]
    public void Totals_say_what_the_range_was_without_its_breaks()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);
        LogEntry("Break 11:00 1h", Tuesday);

        Assert.Equal(
            "1 Nov 2026 – 30 Nov 2026\nClient portal  2h30m\nBreak  1h\nTotal  3h30m · without breaks 2h30m",
            Call("totals", """{"from":"2026-11-01","to":"2026-11-30"}""").Text);
    }

    [Fact]
    public void A_totals_report_cannot_end_before_it_starts()
    {
        var outcome = Call("totals", """{"from":"2026-11-30","to":"2026-11-01"}""");

        Assert.Equal("A report cannot end before it starts.", outcome.Text);
    }

    [Fact]
    public void Totals_of_a_stretch_with_nothing_in_it_say_so()
    {
        Assert.Equal(
            "1 Nov 2026 – 30 Nov 2026\nNothing recorded.",
            Call("totals", """{"from":"2026-11-01","to":"2026-11-30"}""").Text);
    }

    [Fact]
    public void The_projects_carry_how_many_entries_each_holds()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);
        LogEntry("Mobile 11:00 1h", Tuesday);

        Assert.Equal("Client portal (1 entry)\nMobile (1 entry)", Call("list_projects", "{}").Text);
    }

    [Fact]
    public void The_topics_of_a_project_carry_how_many_entries_each_holds()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);

        Assert.Equal("login (1 entry)", Call("list_topics", """{"project":"Client portal"}""").Text);
    }

    [Fact]
    public void A_project_without_topics_says_so()
    {
        LogEntry("Mobile 11:00 1h", Tuesday);

        Assert.Equal("Mobile has no topics.", Call("list_topics", """{"project":"Mobile"}""").Text);
    }

    [Fact]
    public void Renaming_a_project_answers_to_the_new_name_and_undo_takes_it_back()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);

        Assert.Equal(
            "'Client portal' answers to 'Portal' now.",
            Call("rename_project", """{"project":"Client portal","name":"Portal"}""").Text);
        Assert.Contains("Portal (1 entry)", Call("list_projects", "{}").Text);

        Assert.Equal("Undone.", Call("undo", "{}").Text);
        Assert.Contains("Client portal (1 entry)", Call("list_projects", "{}").Text);
    }

    [Fact]
    public void Removing_a_project_says_that_its_entries_keep_it()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);
        LogEntry("Mobile 11:00 1h", Tuesday);

        Assert.Equal(
            "'Client portal' is out of the catalogue; its entry keeps it.",
            Call("remove_project", """{"project":"Client portal"}""").Text);
        Assert.Equal("Mobile (1 entry)", Call("list_projects", "{}").Text);
    }

    [Fact]
    public void A_line_on_a_project_out_of_the_catalogue_brings_it_back()
    {
        LogEntry("Client portal 08:30 2h", Tuesday);
        Call("remove_project", """{"project":"Client portal"}""");

        LogEntry("Client portal 13:00 1h", Tuesday);

        Assert.Contains("Client portal (2 entries)", Call("list_projects", "{}").Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Renaming_a_project_onto_a_name_in_use_says_the_two_merged()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);
        LogEntry("Mobile 11:00 1h", Tuesday);

        Assert.Equal(
            "'Client portal' merged into 'Mobile'; its entry moved there.",
            Call("rename_project", """{"project":"Client portal","name":"mobile"}""").Text);
        Assert.Equal("Mobile (2 entries)", Call("list_projects", "{}").Text);
    }

    [Fact]
    public void Removing_a_topic_says_that_its_entries_keep_it()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);

        Assert.Equal(
            "'login' is out of Client portal; its entries stay as they are.",
            Call("remove_topic", """{"project":"Client portal","topic":"login"}""").Text);
        Assert.Contains("08:30-11:00 Client portal (login) 2h30m", Call("day", """{"date":"2026-11-03"}""").Text);
    }

    [Fact]
    public void Undo_with_nothing_behind_it_says_there_is_nothing_to_undo()
    {
        var outcome = Call("undo", "{}");

        Assert.Equal("There is nothing to undo.", outcome.Text);
        Assert.True(outcome.IsError);
    }

    [Fact]
    public void An_unknown_tool_is_refused_by_name()
    {
        Assert.Equal("There is no tool called 'log'.", Call("log", "{}").Text);
    }

    [Fact]
    public void Every_tool_an_agent_is_told_about_can_be_called()
    {
        foreach (var tool in _tools.Tools)
        {
            Assert.NotEqual($"There is no tool called '{tool.Name}'.", Call(tool.Name, "{}").Text);
        }
    }

    [Fact]
    public void The_tools_are_the_eleven_an_agent_is_told_about()
    {
        Assert.Equal(
            [
                "day",
                "list_projects",
                "list_topics",
                "log_entry",
                "log_now",
                "remove_project",
                "remove_topic",
                "rename_project",
                "rename_topic",
                "totals",
                "undo",
            ],
            _tools.Tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
    }
}
