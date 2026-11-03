// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>What composing an entry produced: the entry, or the reasons it was refused.</summary>
public sealed record ComposeResult(Entry? Entry, IReadOnlyList<string> Problems)
{
    public bool Created => Entry is not null;

    public static ComposeResult Refused(params string[] problems) => new(null, problems);
}

/// <summary>
/// Turns a parsed command into journal changes: it resolves the project and topic by name,
/// opening them when the user is naming something new, and refuses what the ledger cannot hold.
/// </summary>
public sealed class EntryComposer(Ledger ledger)
{
    public ComposeResult Compose(EntryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = PlanOf(command);

        if (plan.Entry is not { } entry)
        {
            return new ComposeResult(null, plan.Problems);
        }

        ledger.Commit(plan.Changes, ChangeOrigin.User);

        return new ComposeResult(entry, []);
    }

    /// <summary>What composing a line would write, or why it cannot be written at all.</summary>
    private sealed record Plan(Entry? Entry, List<LedgerEvent> Changes, List<string> Problems);

    /// <summary>
    /// The walk from a line to the changes it would add, in the order a person would meet the
    /// objections: the day, the names, then the rest of the day the entry has to fit into. Nothing
    /// is written here, so each step stays readable and testable on its own.
    /// </summary>
    private Plan PlanOf(EntryCommand command)
    {
        var changes = new List<LedgerEvent>();
        var problems = new List<string>();

        if (DayRefusal(command.Date) is { } refusal)
        {
            return new Plan(null, changes, [refusal]);
        }

        var projectId = ResolveProject(command.Project, changes, problems);

        if (projectId is not { } project)
        {
            return new Plan(null, changes, problems);
        }

        var topicId = ResolveTopic(project, command.Topic, changes, problems);

        if (problems.Count > 0)
        {
            return new Plan(null, changes, problems);
        }

        var entry = EntryOf(command, project, topicId);
        var refusals = Refusals(entry);

        if (refusals.Count > 0)
        {
            return new Plan(null, changes, refusals);
        }

        changes.Add(new EntryLogged(entry));

        return new Plan(entry, changes, problems);
    }

    private static Entry EntryOf(EntryCommand command, Guid projectId, Guid? topicId) =>
        new()
        {
            Id = EntryId.Create(),
            ProjectId = projectId,
            TopicId = topicId,
            Date = command.Date,
            Start = command.Start,
            Duration = command.Duration,
            Note = command.Note,
        };

    /// <summary>Why the ledger cannot hold this entry: its shape, then the day it has to fit into.</summary>
    private List<string> Refusals(Entry entry) => [.. ShapeRefusals(entry), .. Conflicts(entry)];

    private static IEnumerable<string> ShapeRefusals(Entry entry)
    {
        if (entry.Duration < TimeSpan.Zero)
        {
            yield return "A duration cannot be negative.";
        }

        if (entry.CrossesMidnight)
        {
            yield return $"{entry.Start:HH:mm} + {Durations.Format(entry.Duration)} runs past midnight; split it into two entries.";
        }
    }

    /// <summary>
    /// Whether a line would be refused for intersecting the day, without writing anything. The
    /// window uses it to complain while the line is being typed rather than after Enter.
    /// </summary>
    public IReadOnlyList<string> Preview(EntryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Duration <= TimeSpan.Zero)
        {
            return [];
        }

        var probe = new Entry
        {
            Id = EntryId.Create(),
            ProjectId = Guid.Empty,
            Date = command.Date,
            Start = command.Start,
            Duration = command.Duration,
        };

        return Conflicts(probe);
    }

    /// <summary>Retracts an entry, keeping its whole content in the journal so that undo can restore it.</summary>
    public ComposeResult Retract(EntryId id)
    {
        if (!ledger.State.Entries.TryGetValue(id, out var entry))
        {
            return ComposeResult.Refused("That entry is no longer on the day.");
        }

        if (DayRefusal(entry.Date) is { } refusal)
        {
            return ComposeResult.Refused(refusal);
        }

        ledger.Commit([new EntryRetracted(entry)]);

        return new ComposeResult(entry, []);
    }

    /// <summary>
    /// Replaces an entry with a corrected version. The journal keeps both sides, so this is undoable
    /// and the history says what the correction was rather than silently overwriting it.
    /// </summary>
    public ComposeResult Revise(Entry before, Entry after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var corrected = after with { Note = NormalizeNote(after.Note) };
        var problems = RevisionProblems(before, corrected);

        if (DayRefusal(corrected.Date) is { } refusal)
        {
            problems = [refusal, .. problems];
        }

        if (problems.Count == 0)
        {
            problems = Conflicts(corrected, ignore: corrected.Id);
        }

        if (problems.Count > 0)
        {
            return new ComposeResult(null, problems);
        }

        if (corrected == before)
        {
            return new ComposeResult(before, []);
        }

        ledger.Commit([new EntryRevised(before, corrected)]);

        return new ComposeResult(corrected, []);
    }

    /// <summary>Everything the ledger refuses about a correction, in the order a person would read it.</summary>
    private List<string> RevisionProblems(Entry before, Entry after) =>
        [.. ReferenceProblems(after), .. ShapeProblems(before, after)];

    private IEnumerable<string> ReferenceProblems(Entry after)
    {
        if (!ledger.State.Projects.ContainsKey(after.ProjectId))
        {
            yield return "That project is no longer known.";
        }

        if (after.TopicId is { } topicId && !ledger.State.Topics.ContainsKey(topicId))
        {
            yield return "That topic is no longer known.";
        }
    }

    private static IEnumerable<string> ShapeProblems(Entry before, Entry after)
    {
        foreach (var problem in ShapeRefusals(after))
        {
            yield return problem;
        }

        if (after.Date != before.Date)
        {
            yield return "An entry cannot be moved to another day; log a new one instead.";
        }
    }

    private Guid? ResolveProject(string name, List<LedgerEvent> changes, List<string> problems)
    {
        var match = ledger.State.MatchProject(name);

        if (match.Ambiguous)
        {
            problems.Add($"'{name.Trim()}' fits more than one project; write more of the name.");
            return null;
        }

        if (match.Found)
        {
            return match.Value!.Id;
        }

        var id = Guid.CreateVersion7();
        changes.Add(new ProjectOpened(id, name.Trim()));

        return id;
    }

    private Guid? ResolveTopic(Guid projectId, string? name, List<LedgerEvent> changes, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var match = ledger.State.MatchTopic(projectId, name);

        if (match.Ambiguous)
        {
            problems.Add($"'{name.Trim()}' fits more than one topic of this project; write more of the name.");
            return null;
        }

        if (match.Found)
        {
            return match.Value!.Id;
        }

        var id = Guid.CreateVersion7();
        changes.Add(new TopicAdded(id, projectId, name.Trim()));

        return id;
    }

    /// <summary>A note is what the person meant to write, without the padding around it.</summary>
    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    /// <summary>Why the day takes no entries, or nothing when it is a working day.</summary>
    private string? DayRefusal(DateOnly day) =>
        ledger.State.TakesEntries(day)
            ? null
            : $"{day:ddd d MMMM} is marked {DayKinds.LabelOf(ledger.State.KindOf(day))}; set the day to Working day first.";

    /// <summary>
    /// The day is a sequence, so two entries may not share a minute. Each conflict names the entry
    /// that is already there, because "overlaps something" is not something a person can act on.
    /// </summary>
    private List<string> Conflicts(Entry entry, EntryId? ignore = null)
    {
        if (entry.Duration == TimeSpan.Zero)
        {
            return [];
        }

        return
        [
            .. ledger.State.EntriesOn(entry.Date)
                .Where(other => IsDifferentAndOverlapping(other, entry, ignore))
                .Select(DescribeConflict),
        ];
    }

    private static bool IsDifferentAndOverlapping(Entry other, Entry entry, EntryId? ignore) =>
        other.Duration != TimeSpan.Zero && other.Id != ignore && Overlaps(other, entry);

    private string DescribeConflict(Entry other)
    {
        var project = ledger.State.ProjectName(other.ProjectId);
        var topic = ledger.State.TopicName(other.TopicId);
        var label = topic is null ? project : $"{project} ({topic})";

        return $"intersects {label} {other.Start:HH:mm}-{other.End:HH:mm}";
    }

    private static bool Overlaps(Entry left, Entry right) =>
        left.Start.ToTimeSpan() < right.Start.ToTimeSpan() + right.Duration
        && right.Start.ToTimeSpan() < left.Start.ToTimeSpan() + left.Duration;
}
