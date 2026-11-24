// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>What composing an entry produced: the entry, or the reasons it was refused.</summary>
public sealed record ComposeResult(Entry? Entry, IReadOnlyList<string> Problems)
{
    public bool Created => Entry is not null;

    /// <summary>What the day gave up for the entry: the work it crossed and what is left of it.</summary>
    public IReadOnlyList<string> Yielded { get; init; } = [];

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

        Write(plan.Names, plan.Ranges);

        return new ComposeResult(entry, []) { Yielded = plan.Yielded };
    }

    /// <summary>
    /// Writes what one decision made. The names it opened go in first and on their own, because a
    /// name has no inverse and cannot ride in a batch undo takes back; the ranges travel as one
    /// change — a batch when the decision wrote several — so one undo takes the whole of a line or a
    /// correction back, split parts and all.
    /// </summary>
    private void Write(IReadOnlyList<LedgerEvent> names, IReadOnlyList<LedgerEvent> ranges)
    {
        if (names.Count == 0 && ranges.Count == 0)
        {
            return;
        }

        List<LedgerEvent> changes = [.. names, .. AsOneDecision(ranges)];

        ledger.Commit(changes, ChangeOrigin.User);
    }

    /// <summary>The ranges one decision wrote, as one change: itself when there is one, a batch when several.</summary>
    private static IReadOnlyList<LedgerEvent> AsOneDecision(IReadOnlyList<LedgerEvent> ranges) =>
        ranges.Count switch
        {
            0 => [],
            1 => ranges,
            _ => [new Batch([.. ranges])],
        };

    /// <summary>What composing a line would write, or why it cannot be written at all.</summary>
    private sealed record Plan(
        Entry? Entry,
        List<LedgerEvent> Names,
        List<LedgerEvent> Ranges,
        List<string> Problems,
        IReadOnlyList<string> Yielded);

    /// <summary>The project and topic a line names, opened where they are new.</summary>
    private sealed record Named(Guid ProjectId, Guid? TopicId);

    /// <summary>
    /// The walk from a line to the changes it would add, in the order a person would meet the
    /// objections: the day, the names, then the rest of the day the entry has to fit into. Nothing
    /// is written here, so each step stays readable and testable on its own.
    /// </summary>
    private Plan PlanOf(EntryCommand command)
    {
        var names = new List<LedgerEvent>();
        var problems = new List<string>();

        if (DayRefusal(command.Date) is { } refusal)
        {
            return new Plan(null, names, [], [refusal], []);
        }

        if (NamesOf(command.Project, command.Topic, BreakTopic.Refused, names, problems) is not { } named)
        {
            return new Plan(null, names, [], problems, []);
        }

        var entry = EntryOf(command, named.ProjectId, named.TopicId);
        var refusals = ShapeRefusals(entry).ToList();

        if (refusals.Count > 0)
        {
            return new Plan(null, names, [], refusals, []);
        }

        // The day gives way to the line before the line joins it, so that the work it crosses never
        // shares a minute with it and undoing the entry first leaves the day as it now reads.
        var taken = DaySlot.Of(entry);
        var crossed = Crossed(entry.Date, taken).ToList();
        List<LedgerEvent> ranges = [.. Yielding(crossed, taken), new EntryLogged(entry)];

        return new Plan(entry, names, ranges, problems, [.. crossed.Select(other => GivingWay(other, taken))]);
    }

    /// <summary>What a break does with a topic: a line refuses it, because the brackets were typed.</summary>
    private enum BreakTopic
    {
        Refused,

        Dropped,
    }

    /// <summary>
    /// The project and its topic, or nothing when the names cannot be used. A break is the one
    /// project without topics. A line and a row ask this the same way, so they refuse the same
    /// things in the same words.
    /// </summary>
    private Named? NamesOf(
        string projectName,
        string? topicName,
        BreakTopic onBreak,
        List<LedgerEvent> changes,
        List<string> problems)
    {
        if (Names.Trimmed(projectName).Length == 0)
        {
            problems.Add("An entry needs a project.");
            return null;
        }

        return ResolveProject(projectName, changes, problems) is { } project
            ? TopicOf(project, topicName, onBreak, changes, problems)
            : null;
    }

    /// <summary>
    /// A break is the one project without topics: a line refuses a topic that was typed in
    /// brackets, and a row drops the one it was carrying.
    /// </summary>
    private Named? TopicOf(
        (Guid Id, string Name) project,
        string? topicName,
        BreakTopic onBreak,
        List<LedgerEvent> changes,
        List<string> problems)
    {
        if (Break.IsBreak(project.Name) && !string.IsNullOrWhiteSpace(topicName))
        {
            if (onBreak is BreakTopic.Refused)
            {
                problems.Add(Break.RefusedTopic);
                return null;
            }

            return new Named(project.Id, null);
        }

        var topicId = ResolveTopic(project.Id, topicName, changes, problems);

        return problems.Count > 0 ? null : new Named(project.Id, topicId);
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

    /// <summary>Why the ledger cannot hold this entry: its own shape, and nothing else.</summary>
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
    /// What a line would take away from the day, in the words a person reads: the work it crosses
    /// and what is left of it. Nothing is written, so the window can say it while the line is still
    /// being typed.
    /// </summary>
    public IReadOnlyList<string> Yielded(EntryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var taken = new DaySlot(command.Start.ToTimeSpan(), command.Start.ToTimeSpan() + command.Duration);

        return [.. Crossed(command.Date, taken).Select(other => GivingWay(other, taken))];
    }

    /// <summary>
    /// The entries already on the day that share a minute with the range a line or a correction
    /// writes. A stretch with no minutes shares none, so it crosses nothing: an entry written as a
    /// bare clock marks a place on the day, and the work it sits in stays whole.
    /// </summary>
    private IEnumerable<Entry> Crossed(DateOnly day, DaySlot taken, EntryId? ignore = null)
    {
        if (taken.Duration <= TimeSpan.Zero)
        {
            return [];
        }

        return ledger.State.EntriesOn(day)
            .Where(other => other.Id != ignore && other.Duration > TimeSpan.Zero && DaySlot.Of(other).Overlaps(taken));
    }

    /// <summary>
    /// What the day writes instead of crossing work: an entry keeps the minutes the line does not
    /// take, and one the line covers whole goes off the day. Every change commits with the entry.
    /// </summary>
    private static IEnumerable<LedgerEvent> Yielding(IEnumerable<Entry> crossed, DaySlot taken)
    {
        foreach (var other in crossed)
        {
            var pieces = DaySlot.Of(other).Minus(taken);

            if (pieces.Count == 0)
            {
                yield return new EntryRetracted(other);
                continue;
            }

            yield return new EntryRevised(other, Piece(other, pieces[0]));

            foreach (var piece in pieces.Skip(1))
            {
                yield return new EntryLogged(Piece(other with { Id = EntryId.Create() }, piece));
            }
        }
    }

    /// <summary>An entry as one of the pieces left of it: the same work on a shorter stretch.</summary>
    private static Entry Piece(Entry entry, DaySlot piece) =>
        entry with
        {
            Start = piece.StartsAt,
            Duration = piece.Duration,
        };

    /// <summary>What one crossed entry becomes, in the words the hint and a tool answer use.</summary>
    private string GivingWay(Entry other, DaySlot taken)
    {
        var pieces = DaySlot.Of(other).Minus(taken);

        return pieces.Count switch
        {
            0 => $"covers {Labelled(other)}",
            1 => $"trims {Labelled(other)} to {Span(pieces[0])}",
            _ => $"splits {Labelled(other)} into {Span(pieces[0])} and {Span(pieces[1])}",
        };
    }

    /// <summary>A stretch of the day as a line writes it.</summary>
    private static string Span(DaySlot piece) => $"{ClockText.Format(piece.StartsAt)}-{ClockText.Format(piece.EndsAt)}";

    /// <summary>An entry named as a sentence names it: the work, then the clocks it covers.</summary>
    private string Labelled(Entry entry) =>
        $"{LabelOf(entry)} {ClockText.Format(entry.Start)}-{ClockText.Format(entry.End)}";

    /// <summary>The work an entry holds, as a row or a page names it: the project, and its topic.</summary>
    private string LabelOf(Entry entry) =>
        ledger.State.TopicName(entry.TopicId) is { } topic
            ? $"{ledger.State.ProjectName(entry.ProjectId)} ({topic})"
            : ledger.State.ProjectName(entry.ProjectId);

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

        Write([], [new EntryRetracted(entry)]);

        return new ComposeResult(entry, []);
    }

    /// <summary>
    /// Replaces an entry with a corrected version. A correction takes the minutes it crosses the way a
    /// line does, so what it runs over gives way; the journal keeps both sides, so this is undoable
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

        if (problems.Count > 0)
        {
            return new ComposeResult(null, problems);
        }

        if (corrected == before)
        {
            return new ComposeResult(before, []);
        }

        // The revision is the last change of the batch, so undoing it first puts the row back where
        // it was while the work it crossed is still carved.
        var taken = DaySlot.Of(corrected);
        var crossed = Crossed(corrected.Date, taken, ignore: corrected.Id).ToList();
        List<LedgerEvent> ranges = [.. Yielding(crossed, taken), new EntryRevised(before, corrected)];

        Write([], ranges);

        return new ComposeResult(corrected, []) { Yielded = [.. crossed.Select(other => GivingWay(other, taken))] };
    }

    /// <summary>Everything the ledger refuses about a correction, in the order a person would read it.</summary>
    private List<string> RevisionProblems(Entry before, Entry after) =>
        [.. ReferenceProblems(after), .. ShapeProblems(before, after)];

    /// <summary>
    /// Puts a note on the stretch of the day that ended last — the work just logged, which is what a
    /// note of its own is about. It is a correction like any other, so it is undoable, and a day with
    /// nothing on it is refused rather than guessed at.
    /// </summary>
    public ComposeResult Note(DateOnly day, string note)
    {
        if (LastOn(day) is not { } entry)
        {
            return ComposeResult.Refused(NoteLine.NothingToNoteMessage);
        }

        return Revise(entry, entry with { Note = note });
    }

    /// <summary>The entry the day stopped with: nothing when it holds none.</summary>
    private Entry? LastOn(DateOnly day) =>
        ledger.State.EntriesOn(day).MaxBy(entry => entry.Start.ToTimeSpan() + entry.Duration);

    /// <summary>
    /// Points an entry at a project — and a topic of it — by name, which is how a row is corrected
    /// when the project was not in the list yet. A name that is new is opened in the same commit as
    /// the correction, so the row and the catalogue never disagree.
    /// </summary>
    public ComposeResult Point(Entry entry, string projectName, string? topicName)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var changes = new List<LedgerEvent>();
        var problems = new List<string>();

        if (DayRefusal(entry.Date) is { } refusal)
        {
            return ComposeResult.Refused(refusal);
        }

        if (NamesOf(projectName, topicName, BreakTopic.Dropped, changes, problems) is not { } names)
        {
            return new ComposeResult(null, problems);
        }

        var after = entry with { ProjectId = names.ProjectId, TopicId = names.TopicId };

        if (after == entry)
        {
            // The row already points where it should; a revival the name asked for is still written.
            Write(changes, []);

            return new ComposeResult(entry, []);
        }

        Write(changes, [new EntryRevised(entry, after)]);

        return new ComposeResult(after, []);
    }

    private IEnumerable<string> ReferenceProblems(Entry after)
    {
        if (!ledger.State.Projects.ContainsKey(after.ProjectId))
        {
            yield return "That project is no longer known.";
        }

        if (after.TopicId is not null && Break.IsBreak(ledger.State.ProjectName(after.ProjectId)))
        {
            yield return Break.RefusedTopic;
        }

        if (after.TopicId is { } topicId && !ledger.State.Topics.ContainsKey(topicId))
        {
            yield return "That topic is no longer known.";
        }
    }

    /// <summary>The shape's own objections, and the one a move to another day adds.</summary>
    private static IEnumerable<string> ShapeProblems(Entry before, Entry after) =>
        [.. ShapeRefusals(after), .. MoveRefusal(before, after)];

    /// <summary>A correction stays on its day; moving a range is logging another one.</summary>
    private static IEnumerable<string> MoveRefusal(Entry before, Entry after) =>
        after.Date != before.Date ? ["An entry cannot be moved to another day; log a new one instead."] : [];

    private (Guid Id, string Name)? ResolveProject(string name, List<LedgerEvent> changes, List<string> problems)
    {
        var match = ledger.State.MatchProject(name);

        if (Names.Ambiguous(match, name, "project") is { } ambiguous)
        {
            problems.Add(ambiguous);
            return null;
        }

        if (match.Value is { } found)
        {
            if (found.Archived)
            {
                changes.Add(Restore(found));
            }

            return (found.Id, found.Name);
        }

        var id = Guid.CreateVersion7();
        changes.Add(new ProjectOpened(id, name.Trim()));

        return (id, name.Trim());
    }

    private Guid? ResolveTopic(Guid projectId, string? name, List<LedgerEvent> changes, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var match = ledger.State.MatchTopic(projectId, name);

        if (Names.Ambiguous(match, name, "topic of this project") is { } ambiguous)
        {
            problems.Add(ambiguous);
            return null;
        }

        if (match.Value is { } found)
        {
            if (found.Archived)
            {
                changes.Add(Restore(found));
            }

            return found.Id;
        }

        var id = Guid.CreateVersion7();
        changes.Add(new TopicAdded(id, projectId, name.Trim()));

        return id;
    }

    /// <summary>
    /// A name that is out of the catalogue comes back when it is typed again: the same identity, its
    /// entries where they were, and one change — so undoing the line takes the revival with it.
    /// </summary>
    private ProjectRestored Restore(Project project)
    {
        var topics = ledger.State.Topics.Values
            .Where(topic => topic.ProjectId == project.Id && topic.Archived)
            .ToArray();
        var entries = ledger.State.Entries.Values
            .Where(entry => entry.ProjectId == project.Id)
            .ToArray();

        return new ProjectRestored(project, topics, entries, entries);
    }

    /// <summary>One topic comes back the same way, with the entries that were logged under it.</summary>
    private TopicRestored Restore(Topic topic)
    {
        var entries = ledger.State.Entries.Values
            .Where(entry => entry.TopicId == topic.Id)
            .ToArray();

        return new TopicRestored(topic, entries, entries);
    }

    /// <summary>A note is what the person meant to write, without the padding around it.</summary>
    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    /// <summary>Why the day takes no entries, or nothing when it is a working day.</summary>
    private string? DayRefusal(DateOnly day) =>
        ledger.State.TakesEntries(day)
            ? null
            : $"{day:ddd d MMMM} is marked {DayKinds.LabelOf(ledger.State.KindOf(day))}; set the day to Working day first.";
}
