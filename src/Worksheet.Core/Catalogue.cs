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
    /// rewritten. A name another project already answers to is refused: names are how people and
    /// agents point at a project, and the first of two equal names would be unreachable.
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

        return Rename(
            project.Name,
            newName,
            "project",
            ledger.State.Projects.Values.Where(other => other.Id != project.Id).Select(other => other.Name),
            (before, after) => new ProjectRenamed(project.Id, before, after));
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

        return Rename(
            topic.Name,
            newName,
            $"topic in {project.Value.Name}",
            ledger.State.Topics.Values
                .Where(other => other.ProjectId == topic.ProjectId && other.Id != topic.Id)
                .Select(other => other.Name),
            (before, after) => new TopicRenamed(topic.Id, before, after));
    }

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
        IEnumerable<string> taken,
        Func<string, string, LedgerEvent> change)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return CatalogueResult.Refused($"A {what} needs a name.");
        }

        var trimmed = newName.Trim();

        if (taken.Any(name => string.Equals(name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return CatalogueResult.Refused($"There is already a {what} called '{trimmed}'.");
        }

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
