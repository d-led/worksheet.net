// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>
/// What a catalogue operation did, or the reasons it would not do it. An operation is a decision
/// about a name, so a refusal is a sentence to show, not an exception to catch.
/// </summary>
public sealed record CatalogueResult(IReadOnlyList<string> Problems)
{
    public bool Accepted => Problems.Count == 0;

    public static CatalogueResult Done { get; } = new([]);

    public static CatalogueResult Refused(params string[] problems) => new(problems);
}

/// <summary>
/// The projects and topics as a catalogue: renamed, and taken out of use with the entries they
/// hold. Every operation writes one journal change, so every operation is one undo; nothing here
/// touches what a day was declared to be.
/// </summary>
public sealed class Catalogue(Ledger ledger)
{
    /// <summary>
    /// A project keeps its identity when it changes its name, so its entries follow without being
    /// rewritten. A name another project already answers to is not a refusal: it is the same
    /// decision taken further — the two become one — so the rename merges.
    /// </summary>
    public CatalogueResult RenameProject(string projectName, string newName)
    {
        var match = ledger.State.MatchProject(projectName);

        if (Unusable(match, projectName, "project") is { } refusal)
        {
            return refusal;
        }

        var project = match.Value!;

        if (Gone(project) is { } gone)
        {
            return gone;
        }

        return TwinProject(project.Id, newName) is { } into
            ? MergeProject(project, into)
            : Rename(project.Name, newName, "project", (before, after) => new ProjectRenamed(project.Id, before, after));
    }

    /// <summary>
    /// Takes a project out of the catalogue and leaves every entry exactly as it is: the journal is
    /// append-only, so the hours of a retired project keep its name. The name stops being offered,
    /// and typing it again brings the project back. Moving hours to another project is a remap, a
    /// decision of its own, not a side effect of this one.
    /// </summary>
    public CatalogueResult RemoveProject(string projectName)
    {
        var match = ledger.State.MatchProject(projectName);

        if (Unusable(match, projectName, "project") is { } refusal)
        {
            return refusal;
        }

        var project = match.Value!;

        if (Gone(project) is { } gone)
        {
            return gone;
        }

        var topics = ledger.State.Topics.Values.Where(topic => topic.ProjectId == project.Id).ToArray();
        var entries = EntriesOf(project.Id);

        ledger.Commit([new ProjectRemoved(project, topics, entries, entries)]);

        return CatalogueResult.Done;
    }

    public CatalogueResult RenameTopic(string projectName, string topicName, string newName)
    {
        var project = ledger.State.MatchProject(projectName);

        if (Unusable(project, projectName, "project") is { } projectRefusal)
        {
            return projectRefusal;
        }

        var match = ledger.State.MatchTopic(project.Value!.Id, topicName);

        if (Unusable(match, topicName, "topic") is { } refusal)
        {
            return refusal;
        }

        var topic = match.Value!;

        if (Gone(topic) is { } gone)
        {
            return gone;
        }

        return TwinTopic(topic.ProjectId, newName, topic.Id) is { } into
            ? MergeTopic(topic, into)
            : Rename(topic.Name, newName, $"topic in {project.Value.Name}", (before, after) => new TopicRenamed(topic.Id, before, after));
    }

    /// <summary>
    /// Two projects become one. Every entry of the source keeps its hours and gains the target's
    /// name; a topic the target does not have moves under it, a topic it does have joins that one,
    /// and the source name goes out of the catalogue. It is one change, so it is one undo.
    /// </summary>
    private CatalogueResult MergeProject(Project from, Project into)
    {
        var twins = Twins(from.Id, into.Id);
        ledger.Commit([new Batch(Merging(from, into, twins))]);

        return CatalogueResult.Done;
    }

    /// <summary>Everything a project merge changes, gathered so that one undo takes the lot back.</summary>
    private LedgerEvent[] Merging(Project from, Project into, Dictionary<Guid, Topic> twins) =>
        [
            .. Revival(into),
            .. TopicsOf(from.Id).SelectMany(topic => twins.TryGetValue(topic.Id, out var twin)
                ? Joining(topic, twin)
                : [new TopicMoved(topic.Id, from.Id, into.Id)]),
            .. EntriesOf(from.Id).Select(entry => (LedgerEvent)new EntryRevised(entry, Moved(entry, into, twins))),
            new ProjectRemoved(from, [], [], []),
        ];

    /// <summary>Merging into a name that was retired brings it back, since the name is what was asked for.</summary>
    private LedgerEvent[] Revival(Project into) =>
        into.Archived ? [new ProjectRestored(into, TopicsOf(into.Id).ToArray(), [], [])] : [];

    /// <summary>An entry of the project that goes away, now naming the one that stays.</summary>
    private static Entry Moved(Entry entry, Project into, Dictionary<Guid, Topic> twins) =>
        entry with
        {
            ProjectId = into.Id,
            TopicId = entry.TopicId is { } id && twins.TryGetValue(id, out var twin) ? twin.Id : entry.TopicId,
        };

    /// <summary>Two topics become one: the entries of the source move onto the target, and it goes.</summary>
    private CatalogueResult MergeTopic(Topic from, Topic into)
    {
        ledger.Commit([new Batch(
        [
            .. Revival(into),
            .. EntriesOn(from.Id).Select(entry => (LedgerEvent)new EntryRevised(entry, entry with { TopicId = into.Id })),
            new TopicRemoved(from, [], []),
        ])]);

        return CatalogueResult.Done;
    }

    /// <summary>Merging into a topic that was out of use brings it back, since its name was asked for.</summary>
    private static LedgerEvent[] Revival(Topic into) =>
        into.Archived ? [new TopicRestored(into, [], [])] : [];

    /// <summary>A source topic taken out in favour of the target's topic of the same name.</summary>
    private static LedgerEvent[] Joining(Topic topic, Topic twin) =>
        twin.Archived
            ? [new TopicRestored(twin, [], []), new TopicRemoved(topic, [], [])]
            : [new TopicRemoved(topic, [], [])];

    /// <summary>
    /// Which of the source's topics already exist under the target, by name. Archived ones count: two
    /// topics of one project may not share a name, whether one of them is out of use or not.
    /// </summary>
    private Dictionary<Guid, Topic> Twins(Guid fromProjectId, Guid intoProjectId) =>
        TopicsOf(fromProjectId)
            .Select(topic => (topic, twin: TwinTopic(intoProjectId, topic.Name)))
            .Where(pair => pair.twin is not null)
            .ToDictionary(pair => pair.topic.Id, pair => pair.twin!);

    /// <summary>The project already answering to that name, which a rename into it merges with.</summary>
    private Project? TwinProject(Guid sourceId, string name) =>
        ledger.State.Projects.Values.FirstOrDefault(other =>
            other.Id != sourceId && SameName(other.Name, name));

    /// <summary>
    /// The topic of that project already answering to that name, unless it is the one being renamed.
    /// A topic out of use counts: two topics of one project may not share a name.
    /// </summary>
    private Topic? TwinTopic(Guid projectId, string name, Guid? except = null) =>
        ledger.State.Topics.Values.FirstOrDefault(other =>
            other.ProjectId == projectId && other.Id != except && SameName(other.Name, name));

    private static bool SameName(string name, string other) =>
        string.Equals(name.Trim(), Names.Trimmed(other), StringComparison.OrdinalIgnoreCase);

    /// <summary>Taking a topic away leaves its entries on the day with the topic they had.</summary>
    public CatalogueResult RemoveTopic(string projectName, string topicName)
    {
        var project = ledger.State.MatchProject(projectName);

        if (Unusable(project, projectName, "project") is { } projectRefusal)
        {
            return projectRefusal;
        }

        var match = ledger.State.MatchTopic(project.Value!.Id, topicName);

        if (Unusable(match, topicName, $"topic in {project.Value.Name}") is { } refusal)
        {
            return refusal;
        }

        var topic = match.Value!;

        if (Gone(topic) is { } gone)
        {
            return gone;
        }

        var entries = ledger.State.Entries.Values.Where(entry => entry.TopicId == topic.Id).ToArray();

        ledger.Commit([new TopicRemoved(topic, entries, entries)]);

        return CatalogueResult.Done;
    }

    /// <summary>One rename for projects and topics: the same refusals, the same single change.</summary>
    private CatalogueResult Rename(
        string currentName,
        string newName,
        string what,
        Func<string, string, LedgerEvent> change)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return CatalogueResult.Refused($"A {what} needs a name.");
        }

        var trimmed = newName.Trim();

        // Names are matched without regard to case, so a rename that only changes the case is no change.
        if (string.Equals(trimmed, currentName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return CatalogueResult.Done;
        }

        ledger.Commit([change(currentName, trimmed)]);

        return CatalogueResult.Done;
    }

    private Entry[] EntriesOf(Guid projectId) =>
        [.. ledger.State.Entries.Values.Where(entry => entry.ProjectId == projectId)];

    private IEnumerable<Entry> EntriesOn(Guid topicId) =>
        ledger.State.Entries.Values.Where(entry => entry.TopicId == topicId);

    private IEnumerable<Topic> TopicsOf(Guid projectId) =>
        ledger.State.Topics.Values.Where(topic => topic.ProjectId == projectId);

    /// <summary>An archived project or topic is still in the ledger — it is simply not an answer any more.</summary>
    private static CatalogueResult? Gone(Project project) =>
        project.Archived ? AlreadyGone(project.Name) : null;

    private static CatalogueResult? Gone(Topic topic) =>
        topic.Archived ? AlreadyGone(topic.Name) : null;

    private static CatalogueResult AlreadyGone(string name) =>
        CatalogueResult.Refused(Names.OutOfTheCatalogue(name));

    /// <summary>
    /// Nothing, or why a name does not point at one thing. The wording lives in <see cref="Names"/>
    /// so that a refusal sounds the same wherever a name is typed.
    /// </summary>
    private static CatalogueResult? Unusable<T>(NameMatch<T> match, string name, string what)
        where T : class =>
        Names.Refusal(match, name, what) is { } refusal ? CatalogueResult.Refused(refusal) : null;
}
