// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Worksheet.Core;

namespace Worksheet.Export;

/// <summary>
/// The report as data, for whoever wants to process it: minutes as integers, shares as fractions,
/// and the hours written the way the application writes them so a reader and a script see the same
/// number. The total is everything the range holds, breaks included; the worked time is the same
/// range without its breaks. Days are the days that hold something, in order, each under the name
/// the journal stores for its kind; the days off are the days that rest with nothing on them — a
/// day off, a flex day, a sick day or a holiday — so no kind of day of the range is left out.
/// </summary>
public static partial class ReportJson
{
    /// <summary>
    /// The version of the shape the data file is written in. It counts up only when a reader built
    /// for the previous version would read the new file wrongly: a field renamed or dropped, a unit
    /// changed, a value moved into another. A field merely added leaves it where it is. Version 2
    /// gave <c>kind</c> the journal's name for a kind of day where version 1 wrote a label.
    /// </summary>
    public const int FormatVersion = 2;

    /// <summary>
    /// The one place the file's shape is read from: generated at compile time, so the single-file
    /// build serialises without reflection and a rename in the records cannot drift from the reader.
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ReportDocument))]
    private sealed partial class Context : JsonSerializerContext;

    public static string Of(RangeReport report, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(report);

        var total = report.TotalMinutes;

        var document = new ReportDocument(
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
            report.WorkedMinutes,
            Shares.Hours(report.WorkedMinutes),
            report.DaysLogged,
            report.Projects.Select(project => Project(project, total)),
            report.Days.Select(DayOf),
            report.DaysOff.Select(DayOff));

        return JsonSerializer.Serialize(document, Context.Default.ReportDocument);
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
            Kind(day.Kind),
            day.Minutes,
            Shares.Hours(day.Minutes),
            day.WorkedMinutes,
            Shares.Hours(day.WorkedMinutes),
            day.Entries.Select(Entry));

    private static ReportDayOff DayOff(DayLine day) => new(Day(day.Date), Kind(day.Kind));

    /// <summary>
    /// The file's word for a kind of day: the name the journal stores, which is the name a script
    /// can match on. A label is a surface's business and may be reworded — a weekend was renamed a
    /// day off — while a stored name may not, since the journals written before it would then be
    /// unreadable; a reader of this file is in the same position as a journal.
    /// </summary>
    private static string Kind(DayKind kind) => kind.ToString();

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

    private static string Clock(TimeOnly time) => ClockText.Format(time);
}
