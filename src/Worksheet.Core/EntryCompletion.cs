// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.RegularExpressions;

namespace Worksheet.Core;

/// <summary>What is being named in a half-written line, and the known names that continue it.</summary>
public sealed record EntryProposals(EntryStep Field, IReadOnlyList<string> Names)
{
    /// <summary>Nothing to name: the line has left the names behind and is asking for a time.</summary>
    public static EntryProposals None { get; } = new(EntryStep.Start, []);

    public bool Any => Names.Count > 0;
}

/// <summary>
/// What can be typed next, and what a chosen name does to the line. A project is named first; once
/// its bracket is open, its own topics are named inside it. Both are prefix matches, so the same
/// project is never opened twice under two spellings.
/// </summary>
public static partial class EntryCompletion
{
    private const int Limit = 6;

    /// <summary>The known names that continue the word being typed, in alphabetical order.</summary>
    public static EntryProposals Proposals(LedgerState state, string? text)
    {
        ArgumentNullException.ThrowIfNull(state);

        var draft = DraftOf(text ?? string.Empty);

        return draft.Field switch
        {
            EntryStep.Project => new EntryProposals(EntryStep.Project, ProjectNames(state, draft.Word)),
            EntryStep.Topic => new EntryProposals(EntryStep.Topic, TopicNames(state, draft.Project, draft.Word)),
            _ => EntryProposals.None,
        };
    }

    /// <summary>
    /// The line with <paramref name="name"/> written over the word being typed, and the bracket
    /// that comes next either opened or closed, so that the hand keeps moving.
    /// </summary>
    public static string Take(LedgerState state, string? text, string name)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var draft = DraftOf(text ?? string.Empty);

        return draft.Field switch
        {
            EntryStep.Project => Project(draft, state, name),
            EntryStep.Topic => Join(draft.Before, $"{name})", draft.After),
            _ => text ?? string.Empty,
        };
    }

    /// <summary>
    /// A completed project, followed by its open bracket when it has topics to name and no time has
    /// been typed yet, so that the next thing to type is already expected.
    /// </summary>
    private static string Project(Draft draft, LedgerState state, string name) =>
        draft.After.Trim().Length == 0 && HasTopics(state, name)
            ? $"{draft.Before}{name} ("
            : Join(draft.Before, name, draft.After);

    /// <summary>A half-written line cut into the part before the name, the name, and the rest.</summary>
    private readonly record struct Draft(string Before, string Word, string After, string? Project, EntryStep Field);

    /// <summary>
    /// Fields are found from the right, as the parser does: the time and the note come last, before
    /// them the brackets, and before those the project — after an optional leading date.
    /// </summary>
    private static Draft DraftOf(string text)
    {
        var line = text.Trim();
        var clock = Clock().Match(line);
        var head = clock.Success ? line[..clock.Index] : line;
        var after = clock.Success ? line[clock.Index..] : string.Empty;
        var open = head.IndexOf('(');

        if (open < 0)
        {
            var date = DatePrefix().Match(head);
            var before = date.Success ? head[..date.Length] : string.Empty;

            return new Draft(before, head[before.Length..].Trim(), after, null, EntryStep.Project);
        }

        return head.IndexOf(')', open) >= 0
            ? new Draft(head.TrimEnd(), string.Empty, after, null, EntryStep.Start)
            : new Draft(head[..(open + 1)], head[(open + 1)..].Trim(), after, head[..open].Trim(), EntryStep.Topic);
    }

    /// <summary>
    /// What follows a completed project: an open bracket when the project has topics to offer,
    /// otherwise the gap where the time goes.
    /// </summary>
    private static bool HasTopics(LedgerState state, string name) =>
        state.MatchProject(name).Value is { } project
        && state.Topics.Values.Any(topic => topic.ProjectId == project.Id && !topic.Archived);

    private static string Join(string before, string name, string after) =>
        after.Trim().Length == 0 ? $"{before}{name} " : $"{before}{name} {after.Trim()}";

    /// <summary>
    /// The projects that continue what has been typed. An empty line offers nothing: a list of every
    /// project before a single letter is noise, not a help.
    /// </summary>
    private static IReadOnlyList<string> ProjectNames(LedgerState state, string prefix) =>
        prefix.Length == 0
            ? []
            : Starts(state.Projects.Values.Where(project => !project.Archived).Select(project => project.Name), prefix);

    private static IReadOnlyList<string> TopicNames(LedgerState state, string? project, string prefix) =>
        state.MatchProject(project ?? string.Empty).Value is { } found
            ? Starts(
                state.Topics.Values
                    .Where(topic => topic.ProjectId == found.Id && !topic.Archived)
                    .Select(topic => topic.Name),
                prefix)
            : [];

    /// <summary>The names that continue the word, alphabetically, and never the word itself.</summary>
    private static IReadOnlyList<string> Starts(IEnumerable<string> names, string word) =>
    [
        .. names
            .Where(name => name.StartsWith(word, StringComparison.CurrentCultureIgnoreCase)
                        && !string.Equals(name, word, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .Take(Limit),
    ];

    // The same shape the parser reads as a leading date, "3.11. " or "12.11.2026. ".
    [GeneratedRegex(@"^(?<d>\d{1,2})\.(?<m>\d{1,2})\.(?:(?<y>\d{4})\.?)?\s+", RegexOptions.CultureInvariant)]
    private static partial Regex DatePrefix();

    [GeneratedRegex(@"\d{1,2}:\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex Clock();
}
