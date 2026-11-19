// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Export;

/// <summary>One project and what it is worth over the range, with its topics broken out.</summary>
public sealed record ProjectLine(string Project, int Minutes, IReadOnlyList<TopicLine> Topics);

/// <summary>One topic of a project; no name is the hours that were logged without one.</summary>
public sealed record TopicLine(string? Topic, int Minutes)
{
    /// <summary>How the hours that were logged without a topic are named, in the data and on the page.</summary>
    public const string WithoutTopic = "without a topic";

    public string Label => Topic ?? WithoutTopic;
}

/// <summary>One time range as the report shows it.</summary>
public sealed record EntryLine(TimeOnly Start, TimeOnly End, int Minutes, string Project, string? Topic, string? Note);

/// <summary>One day of the range, with what it holds; a day off holds nothing.</summary>
/// <remarks>The time is the whole day, a break included; the worked time leaves the breaks out.</remarks>
public sealed record DayLine(DateOnly Date, string Kind, int Minutes, int WorkedMinutes, IReadOnlyList<EntryLine> Entries)
{
    /// <summary>Whether the day holds a range at all; a day of the range that does not is a day off.</summary>
    public bool HoldsSomething => Entries.Count > 0;
}

/// <summary>
/// The range as a materialized object: what the JSON file holds and what the page is drawn from,
/// so both always agree and nothing has to be read twice. The time is the total the journal holds,
/// breaks included; the worked time is what is left of it once the breaks are taken out. Every day
/// of the range is here, the days it holds nothing for included, so a report never skips a date.
/// </summary>
public sealed record RangeReport(
    ExportRange Range,
    int TotalMinutes,
    int WorkedMinutes,
    IReadOnlyList<ProjectLine> Projects,
    IReadOnlyList<DayLine> EveryDay,
    IReadOnlyCollection<string> Catalogue)
{
    /// <summary>The days that hold something, in order: what the data file calls the days.</summary>
    public IReadOnlyList<DayLine> Days => [.. EveryDay.Where(day => day.HoldsSomething)];

    /// <summary>The days of the range with nothing on them, in order, so the page can say them.</summary>
    public IReadOnlyList<DayLine> DaysOff => [.. EveryDay.Where(day => !day.HoldsSomething)];

    /// <summary>Days that hold something, counted for the report's headline figures.</summary>
    public int DaysLogged => Days.Count;

    /// <summary>Whether a break sits in the range, so the page has a second figure to give.</summary>
    public bool HoldsABreak => WorkedMinutes != TotalMinutes;

    /// <summary>Half-open minutes are kept whole: a range of the day is worth exactly what it says.</summary>
    public static RangeReport Of(LedgerState state, ExportRange range)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(range);

        var days = DaysOf(state, range).ToArray();
        var entries = days.SelectMany(day => day.Entries).ToArray();

        return new RangeReport(
            range,
            entries.Sum(entry => entry.Minutes),
            entries.Where(entry => !Break.IsBreak(entry.Project)).Sum(entry => entry.Minutes),
            ProjectsOf(entries),
            EveryDayOf(state, range, days),
            state.Catalogue);
    }

    /// <summary>
    /// Every day of the range in its place: the days that hold something, and a day off wherever the
    /// dates would otherwise jump, since a list of logged days read line by line hides the jump.
    /// </summary>
    private static DayLine[] EveryDayOf(LedgerState state, ExportRange range, DayLine[] logged)
    {
        var byDate = logged.ToDictionary(day => day.Date);

        return [.. range.EachDay.Select(day => byDate.GetValueOrDefault(day) ?? DayOff(state, day))];
    }

    /// <summary>A day of the range with nothing on it, under the kind the journal keeps for it.</summary>
    private static DayLine DayOff(LedgerState state, DateOnly day) =>
        new(day, DayKinds.LabelOf(state.KindOf(day)), 0, 0, []);

    /// <summary>The days that hold something: an empty day is not detail, it is an absence of it.</summary>
    private static IEnumerable<DayLine> DaysOf(LedgerState state, ExportRange range) =>
        state.Entries
            .Values
            .Where(entry => entry.Date >= range.From && entry.Date <= range.To)
            .GroupBy(entry => entry.Date)
            .OrderBy(day => day.Key)
            .Select(day => new DayLine(
                day.Key,
                DayKinds.LabelOf(state.KindOf(day.Key)),
                day.Sum(entry => Minutes(entry.Duration)),
                day.Where(entry => !Break.IsBreak(state.ProjectName(entry.ProjectId))).Sum(entry => Minutes(entry.Duration)),
                [.. day.OrderBy(entry => entry.Start)
                    .ThenBy(entry => entry.Id.Value)
                    .Select(entry => LineOf(state, entry))]));

    private static EntryLine LineOf(LedgerState state, Entry entry) =>
        new(
            entry.Start,
            entry.End,
            Minutes(entry.Duration),
            state.ProjectName(entry.ProjectId),
            state.TopicName(entry.TopicId),
            entry.Note);

    /// <summary>Sorted by project, then by topic, with the un-topicked hours last within a project.</summary>
    private static ProjectLine[] ProjectsOf(IEnumerable<EntryLine> entries) =>
        [.. entries
            .GroupBy(entry => entry.Project)
            .OrderBy(project => project.Key, Dates.Names)
            .Select(project => new ProjectLine(
                project.Key,
                project.Sum(entry => entry.Minutes),
                [.. project
                    .GroupBy(entry => entry.Topic)
                    .OrderBy(topic => topic.Key is null)
                    .ThenBy(topic => topic.Key, Dates.Names)
                    .Select(topic => new TopicLine(topic.Key, topic.Sum(entry => entry.Minutes)))]))];

    private static int Minutes(TimeSpan duration) => (int)Math.Round(duration.TotalMinutes);
}
