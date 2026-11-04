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
            new ReportDocument(
                FormatVersion,
                new ReportCoverage(
                    Day(report.Range.From),
                    Day(report.Range.To),
                    report.Range.Period.ToString().ToLowerInvariant(),
                    report.Range.Label,
                    report.Range.Days),
                generatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                total,
                Shares.Hours(total),
                report.DaysLogged,
                report.Projects.Select(project => Project(project, total)),
                report.Days.Select(DayOf)),
            Options);
    }

    private static ReportProjectLine Project(ProjectLine project, int total) =>
        new(
            project.Project,
            project.Minutes,
            Shares.Hours(project.Minutes),
            Shares.Of(project.Minutes, total),
            project.Topics.Select(topic => Topic(topic, total)));

    private static ReportTopicLine Topic(TopicLine topic, int total) =>
        new(
            topic.Topic,
            topic.Minutes,
            Shares.Hours(topic.Minutes),
            Shares.Of(topic.Minutes, total));

    private static ReportDayLine DayOf(DayLine day) =>
        new(
            Day(day.Date),
            day.Kind,
            day.Minutes,
            Shares.Hours(day.Minutes),
            day.Entries.Select(Entry));

    private static ReportEntryLine Entry(EntryLine entry) =>
        new(
            Clock(entry.Start),
            Clock(entry.End),
            entry.Minutes,
            Shares.Hours(entry.Minutes),
            entry.Project,
            entry.Topic,
            entry.Note);

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Clock(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
