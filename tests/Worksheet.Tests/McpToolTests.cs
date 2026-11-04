// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;
using Worksheet.Core;
using Worksheet.Mcp;

namespace Worksheet.Tests;

/// <summary>
/// The tools as an agent meets them: it types a line, reads a day back, asks what a project is
/// worth, and changes the catalogue — and every answer is a sentence it could show a person.
/// </summary>
public sealed class McpToolTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    private readonly Ledger _ledger = Core.Ledger.Open(new InMemoryJournal());
    private readonly WorksheetTools _tools;

    public McpToolTests() => _tools = new WorksheetTools(_ledger);

    private ToolOutcome Call(string name, string arguments)
    {
        using var document = JsonDocument.Parse(arguments);

        return _tools.Call(new ToolCall(name, document.RootElement.Clone()));
    }

    private ToolOutcome LogEntry(string line, DateOnly? on = null) =>
        Call("log_entry", JsonSerializer.Serialize(new { line, date = on?.ToString("yyyy-MM-dd") }));

    [Fact]
    public void A_line_is_logged_and_the_answer_names_the_range()
    {
        var outcome = LogEntry("Client portal (login) 08:30 2h30m", Tuesday);

        Assert.Equal("Logged 08:30-11:00 Client portal (login) 2h30m on Tue 3 Nov 2026.", outcome.Text);
        Assert.False(outcome.IsError);
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
    public void The_day_shows_every_range_in_order_and_the_total()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);
        LogEntry("Mobile 11:00 1h", Tuesday);

        Assert.Equal(
            "Tue 3 Nov 2026 — Working day\n08:30-11:00 Client portal (login) 2h30m\n11:00-12:00 Mobile 1h\nTotal 3h30m",
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
    public void Removing_a_project_says_where_its_hours_went()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);
        LogEntry("Mobile 11:00 1h", Tuesday);

        Assert.Equal(
            "'Client portal' is out of the catalogue; its entries are under 'Mobile' now.",
            Call("remove_project", """{"project":"Client portal","move_entries_to":"Mobile"}""").Text);
        Assert.Equal("Mobile (2 entries)", Call("list_projects", "{}").Text);
    }

    [Fact]
    public void A_project_that_still_holds_entries_is_not_removed_without_them()
    {
        LogEntry("Client portal 08:30 2h", Tuesday);

        Assert.Equal(
            "Client portal still has 1 entry; name the project they move to.",
            Call("remove_project", """{"project":"Client portal"}""").Text);
    }

    [Fact]
    public void Removing_a_topic_leaves_its_entries_on_the_day()
    {
        LogEntry("Client portal (login) 08:30 2h30m", Tuesday);

        Assert.Equal(
            "'login' is out of Client portal; its entries stay on their days without a topic.",
            Call("remove_topic", """{"project":"Client portal","topic":"login"}""").Text);
        Assert.Contains("08:30-11:00 Client portal 2h30m", Call("day", """{"date":"2026-11-03"}""").Text);
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
    public void The_tools_are_the_ten_an_agent_is_told_about()
    {
        Assert.Equal(
            [
                "day",
                "list_projects",
                "list_topics",
                "log_entry",
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
