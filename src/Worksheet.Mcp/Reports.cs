// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using Worksheet.Core;

namespace Worksheet.Mcp;

/// <summary>
/// What a tool says back, in the shape a person reads: one day, a stretch of days, the catalogue.
/// Kept apart from the tools so the wording can be read on its own, and so every answer to the
/// same question looks the same.
/// </summary>
internal static class Reports
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Day(LedgerState state, DateOnly day)
    {
        var layout = DayLayout.Of(state.EntriesOn(day));
        var header = $"{ShortDate(day)} — {DayKinds.LabelOf(state.KindOf(day))}";

        if (layout.Entries.Count == 0)
        {
            return $"{header}\nNothing recorded.";
        }

        return Line([header, .. layout.Entries.Select(entry => Describe(state, entry)), $"Total {Durations.Format(layout.Total)}"]);
    }

    public static string Totals(LedgerState state, DateOnly from, DateOnly to)
    {
        var entries = state.Entries.Values.Where(entry => entry.Date >= from && entry.Date <= to).ToArray();

        if (entries.Length == 0)
        {
            return $"{Range(from, to)}\nNothing recorded.";
        }

        var projects = entries
            .GroupBy(entry => state.ProjectName(entry.ProjectId))
            .Select(project => (Name: project.Key, Total: project.Aggregate(TimeSpan.Zero, (sum, entry) => sum + entry.Duration)))
            .OrderByDescending(project => project.Total)
            .ThenBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .Select(project => $"{project.Name}  {Durations.Format(project.Total)}");

        var total = entries.Aggregate(TimeSpan.Zero, (sum, entry) => sum + entry.Duration);

        return Line([Range(from, to), .. projects, $"Total  {Durations.Format(total)}"]);
    }

    public static string Projects(LedgerState state)
    {
        var projects = state.Projects.Values
            .Where(project => !project.Archived)
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .Select(project => $"{project.Name}{Held(CountOf(state, project.Id))}")
            .ToArray();

        return projects.Length == 0 ? "No projects yet." : Line(projects);
    }

    public static string Topics(LedgerState state, Project project)
    {
        var topics = state.Topics.Values
            .Where(topic => topic.ProjectId == project.Id && !topic.Archived)
            .OrderBy(topic => topic.Name, StringComparer.OrdinalIgnoreCase)
            .Select(topic => $"{topic.Name}{Held(CountOf(state, topic))}")
            .ToArray();

        return topics.Length == 0 ? $"{project.Name} has no topics." : Line(topics);
    }

    /// <summary>One range as the day shows it: its clocks, what it was on, and how long it lasted.</summary>
    public static string Describe(LedgerState state, Entry entry) =>
        $"{Clock(entry.Start)}-{Clock(entry.End)} {Label(state, entry)} {Durations.Format(entry.Duration)}";

    public static string Label(LedgerState state, Entry entry) =>
        state.TopicName(entry.TopicId) is { } topic
            ? $"{state.ProjectName(entry.ProjectId)} ({topic})"
            : state.ProjectName(entry.ProjectId);

    public static string ShortDate(DateOnly day) => day.ToString("ddd d MMM yyyy", Invariant);

    private static string Line(IEnumerable<string> lines) => string.Join('\n', lines);

    private static string Range(DateOnly from, DateOnly to) =>
        $"{from.ToString("d MMM yyyy", Invariant)} – {to.ToString("d MMM yyyy", Invariant)}";

    private static string Clock(TimeOnly time) => time.ToString("HH:mm", Invariant);

    private static string Held(int entries) =>
        entries switch
        {
            0 => string.Empty,
            1 => " (1 entry)",
            _ => $" ({entries} entries)",
        };

    private static int CountOf(LedgerState state, Guid projectId) =>
        state.Entries.Values.Count(entry => entry.ProjectId == projectId);

    private static int CountOf(LedgerState state, Topic topic) =>
        state.Entries.Values.Count(entry => entry.TopicId == topic.Id);
}
