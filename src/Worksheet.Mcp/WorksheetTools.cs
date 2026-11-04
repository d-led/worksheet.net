// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using Worksheet.Core;

namespace Worksheet.Mcp;

/// <summary>
/// The ledger, as an agent reaches it: the entry line, the day, the totals, the catalogue, and one
/// undo. Every answer is a sentence — a refusal is a sentence too — because that is what the caller
/// can act on. Nothing here knows where the call came from.
/// </summary>
public sealed class WorksheetTools(Ledger ledger) : IToolkit
{
    private readonly EntryComposer _composer = new(ledger);
    private readonly Catalogue _catalogue = new(ledger);

    public IReadOnlyList<ToolDefinition> Tools => Definitions;

    public ToolOutcome Call(ToolCall call) =>
        call.Name switch
        {
            "log_entry" => LogEntry(call),
            "day" => Day(call),
            "totals" => Totals(call),
            "list_projects" => ToolOutcome.Said(Reports.Projects(ledger.State)),
            "list_topics" => ListTopics(call),
            "rename_project" => RenameProject(call),
            "remove_project" => RemoveProject(call),
            "rename_topic" => RenameTopic(call),
            "remove_topic" => RemoveTopic(call),
            "undo" => Undo(),
            _ => ToolOutcome.Refused($"There is no tool called '{call.Name}'."),
        };

    /// <summary>The line is the product: whatever the entry field accepts, a tool call accepts too.</summary>
    private ToolOutcome LogEntry(ToolCall call)
    {
        if (call.Text("line") is not { } line)
        {
            return ToolOutcome.Refused("log_entry needs a line, such as 'Client portal (login) 08:30 2h30m'.");
        }

        if (DayOf(call) is not { } day)
        {
            return BadDate(call.Text("date"));
        }

        var parse = EntryCommandParser.Parse(line, day);

        if (!parse.Succeeded)
        {
            return ToolOutcome.Refused(parse.Error!);
        }

        var composed = _composer.Compose(parse.Command!);

        return composed.Created
            ? ToolOutcome.Said($"Logged {Reports.Describe(ledger.State, composed.Entry!)} on {Reports.ShortDate(composed.Entry!.Date)}.")
            : ToolOutcome.Refused([.. composed.Problems]);
    }

    private ToolOutcome Day(ToolCall call) =>
        DayOf(call) is { } day ? ToolOutcome.Said(Reports.Day(ledger.State, day)) : BadDate(call.Text("date"));

    private ToolOutcome Totals(ToolCall call)
    {
        if (Moment(call.Text("from")) is not { } from || Moment(call.Text("to")) is not { } to)
        {
            return ToolOutcome.Refused("totals needs two dates, from and to, written as 2026-11-03.");
        }

        return to < from
            ? ToolOutcome.Refused("A report cannot end before it starts.")
            : ToolOutcome.Said(Reports.Totals(ledger.State, from, to));
    }

    private ToolOutcome ListTopics(ToolCall call)
    {
        var match = ledger.State.MatchProject(Names.Trimmed(call.Text("project")));

        if (Names.Refusal(match, call.Text("project") ?? string.Empty, "project") is { } refusal)
        {
            return ToolOutcome.Refused(refusal);
        }

        return ToolOutcome.Said(Reports.Topics(ledger.State, match.Value!));
    }

    private ToolOutcome RenameProject(ToolCall call) =>
        Answered(
            _catalogue.RenameProject(Names.Trimmed(call.Text("project")), Names.Trimmed(call.Text("name"))),
            $"'{Names.Trimmed(call.Text("project"))}' answers to '{Names.Trimmed(call.Text("name"))}' now.");

    private ToolOutcome RemoveProject(ToolCall call)
    {
        var project = Names.Trimmed(call.Text("project"));
        var destination = Names.Trimmed(call.Text("move_entries_to"));

        // Counted before the call: afterwards the project is archived and its entries are elsewhere.
        var held = Held(project);

        return Answered(
            _catalogue.RemoveProject(project, call.Text("move_entries_to")),
            destination.Length > 0 && held > 0
                ? $"'{project}' is out of the catalogue; its entries are under '{destination}' now."
                : $"'{project}' is out of the catalogue.");
    }

    private ToolOutcome RenameTopic(ToolCall call) =>
        Answered(
            _catalogue.RenameTopic(
                Names.Trimmed(call.Text("project")),
                Names.Trimmed(call.Text("topic")),
                Names.Trimmed(call.Text("name"))),
            $"{Names.Trimmed(call.Text("project"))} / '{Names.Trimmed(call.Text("topic"))}' is '{Names.Trimmed(call.Text("name"))}' now.");

    private ToolOutcome RemoveTopic(ToolCall call) =>
        Answered(
            _catalogue.RemoveTopic(Names.Trimmed(call.Text("project")), Names.Trimmed(call.Text("topic"))),
            $"'{Names.Trimmed(call.Text("topic"))}' is out of {Names.Trimmed(call.Text("project"))}; its entries stay on their days without a topic.");

    private ToolOutcome Undo() =>
        ledger.TryUndo(out var reason) ? ToolOutcome.Said("Undone.") : ToolOutcome.Refused(reason!);

    private static ToolOutcome Answered(CatalogueResult result, string done) =>
        result.Accepted ? ToolOutcome.Said(done) : ToolOutcome.Refused([.. result.Problems]);

    /// <summary>How many entries the project a name points at holds; nothing when the name points nowhere.</summary>
    private int Held(string projectName) =>
        ledger.State.MatchProject(projectName) is { Found: true } match
            ? ledger.State.Entries.Values.Count(entry => entry.ProjectId == match.Value!.Id)
            : 0;

    /// <summary>The day a call speaks about: the one it named, or today.</summary>
    private static DateOnly? DayOf(ToolCall call) =>
        call.Text("date") is { } text ? Moment(text) : DateOnly.FromDateTime(DateTime.Today);

    /// <summary>A date as a caller writes it, <c>2026-11-03</c>, or nothing when it does not read as one.</summary>
    private static DateOnly? Moment(string? text) =>
        DateOnly.TryParse(Names.Trimmed(text), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static ToolOutcome BadDate(string? text) =>
        ToolOutcome.Refused($"'{Names.Trimmed(text)}' is not a date; write it as 2026-11-03.");

    private static readonly ToolDefinition[] Definitions =
    [
        new(
            "log_entry",
            "Record one time range. The line is the application's own syntax, for example "
            + "'Client portal (login) 08:30 2h30m', 'Client portal (reports) 11:00-12:30' or 'Break 12:30 45m'. "
            + "A line may name its own day ('3.11. Reading 14:00 45m'); otherwise date is the day it belongs to.",
            new
            {
                type = "object",
                properties = new
                {
                    line = new { type = "string", description = "The entry line to record." },
                    date = new { type = "string", description = "The day the line belongs to, 2026-11-03. Defaults to today." },
                },
                required = new[] { "line" },
            }),
        new(
            "day",
            "One day as recorded: every range with its project and topic, then the total.",
            new
            {
                type = "object",
                properties = new
                {
                    date = new { type = "string", description = "The day, 2026-11-03. Defaults to today." },
                },
            }),
        new(
            "totals",
            "Hours per project between two days, both included, longest first.",
            new
            {
                type = "object",
                properties = new
                {
                    from = new { type = "string", description = "The first day, 2026-11-03." },
                    to = new { type = "string", description = "The last day, 2026-11-03." },
                },
                required = new[] { "from", "to" },
            }),
        new(
            "list_projects",
            "Every project in use, with how many entries each holds.",
            new { type = "object", properties = new { } }),
        new(
            "list_topics",
            "The topics of one project, with how many entries each holds.",
            new
            {
                type = "object",
                properties = new { project = new { type = "string", description = "The project's name, or enough of it to be the only one." } },
                required = new[] { "project" },
            }),
        new(
            "rename_project",
            "Give a project another name. Its entries follow, and the change is one undo.",
            new
            {
                type = "object",
                properties = new
                {
                    project = new { type = "string", description = "The project's current name." },
                    name = new { type = "string", description = "The name it should answer to." },
                },
                required = new[] { "project", "name" },
            }),
        new(
            "remove_project",
            "Take a project out of the catalogue. Refused while it holds entries, unless move_entries_to names "
            + "the project they move to; their topics stay behind with the project that goes away.",
            new
            {
                type = "object",
                properties = new
                {
                    project = new { type = "string", description = "The project to take out of use." },
                    move_entries_to = new { type = "string", description = "The project its entries move to." },
                },
                required = new[] { "project" },
            }),
        new(
            "rename_topic",
            "Give a topic of a project another name. Its entries follow, and the change is one undo.",
            new
            {
                type = "object",
                properties = new
                {
                    project = new { type = "string", description = "The project the topic belongs to." },
                    topic = new { type = "string", description = "The topic's current name." },
                    name = new { type = "string", description = "The name it should answer to." },
                },
                required = new[] { "project", "topic", "name" },
            }),
        new(
            "remove_topic",
            "Take a topic out of a project. Its entries stay on their days without a topic.",
            new
            {
                type = "object",
                properties = new
                {
                    project = new { type = "string", description = "The project the topic belongs to." },
                    topic = new { type = "string", description = "The topic to take out of use." },
                },
                required = new[] { "project", "topic" },
            }),
        new(
            "undo",
            "Undo the last change to the ledger, whoever made it: the person at the window or this caller.",
            new { type = "object", properties = new { } }),
    ];
}
