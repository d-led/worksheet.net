// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json.Serialization;

namespace Worksheet.Export;

/// <summary>
/// The report file's shape, one record per thing it holds. The names are the file's contract with
/// whoever reads it, so they are spelled out here rather than left to a serializer's naming policy,
/// and the order of the members is the order they appear in the file.
/// </summary>
internal sealed record ReportDocument(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("range")] ReportCoverage Range,
    [property: JsonPropertyName("generatedAt")] string GeneratedAt,
    [property: JsonPropertyName("totalMinutes")] int TotalMinutes,
    [property: JsonPropertyName("total")] string Total,
    [property: JsonPropertyName("workedMinutes")] int WorkedMinutes,
    [property: JsonPropertyName("worked")] string Worked,
    [property: JsonPropertyName("daysLogged")] int DaysLogged,
    [property: JsonPropertyName("projects")] IEnumerable<ReportProjectLine> Projects,
    [property: JsonPropertyName("days")] IEnumerable<ReportDayLine> Days);

internal sealed record ReportCoverage(
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("period")] string Period,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("days")] int Days);

internal sealed record ReportProjectLine(
    [property: JsonPropertyName("project")] string Project,
    [property: JsonPropertyName("minutes")] int Minutes,
    [property: JsonPropertyName("hours")] string Hours,
    [property: JsonPropertyName("share")] double Share,
    [property: JsonPropertyName("topics")] IEnumerable<ReportTopicLine> Topics);

internal sealed record ReportTopicLine(
    [property: JsonPropertyName("topic")] string? Topic,
    [property: JsonPropertyName("minutes")] int Minutes,
    [property: JsonPropertyName("hours")] string Hours,
    [property: JsonPropertyName("share")] double Share);

internal sealed record ReportDayLine(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("minutes")] int Minutes,
    [property: JsonPropertyName("hours")] string Hours,
    [property: JsonPropertyName("entries")] IEnumerable<ReportEntryLine> Entries);

internal sealed record ReportEntryLine(
    [property: JsonPropertyName("start")] string Start,
    [property: JsonPropertyName("end")] string End,
    [property: JsonPropertyName("minutes")] int Minutes,
    [property: JsonPropertyName("hours")] string Hours,
    [property: JsonPropertyName("project")] string Project,
    [property: JsonPropertyName("topic")] string? Topic,
    [property: JsonPropertyName("note")] string? Note);
