// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;

namespace Worksheet.Core;

/// <summary>
/// The fold of a journal. One mutable instance owns its collections; the ledger is the only
/// writer, so callers read through the read-only views and never keep them across a change.
/// </summary>
public sealed class LedgerState
{
    private readonly Dictionary<Guid, Project> _projects = [];
    private readonly Dictionary<Guid, Topic> _topics = [];
    private readonly Dictionary<EntryId, Entry> _entries = [];
    private readonly Dictionary<DateOnly, DayKind> _declaredKinds = [];

    private LedgerState()
    {
    }

    public static LedgerState Create() => new();

    public long Version { get; private set; }

    public IReadOnlyDictionary<Guid, Project> Projects => _projects;

    public IReadOnlyDictionary<Guid, Topic> Topics => _topics;

    public IReadOnlyDictionary<EntryId, Entry> Entries => _entries;

    /// <summary>Days that were declared to be something; every other day follows the calendar.</summary>
    public IReadOnlyDictionary<DateOnly, DayKind> DeclaredKinds => _declaredKinds;

    public LedgerState Apply(LedgerEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);

        Record(change);
        Version++;

        return this;
    }

    private void Record(LedgerEvent change)
    {
        switch (change)
        {
            case ProjectOpened opened:
                _projects[opened.ProjectId] = new Project(opened.ProjectId, opened.Name.Trim(), Archived: false);
                break;

            case TopicAdded added:
                _topics[added.TopicId] = new Topic(added.TopicId, added.ProjectId, added.Name.Trim(), Archived: false);
                break;

            case EntryLogged logged:
                _entries[logged.Entry.Id] = logged.Entry;
                break;

            case EntryRevised revised:
                _entries[revised.After.Id] = revised.After;
                break;

            case EntryRetracted retracted:
                _entries.Remove(retracted.Entry.Id);
                break;

            case DayKindChanged changed:
                RecordKind(changed);
                break;

            case ProjectRenamed renamed:
                RecordRename(_projects, renamed.ProjectId, renamed.After, (project, name) => project with { Name = name });
                break;

            case TopicRenamed renamed:
                RecordRename(_topics, renamed.TopicId, renamed.After, (topic, name) => topic with { Name = name });
                break;

            case ProjectRemoved removed:
                RecordRemoval(removed.Project, removed.Topics, removed.Before, removed.After, archived: true);
                break;

            case ProjectRestored restored:
                RecordRemoval(restored.Project, restored.Topics, restored.Before, restored.After, archived: false);
                break;

            case TopicRemoved removed:
                RecordRemoval(removed.Topic, removed.Before, removed.After, archived: true);
                break;

            case TopicRestored restored:
                RecordRemoval(restored.Topic, restored.Before, restored.After, archived: false);
                break;

            case TopicMoved moved:
                _topics[moved.TopicId] = _topics[moved.TopicId] with { ProjectId = moved.ToProjectId };
                break;

            case Batch batch:
                foreach (var part in batch.Changes)
                {
                    Record(part);
                }

                break;

            default:
                throw new NotSupportedException($"{change.GetType().Name} is not a known journal change.");
        }
    }

    public LedgerState Apply(IEnumerable<LedgerEvent> changes)
    {
        foreach (var change in changes)
        {
            Apply(change);
        }

        return this;
    }

    public IEnumerable<Entry> EntriesOn(DateOnly day) =>
        _entries.Values.Where(entry => entry.Date == day).OrderBy(entry => entry.Start).ThenBy(entry => entry.Id.Value);

    /// <summary>
    /// Where the day stops: the end of the last stretch logged on it, or nothing when the day is
    /// empty. A stretch that ends exactly at midnight ends the day too, so it is no start for
    /// another entry on that day.
    /// </summary>
    public TimeOnly? EndOfLast(DateOnly day)
    {
        var ends = EntriesOn(day)
            .Select(entry => entry.Start.ToTimeSpan() + entry.Duration)
            .Where(end => end > TimeSpan.Zero && end < TimeSpan.FromDays(1))
            .ToList();

        return ends.Count == 0 ? null : TimeOnly.FromTimeSpan(ends.Max());
    }

    /// <summary>What the day is: what was declared for it, or what the calendar says.</summary>
    public DayKind KindOf(DateOnly day) =>
        _declaredKinds.TryGetValue(day, out var kind) ? kind : DayKinds.DefaultOf(day);

    /// <summary>What was declared for the day, or nothing while the calendar decides.</summary>
    public DayKind? DeclaredKindOf(DateOnly day) =>
        _declaredKinds.TryGetValue(day, out var kind) ? kind : null;

    /// <summary>Whether the day can be worked on, and therefore edited.</summary>
    public bool TakesEntries(DateOnly day) => DayKinds.TakesEntries(KindOf(day));

    public TimeSpan TotalOn(DateOnly day) =>
        _entries.Values.Where(entry => entry.Date == day).Aggregate(TimeSpan.Zero, (total, entry) => total + entry.Duration);

    public string ProjectName(Guid projectId) =>
        _projects.TryGetValue(projectId, out var project) ? project.Name : "?";

    public string? TopicName(Guid? topicId) =>
        topicId is { } id && _topics.TryGetValue(id, out var topic) ? topic.Name : null;

    /// <summary>Exact name first (case-insensitive), then a unique prefix.</summary>
    public NameMatch<Project> MatchProject(string name) =>
        Match(_projects.Values, project => project.Name, name);

    public NameMatch<Topic> MatchTopic(Guid projectId, string name) =>
        Match(_topics.Values.Where(topic => topic.ProjectId == projectId), topic => topic.Name, name);

    public LedgerSnapshot ToSnapshot() =>
        new(Version, [.. _projects.Values], [.. _topics.Values], [.. _entries.Values])
        {
            Days = [.. _declaredKinds.Select(pair => new DayKindMark(pair.Key, pair.Value))],
        };

    public static LedgerState Restore(LedgerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var state = new LedgerState { Version = snapshot.Version };

        foreach (var project in snapshot.Projects)
        {
            state._projects[project.Id] = project;
        }

        foreach (var topic in snapshot.Topics)
        {
            state._topics[topic.Id] = topic;
        }

        foreach (var entry in snapshot.Entries)
        {
            state._entries[entry.Id] = entry;
        }

        foreach (var mark in snapshot.Days)
        {
            state._declaredKinds[mark.Date] = mark.Kind;
        }

        return state;
    }

    private void RecordKind(DayKindChanged changed)
    {
        if (changed.After is { } kind)
        {
            _declaredKinds[changed.Date] = kind;
            return;
        }

        _declaredKinds.Remove(changed.Date);
    }

    private static void RecordRename<T>(Dictionary<Guid, T> items, Guid id, string name, Func<T, string, T> renamed)
    {
        if (items.TryGetValue(id, out var item))
        {
            items[id] = renamed(item, name.Trim());
        }
    }

    /// <summary>
    /// A removal archives the project and its topics; its inverse brings them back. The event carries
    /// the entries of both sides, so a fold only has to read the side it means.
    /// </summary>
    private void RecordRemoval(Project project, Topic[] topics, Entry[] before, Entry[] after, bool archived)
    {
        _projects[project.Id] = project with { Archived = archived };

        foreach (var topic in topics)
        {
            _topics[topic.Id] = topic with { Archived = archived };
        }

        foreach (var entry in archived ? after : before)
        {
            _entries[entry.Id] = entry;
        }
    }

    private void RecordRemoval(Topic topic, Entry[] before, Entry[] after, bool archived)
    {
        _topics[topic.Id] = topic with { Archived = archived };

        foreach (var entry in archived ? after : before)
        {
            _entries[entry.Id] = entry;
        }
    }

    private static NameMatch<T> Match<T>(IEnumerable<T> candidates, Func<T, string> nameOf, string wanted)
        where T : class
    {
        var trimmed = wanted.Trim();

        return trimmed.Length == 0
            ? NameMatch<T>.None
            : Exact(candidates, nameOf, trimmed) ?? ByPrefix(candidates, nameOf, trimmed);
    }

    private static NameMatch<T>? Exact<T>(IEnumerable<T> candidates, Func<T, string> nameOf, string wanted)
        where T : class
    {
        foreach (var candidate in candidates)
        {
            if (string.Equals(nameOf(candidate), wanted, StringComparison.OrdinalIgnoreCase))
            {
                return new NameMatch<T>(candidate, false);
            }
        }

        return null;
    }

    private static NameMatch<T> ByPrefix<T>(IEnumerable<T> candidates, Func<T, string> nameOf, string wanted)
        where T : class
    {
        T? match = null;
        var count = 0;

        foreach (var candidate in candidates)
        {
            if (!nameOf(candidate).StartsWith(wanted, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            match = candidate;
            count++;
        }

        return count switch
        {
            0 => NameMatch<T>.None,
            1 => new NameMatch<T>(match, false),
            _ => NameMatch<T>.Ambiguity,
        };
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() =>
        $"v{Version}: {_projects.Count} projects, {_topics.Count} topics, {_entries.Count} entries";
}
