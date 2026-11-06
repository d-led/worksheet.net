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
public sealed class WorksheetTools : IToolkit
{
    private readonly Ledger _ledger;
    private readonly EntryComposer _composer;
    private readonly Catalogue _catalogue;

    /// <summary>What each tool name reaches. One line per name, so the list reads as the tool set.</summary>
    private readonly Dictionary<string, Func<ToolCall, ToolOutcome>> _handlers;

    public WorksheetTools(Ledger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        _ledger = ledger;
        _composer = new EntryComposer(ledger);
        _catalogue = new Catalogue(ledger);
        _handlers = new Dictionary<string, Func<ToolCall, ToolOutcome>>(StringComparer.Ordinal)
        {
            ["log_entry"] = LogEntry,
            ["day"] = Day,
            ["totals"] = Totals,
            ["list_projects"] = _ => ToolOutcome.Said(Reports.Projects(_ledger.State)),
            ["list_topics"] = ListTopics,
            ["rename_project"] = RenameProject,
            ["remove_project"] = RemoveProject,
            ["rename_topic"] = RenameTopic,
            ["remove_topic"] = RemoveTopic,
            ["undo"] = _ => Undo(),
        };
    }

    public IReadOnlyList<ToolDefinition> Tools => Definitions;

    public ToolOutcome Run(ToolCall toolCall) =>
        _handlers.TryGetValue(toolCall.Name, out var handler)
            ? handler(toolCall)
            : ToolOutcome.Refused($"There is no tool called '{toolCall.Name}'.");

    /// <summary>The line is the product: whatever the entry field accepts, a tool call accepts too.</summary>
    private ToolOutcome LogEntry(ToolCall call) =>
        call.Text("line") is not { } line
            ? ToolOutcome.Refused("log_entry needs a line, such as 'Client portal (login) 08:30 2h30m'.")
            : DayOf(call) is not { } day
                ? BadDate(call.Text("date"))
                : Log(line, day);

    /// <summary>
    /// The line in the order it carries what it says — a name, a span, then its note — so a note of
    /// its own is read only for a line that cannot be an entry at all.
    /// </summary>
    private ToolOutcome Log(string line, DateOnly day)
    {
        var parse = EntryCommandParser.Parse(line, day, _ledger.State.EndOfLast);

        if (parse.Succeeded)
        {
            return LogEntry(parse.Command!);
        }

        return NoteLine.Parse(line) is { } note ? Note(note, day) : ToolOutcome.Refused(parse.Error!);
    }

    private ToolOutcome LogEntry(EntryCommand command)
    {
        var composed = _composer.Compose(command);

        return composed.Created
            ? ToolOutcome.Said($"Logged {Reports.Describe(_ledger.State, composed.Entry!)} on {Reports.ShortDate(composed.Entry!.Date)}.")
            : ToolOutcome.Refused([.. composed.Problems]);
    }

    /// <summary>A line that is only a note belongs to the range the day ended with, as it does in the window.</summary>
    private ToolOutcome Note(NoteParse note, DateOnly day)
    {
        var composed = note.Succeeded
            ? _composer.Note(day, note.Note!)
            : ComposeResult.Refused(note.Error!);

        return composed.Created
            ? ToolOutcome.Said($"Noted {Reports.Describe(_ledger.State, composed.Entry!)}, {Reports.ShortDate(day)}.")
            : ToolOutcome.Refused([.. composed.Problems]);
    }

    private ToolOutcome Day(ToolCall call) =>
        DayOf(call) is { } day ? ToolOutcome.Said(Reports.Day(_ledger.State, day)) : BadDate(call.Text("date"));

    private ToolOutcome Totals(ToolCall call)
    {
        if (Moment(call.Text("from")) is not { } from || Moment(call.Text("to")) is not { } to)
        {
            return ToolOutcome.Refused("totals needs two dates, from and to, written as 2026-11-03.");
        }

        return to < from
            ? ToolOutcome.Refused("A report cannot end before it starts.")
            : ToolOutcome.Said(Reports.Totals(_ledger.State, from, to));
    }

    private ToolOutcome ListTopics(ToolCall call)
    {
        var match = _ledger.State.MatchProject(Names.Trimmed(call.Text("project")));

        if (Names.Refusal(match, call.Text("project") ?? string.Empty, "project") is { } refusal)
        {
            return ToolOutcome.Refused(refusal);
        }

        return ToolOutcome.Said(Reports.Topics(_ledger.State, match.Value!));
    }

    private ToolOutcome RenameProject(ToolCall call)
    {
        var project = Names.Trimmed(call.Text("project"));
        var name = Names.Trimmed(call.Text("name"));
        var held = Held(project);
        var result = _catalogue.RenameProject(project, name);

        if (!result.Accepted)
        {
            return ToolOutcome.Refused([.. result.Problems]);
        }

        var into = Merged(project) ? Surviving(name) : null;

        return ToolOutcome.Said(
            into is null
                ? $"'{project}' answers to '{name}' now."
                : $"'{project}' merged into '{into}'; {held switch { 0 => "it held no entries", 1 => "its entry moved there", _ => $"its {held} entries moved there" }}.");
    }

    private ToolOutcome RemoveProject(ToolCall call)
    {
        var project = Names.Trimmed(call.Text("project"));
        var held = Held(project);

        return Answered(
            _catalogue.RemoveProject(project),
            held switch
            {
                0 => $"'{project}' is out of the catalogue.",
                1 => $"'{project}' is out of the catalogue; its entry keeps it.",
                _ => $"'{project}' is out of the catalogue; its {held} entries keep it.",
            });
    }

    private ToolOutcome RenameTopic(ToolCall call)
    {
        var project = Names.Trimmed(call.Text("project"));
        var topic = Names.Trimmed(call.Text("topic"));
        var name = Names.Trimmed(call.Text("name"));
        var result = _catalogue.RenameTopic(project, topic, name);

        if (!result.Accepted)
        {
            return ToolOutcome.Refused([.. result.Problems]);
        }

        return ToolOutcome.Said(
            Merged(topic, project)
                ? $"{project} / '{topic}' merged into '{name}'."
                : $"{project} / '{topic}' is '{name}' now.");
    }

    private ToolOutcome RemoveTopic(ToolCall call) =>
        Answered(
            _catalogue.RemoveTopic(Names.Trimmed(call.Text("project")), Names.Trimmed(call.Text("topic"))),
            $"'{Names.Trimmed(call.Text("topic"))}' is out of {Names.Trimmed(call.Text("project"))}; its entries stay as they are.");

    private ToolOutcome Undo() =>
        _ledger.TryUndo(out var reason) ? ToolOutcome.Said("Undone.") : ToolOutcome.Refused(reason!);

    private static ToolOutcome Answered(CatalogueResult result, string done) =>
        result.Accepted ? ToolOutcome.Said(done) : ToolOutcome.Refused([.. result.Problems]);

    /// <summary>How many entries the project a name points at holds; nothing when the name points nowhere.</summary>
    private int Held(string projectName) =>
        _ledger.State.MatchProject(projectName) is { Found: true } match
            ? _ledger.State.Entries.Values.Count(entry => entry.ProjectId == match.Value!.Id)
            : 0;

    /// <summary>
    /// Whether the project a rename started from is now out of the catalogue, which is what a merge
    /// does to it — the answer a caller gets has to say so, since the two are no longer apart.
    /// </summary>
    private bool Merged(string projectName) =>
        _ledger.State.MatchProject(projectName) is { Found: true } match && match.Value!.Archived;

    private bool Merged(string topicName, string projectName) =>
        _ledger.State.MatchProject(projectName) is not { Found: true } match
        || _ledger.State.MatchTopic(match.Value!.Id, topicName) is not { Found: true } topic
        || topic.Value!.Archived;

    /// <summary>The name a merged project answers to, as the journal spells it rather than as it was typed.</summary>
    private string Surviving(string name) =>
        _ledger.State.Projects.Values
            .FirstOrDefault(project => !project.Archived && string.Equals(project.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            ?.Name ?? name;

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
            "Record exactly one time range. The line is the application's own syntax, for example "
            + "'Client portal (login) 08:30 2h30m', 'Client portal (reports) 11:00-12:30' or 'Break 12:30 45m'. "
            + "A line may leave its start out — 'Client portal till 16:00', 'Client portal for 1h30m' — "
            + "and then begins where the last range of that day ended. "
            + "One call records one range: a day of four ranges is four calls. "
            + "A line may name its own day ('3.11. Reading 14:00 45m'); otherwise date is the day it belongs to. "
            + "A note is written after the range, with or without a '#': 'Reading 14:00 45m # the LiteDB paper'. "
            + "A line that is only a note ('# the paper') notes the range the day ended with. "
            + "A range that cannot be recorded is refused with a sentence saying why, and nothing is written.",
            ToolSchema.Of(
                ("line", ToolProperty.Text("The entry line to record, in the application's own syntax.")),
                ("date", ToolProperty.Text("The day the line belongs to, 2026-11-03. Defaults to today.", required: false))),
            Title: "Log a time range",
            Annotations: ToolAnnotations.Writes),
        new(
            "day",
            "One day as recorded: every range with its project and topic, then the total.",
            ToolSchema.Of(
                ("date", ToolProperty.Text("The day, 2026-11-03. Defaults to today.", required: false))),
            Title: "Read a day",
            Annotations: ToolAnnotations.Readings),
        new(
            "totals",
            "Hours per project between two days, both included, longest first.",
            ToolSchema.Of(
                ("from", ToolProperty.Text("The first day, 2026-11-03.")),
                ("to", ToolProperty.Text("The last day, 2026-11-03."))),
            Title: "Totals for a range",
            Annotations: ToolAnnotations.Readings),
        new(
            "list_projects",
            "Every project in use, with how many entries each holds.",
            ToolSchema.Nothing,
            Title: "List projects",
            Annotations: ToolAnnotations.Readings),
        new(
            "list_topics",
            "The topics of one project, with how many entries each holds.",
            ToolSchema.Of(
                ("project", ToolProperty.Text("The project's name, or enough of it to be the only one."))),
            Title: "List the topics of a project",
            Annotations: ToolAnnotations.Readings),
        new(
            "rename_project",
            "Give a project another name. Its entries follow, and the change is one undo. A name that "
            + "another project already answers to merges the two into that one, with the entries of both; "
            + "the answer says when that happened.",
            ToolSchema.Of(
                ("project", ToolProperty.Text("The project's current name.")),
                ("name", ToolProperty.Text("The name it should answer to."))),
            Title: "Rename a project",
            Annotations: ToolAnnotations.Writes),
        new(
            "remove_project",
            "Take a project out of the catalogue. Its entries are not touched: they keep the project they "
            + "were logged under. The name stops being offered, and typing it again brings the project back.",
            ToolSchema.Of(
                ("project", ToolProperty.Text("The project to take out of use."))),
            Title: "Remove a project",
            Annotations: ToolAnnotations.Removes),
        new(
            "rename_topic",
            "Give a topic of a project another name. Its entries follow, and the change is one undo. A "
            + "name another topic of that project already answers to merges the two, and the answer says so.",
            ToolSchema.Of(
                ("project", ToolProperty.Text("The project the topic belongs to.")),
                ("topic", ToolProperty.Text("The topic's current name.")),
                ("name", ToolProperty.Text("The name it should answer to."))),
            Title: "Rename a topic",
            Annotations: ToolAnnotations.Writes),
        new(
            "remove_topic",
            "Take a topic out of a project. Its entries stay on their days without a topic.",
            ToolSchema.Of(
                ("project", ToolProperty.Text("The project the topic belongs to.")),
                ("topic", ToolProperty.Text("The topic to take out of use."))),
            Title: "Remove a topic",
            Annotations: ToolAnnotations.Removes),
        new(
            "undo",
            "Undo the last change to the ledger, whoever made it: the person at the window or this caller.",
            ToolSchema.Nothing,
            Title: "Undo the last change",
            Annotations: ToolAnnotations.Writes),
    ];
}
