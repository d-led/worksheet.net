// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using Worksheet.Core;

namespace Worksheet.Export;

/// <summary>
/// The report as one page: totals with their shares, a bar per project and a bar per project and
/// topic, a gantt of the range as it was worked, and a foldable list of the days. Bootstrap from a
/// CDN gives it its typography and its bars, mermaid from a CDN draws the gantt, and the one script
/// of the page's own sorts a table when a heading is pressed.
/// </summary>
public static class ReportHtml
{
    private const string Bootstrap = "https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css";

    private const string Mermaid = "https://cdn.jsdelivr.net/npm/mermaid@12.3.0/dist/mermaid.esm.min.mjs";

    /// <summary>What a card says when it has nothing to show; each is a sentence of its own.</summary>
    private const string NothingLogged = """<p class="text-body-secondary mb-0">Nothing was logged in this range.</p>""";

    private const string NoProjectsYet = """<p class="text-body-secondary mb-0">No project was used yet in this range.</p>""";

    private const string NothingToDraw = """<p class="text-body-secondary mb-0">Nothing to draw.</p>""";

    /// <summary>What a bar is filled with when the row is a break: the warm peach, since a pause is not a project.</summary>
    private const string BreakTint = "#f3c89a";

    /// <summary>The page, block by block in the order it is read; each block ends with its line end.</summary>
    public static string Of(RangeReport report, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(report);

        return string.Concat(Blocks(report, generatedAt));
    }

    private static IEnumerable<string> Blocks(RangeReport report, DateTimeOffset generatedAt) =>
    [
        "<!doctype html>\n<html lang=\"en\">\n",
        Head(report),
        "<body>\n<main class=\"container py-4\">\n",
        Header(report),
        Summary(report, generatedAt),
        ProjectCard(report),
        TopicCard(report),
        TimelineCard(report),
        Detail(report),
        Footer,
        "</main>\n",
        Scripts(),
        "</body>\n</html>\n",
    ];

    private static string Head(RangeReport report) =>
        $"""
         <head>
           <meta charset="utf-8">
           <meta name="viewport" content="width=device-width, initial-scale=1">
           <title>Worksheet — {Esc(report.Range.Label)}</title>
           <link href="{Bootstrap}" rel="stylesheet">
         </head>

         """;

    private static string Header(RangeReport report) =>
        $"""
         <header class="mb-3">
           <p class="text-body-secondary text-uppercase small mb-1">Worksheet · {Esc(Word(report.Range.Period))}</p>
           <h1 class="h3 mb-0">{Esc(report.Range.Label)}</h1>
         </header>

         """;

    /// <summary>
    /// Four figures on one line, the way Bootstrap's own typography puts them: a muted label and a
    /// value, no boxes. The dates and the time of generation are small print below.
    /// </summary>
    private static string Summary(RangeReport report, DateTimeOffset generatedAt) =>
        $"""
         <section class="mb-4">
           <p class="mb-1">
             <span class="text-body-secondary">Total</span> <span class="fs-5 fw-semibold">{Esc(Shares.Hours(report.TotalMinutes))}</span>
             <span class="text-body-secondary ms-4">Days logged</span> <span class="fs-5 fw-semibold">{report.DaysLogged}</span>
             <span class="text-body-secondary ms-4">Projects</span> <span class="fs-5 fw-semibold">{report.Projects.Count}</span>
             <span class="text-body-secondary ms-4">Busiest day</span> <span class="fs-5 fw-semibold">{Esc(Busiest(report))}</span>
           </p>
           <p class="text-body-secondary small mb-0">
             {Esc(DayText(report.Range.From))} – {Esc(DayText(report.Range.To))} · generated {Esc(Moment(generatedAt))}
           </p>
         </section>

         """;

    /// <summary>
    /// One bar per project on the row that names it: a share is a length, and a bar of that length
    /// under the number says more in less width than a circle cut into slices.
    /// </summary>
    private static string ProjectCard(RangeReport report) =>
        $"""
         <section class="card mb-4">
           <div class="card-body">
             <h2 class="h5 mb-3">Where the time went</h2>
             {(report.Projects.Count == 0 ? NothingLogged : ProjectBars(report))}
           </div>
         </section>

         """;

    private static string ProjectBars(RangeReport report) =>
        Table(
            ["Project"],
            Rows(report.Projects.Select(project => BarRow(report, [project.Project], project.Minutes, project.Project))),
            $"""<tr class="table-light fw-semibold"><td>Total</td><td></td><td class="text-end">{Esc(Shares.Hours(report.TotalMinutes))}</td><td class="text-end">100.0 %</td></tr>""");

    private static string TopicCard(RangeReport report) =>
        $"""
         <section class="card mb-4">
           <div class="card-body">
             <h2 class="h5 mb-3">By project and topic</h2>
             {(report.Projects.Count == 0 ? NoProjectsYet : Table(["Project", "Topic"], TopicRows(report)))}
           </div>
         </section>

         """;

    /// <summary>A project is named once, on the first of its rows, so the bars start close to the names.</summary>
    private static string TopicRows(RangeReport report) =>
        Rows(report.Projects.SelectMany(project => project.Topics.Select((topic, index) => BarRow(
            report,
            [index == 0 ? project.Project : string.Empty, topic.Label],
            topic.Minutes,
            project.Project))));

    /// <summary>
    /// A table of bars: the cells that name a row, the bar, the time it stands for, its share. Every
    /// heading but the bar sorts, and the footer stays where it is while the rows move.
    /// </summary>
    private static string Table(string[] headings, string rows, string footer = "") =>
        $"""
         <table class="table table-sm align-middle mb-0" data-sortable>
           <thead><tr>{string.Concat(headings.Select(heading => Column(heading, "text", string.Empty)))}<th scope="col" style="width:45%"></th>{Column("Time", "number", "text-end")}{Column("Share", "number", "text-end")}</tr></thead>
           <tbody>
         {rows}  </tbody>
         {(footer.Length == 0 ? string.Empty : $"<tfoot>\n{footer}\n</tfoot>\n")}</table>

         """;

    /// <summary>A heading cell, carrying the classes of a number's column when it has any.</summary>
    private static string Column(string heading, string kind, string classes) =>
        classes.Length == 0
            ? $"""<th scope="col">{Sorting(heading, kind, classes)}</th>"""
            : $"""<th scope="col" class="{classes}">{Sorting(heading, kind, classes)}</th>""";

    /// <summary>A heading that sorts its column when pressed, and says which way it is sorted now.</summary>
    private static string Sorting(string heading, string kind, string classes) =>
        $"""<button type="button" class="btn btn-link p-0 fw-semibold text-reset {classes}" data-sort="{kind}">{Esc(heading)}</button>""";

    /// <summary>Rows as a page writes them: one per line, each with its own line end.</summary>
    private static string Rows(IEnumerable<string> rows) => string.Concat(rows.Select(row => $"{row}\n"));

    /// <summary>
    /// A row in the project's own pastel: the wash behind it, and a bar in the tint itself — or, for
    /// a break, the warm peach, which has no project to be tinted by.
    /// </summary>
    private static string BarRow(RangeReport report, string[] cells, int minutes, string project) =>
        $"""
         <tr style="--bs-table-bg:{Tints.Wash(report.Catalogue, project)}">{string.Concat(cells.Select(cell => $"<td>{Esc(cell)}</td>"))}<td><div class="progress" role="progressbar" aria-valuenow="{Esc(Shares.Percent(minutes, report.TotalMinutes))}" aria-valuemin="0" aria-valuemax="100" style="height:10px"><div class="progress-bar" style="width:{Shares.Width(minutes, report.TotalMinutes)};background:{Tints.Of(report.Catalogue, project) ?? BreakTint}"></div></div></td><td class="text-end text-nowrap" data-value="{minutes}">{Esc(Shares.Hours(minutes))}</td><td class="text-end text-nowrap" data-value="{minutes}">{Esc(Shares.Percent(minutes, report.TotalMinutes))}</td></tr>
         """;

    private static string TimelineCard(RangeReport report) =>
        $"""
         <section class="card mb-4">
           <div class="card-body">
             <h2 class="h5 mb-3">The range as it was worked</h2>
             {(report.Days.Count == 0 || report.Projects.Count == 0 ? NothingToDraw : Gantt(report))}
           </div>
         </section>

         """;

    /// <summary>
    /// The range as a gantt, one swimlane per project: mermaid draws the bars, the axis and the
    /// section bands, which is more picture than a page carried by hand can justify — and its
    /// sections are the thing the hand-drawn lanes could not do. Up to two months every range keeps
    /// its own bar; beyond that a bar per project per day, because a year of single afternoons is a
    /// thicket rather than a picture. The frontmatter asks mermaid for compact mode, so a project's
    /// bars share a row instead of each taking a lane of its own.
    /// </summary>
    private static string Gantt(RangeReport report)
    {
        var perEntry = report.Range.Days <= 62;

        // The bars are numbered before they are grouped, so a section's tasks keep one running id.
        var numbered = Bars(report, perEntry)
            .Select((bar, index) => (bar.Project, Line: $"        {Label(bar.Label)} :e{index + 1}, {bar.Start}, {bar.Minutes}m"));

        var sections = numbered
            .GroupBy(bar => bar.Project)
            .SelectMany(project => new[] { $"    section {Label(project.Key)}" }.Concat(project.Select(bar => bar.Line)));

        return MermaidBlock(
            Rows(
            [
                "---",
                "displayMode: compact",
                "---",
                "gantt",
                $"    title {Label(report.Range.Label)}",
                $"    dateFormat {(perEntry ? "YYYY-MM-DD HH:mm" : "YYYY-MM-DD")}",
                $"    axisFormat {(report.Range.Days == 1 ? "%H:%M" : "%d %b")}",
                "    todayMarker off",
                .. sections,
            ]));
    }

    /// <summary>Every bar of the chart in section order, each still knowing its project.</summary>
    private static IEnumerable<(string Project, string Label, string Start, int Minutes)> Bars(RangeReport report, bool perEntry) =>
        report.Projects.SelectMany(project => BarsOf(report, project.Project, perEntry)
            .Select(bar => (Project: project.Project, bar.Label, bar.Start, bar.Minutes)));

    private static IEnumerable<(string Label, string Start, int Minutes)> BarsOf(RangeReport report, string project, bool perEntry) =>
        perEntry ? EntryBars(report, project) : DayBars(report, project);

    /// <summary>One bar per range, named for its topic and its length: the picture the day itself makes.</summary>
    private static IEnumerable<(string Label, string Start, int Minutes)> EntryBars(RangeReport report, string project) =>
        report.Days
            .SelectMany(day => day.Entries.Select(entry => (Day: day.Date, Entry: entry)))
            .Where(pair => pair.Entry.Project == project)
            .Select(pair => (
                $"{pair.Entry.Topic ?? pair.Entry.Project} {Shares.Hours(pair.Entry.Minutes)}",
                $"{IsoDay(pair.Day)} {ClockText.Format(pair.Entry.Start)}",
                pair.Entry.Minutes));

    /// <summary>One bar per day the project was worked on, for a range too long to show every range in.</summary>
    private static IEnumerable<(string Label, string Start, int Minutes)> DayBars(RangeReport report, string project) =>
        report.Days
            .Select(day => (day.Date, Minutes: day.Entries.Where(entry => entry.Project == project).Sum(entry => entry.Minutes)))
            .Where(day => day.Minutes > 0)
            .Select(day => (
                $"{Reports(day.Date)} ({Shares.Hours(day.Minutes)})",
                IsoDay(day.Date),
                Math.Min(day.Minutes, 24 * 60)));

    private static string MermaidBlock(string diagram) =>
        $"<div class=\"overflow-auto\"><pre class=\"mermaid\">\n{diagram.Replace("<", "&lt;").Replace(">", "&gt;")}</pre></div>\n";

    /// <summary>A label mermaid reads: no colon, which starts a task's data, no quote, and one line.</summary>
    private static string Label(string text) =>
        text.Replace("\r", " ").Replace("\n", " ").Replace(":", "·").Replace("\"", "'").Trim();

    /// <summary>A day as mermaid parses it, in the format the chart declares.</summary>
    private static string IsoDay(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Detail(RangeReport report) =>
        report.Days.Count == 0
            ? string.Empty
            : report.Range.SpansMoreThanADay
                ? Days(report)
                : Entries(report.Catalogue, report.Days[0]);

    /// <summary>The days, folded: each date and its total on the summary, the ranges inside it.</summary>
    private static string Days(RangeReport report) =>
        $"""
         <section class="mb-4">
           <h2 class="h5">Days</h2>
         {string.Concat(report.Days.Select(day => Fold(report.Catalogue, day)))}</section>

         """;

    private static string Fold(IReadOnlyCollection<string> catalogue, DayLine day) =>
        $"""
         <details class="card mb-1"><summary class="card-body py-2">
           <span class="fw-semibold">{Esc(day.Date.ToString("dddd MMM yyyy", CultureInfo.InvariantCulture))}</span>
           <span class="text-body-secondary">· {Esc(day.Kind)} · {Esc(Shares.Hours(day.Minutes))}</span>
         </summary>
         <div class="card-body py-2">
         {Entries(catalogue, day)}</div></details>

         """;

    private static string Entries(IReadOnlyCollection<string> catalogue, DayLine day) =>
        $"""
         <table class="table table-sm align-middle mb-0">
           <thead>
             <tr><th scope="col">From–to</th><th scope="col">Project</th><th scope="col">Topic</th><th scope="col" class="text-end">Time</th><th scope="col">Note</th></tr>
           </thead>
           <tbody>
         {Rows(day.Entries.Select(entry => EntryRow(catalogue, entry)))}</tbody>
         </table>

         """;

    private static string EntryRow(IReadOnlyCollection<string> catalogue, EntryLine entry) =>
        $"""
         <tr style="--bs-table-bg:{Tints.Wash(catalogue, entry.Project)}"><td class="text-nowrap">{ClockText.Format(entry.Start)}–{ClockText.Format(entry.End)}</td><td>{Esc(entry.Project)}</td><td>{Esc(entry.Topic ?? string.Empty)}</td><td class="text-end">{Esc(Shares.Hours(entry.Minutes))}</td><td class="text-body-secondary">{Esc(entry.Note ?? string.Empty)}</td></tr>
         """;

    /// <summary>The page's last word: where the numbers come from, and what the page needs from the net.</summary>
    private const string Footer = """
        <footer class="text-body-secondary small mt-5">
          <p class="mb-1">Made by Worksheet from its journal. Days that hold nothing are left out; the total is what the journal says, not a guess at a working day.</p>
          <p class="mb-0">This page loads Bootstrap and mermaid from jsDelivr. Without a connection it still reads, unstyled; the gantt is then not drawn.</p>
        </footer>

        """;

    /// <summary>
    /// The two scripts: mermaid, which draws the gantt, and the page's own, which sorts a table by the
    /// number in the cell when the column is time or share and by the name otherwise. The sorter is
    /// inline, so the tables work from a file with no connection; the gantt does not.
    /// </summary>
    private static string Scripts() =>
        $$"""
          <script type="module">
            import mermaid from '{{Mermaid}}';
            mermaid.initialize({
              startOnLoad: true,
              securityLevel: 'strict',
              gantt: { barHeight: 16, barGap: 6, topPadding: 40, gridLineStartPadding: 20, fontSize: 11, sectionFontSize: 13 }
            });
          </script>
          {{Sorter}}

          """;

    /// <summary>Pressing a heading sorts its column, by the number in the cell when it is time or share.</summary>
    private const string Sorter = """
        <script>
          for (const table of document.querySelectorAll('table[data-sortable]')) {
            const body = table.tBodies[0];

            for (const button of table.tHead.querySelectorAll('th button')) {
              button.addEventListener('click', () => {
                const column = button.parentElement.cellIndex;
                const numeric = button.dataset.sort === 'number';
                const direction = button.dataset.direction === 'ascending' ? 'descending' : 'ascending';
                const value = row => {
                  const cell = row.cells[column];

                  return numeric ? Number(cell.dataset.value ?? 0) : cell.textContent.trim().toLowerCase();
                };

                for (const other of table.tHead.querySelectorAll('th button')) {
                  delete other.dataset.direction;
                }

                button.dataset.direction = direction;

                [...body.rows]
                  .sort((one, other) => {
                    const order = numeric
                      ? value(one) - value(other)
                      : value(one).localeCompare(value(other));

                    return direction === 'ascending' ? order : -order;
                  })
                  .forEach(row => body.append(row));

                for (const cell of table.tHead.querySelectorAll('th')) {
                  cell.removeAttribute('aria-sort');
                }

                button.parentElement.setAttribute('aria-sort', direction);
              });
            }
          }
        </script>

        """;

    private static string Busiest(RangeReport report) =>
        report.Days.MaxBy(day => day.Minutes) is { } busiest
            ? $"{busiest.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture)} · {Shares.Hours(busiest.Minutes)}"
            : "—";

    private static string Reports(DateOnly day) => day.ToString("d MMM", CultureInfo.InvariantCulture);

    /// <summary>How a day reads in a sentence.</summary>
    private static string DayText(DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Moment(DateTimeOffset moment) =>
        moment.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string Word(ExportPeriod period) =>
        period switch
        {
            ExportPeriod.Day => "day",
            ExportPeriod.Month => "month",
            ExportPeriod.Quarter => "quarter",
            _ => "year",
        };

    private static string Esc(string? text) =>
        (text ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
