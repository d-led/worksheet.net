// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.Text.Json;

namespace Worksheet.Export;

/// <summary>
/// The report as data, for whoever wants to process it: minutes as integers, shares as fractions,
/// and the hours written the way the application writes them so a reader and a script see the same
/// number. Days are the days that hold something, in order.
/// </summary>
public static class ReportJson
{
    /// <summary>
    /// The version of the shape the data file is written in. It counts up only when a reader built
    /// for the previous version would read the new file wrongly: a field renamed or dropped, a unit
    /// changed, a value moved into another. A field merely added leaves it where it is.
    /// </summary>
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Of(RangeReport report, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(report);

        var total = report.TotalMinutes;

        return JsonSerializer.Serialize(
            new
            {
                version = FormatVersion,
                range = new
                {
                    from = Day(report.Range.From),
                    to = Day(report.Range.To),
                    period = report.Range.Period.ToString().ToLowerInvariant(),
                    label = report.Range.Label,
                    days = report.Range.Days,
                },
                generatedAt = generatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                totalMinutes = total,
                total = Shares.Hours(total),
                daysLogged = report.DaysLogged,
                projects = report.Projects.Select(project => Project(project, total)),
                days = report.Days.Select(DayOf),
            },
            Options);
    }

    private static object Project(ProjectLine project, int total) =>
        new
        {
            project = project.Project,
            minutes = project.Minutes,
            hours = Shares.Hours(project.Minutes),
            share = Shares.Of(project.Minutes, total),
            topics = project.Topics.Select(topic => Topic(topic, total)),
        };

    private static object Topic(TopicLine topic, int total) =>
        new
        {
            topic = topic.Topic,
            minutes = topic.Minutes,
            hours = Shares.Hours(topic.Minutes),
            share = Shares.Of(topic.Minutes, total),
        };

    private static object DayOf(DayLine day) =>
        new
        {
            date = Day(day.Date),
            kind = day.Kind,
            minutes = day.Minutes,
            hours = Shares.Hours(day.Minutes),
            entries = day.Entries.Select(Entry),
        };

    private static object Entry(EntryLine entry) =>
        new
        {
            start = Clock(entry.Start),
            end = Clock(entry.End),
            minutes = entry.Minutes,
            hours = Shares.Hours(entry.Minutes),
            project = entry.Project,
            topic = entry.Topic,
            note = entry.Note,
        };

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Clock(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
