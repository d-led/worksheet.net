// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.Text;

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

    public static string Of(RangeReport report, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(report);

        var page = new StringBuilder();

        page.AppendLine("<!doctype html>");
        page.AppendLine("<html lang=\"en\">");
        page.Append(Head(report));
        page.AppendLine("<body>");
        page.AppendLine("<main class=\"container py-4\">");
        page.Append(Header(report));
        page.Append(Summary(report, generatedAt));
        page.Append(ProjectCard(report));
        page.Append(TopicCard(report));
        page.Append(TimelineCard(report));
        page.Append(Detail(report));
        page.Append(Footer(report));
        page.AppendLine("</main>");
        page.Append(Scripts());
        page.AppendLine("</body>");
        page.AppendLine("</html>");

        return page.ToString();
    }

    private static string Head(RangeReport report) =>
        $$"""
          <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Worksheet — {{Esc(report.Range.Label)}}</title>
            <link href="{{Bootstrap}}" rel="stylesheet">
          </head>

          """;

    private static string Header(RangeReport report) =>
        $$"""
          <header class="mb-3">
            <p class="text-body-secondary text-uppercase small mb-1">Worksheet · {{Esc(Word(report.Range.Period))}}</p>
            <h1 class="h3 mb-0">{{Esc(report.Range.Label)}}</h1>
          </header>

          """;

    /// <summary>
    /// Four figures on one line, the way Bootstrap's own typography puts them: a muted label and a
    /// value, no boxes. The dates and the time of generation are small print below.
    /// </summary>
    private static string Summary(RangeReport report, DateTimeOffset generatedAt) =>
        $$"""
          <section class="mb-4">
            <p class="mb-1">
              <span class="text-body-secondary">Total</span> <span class="fs-5 fw-semibold">{{Esc(Shares.Hours(report.TotalMinutes))}}</span>
              <span class="text-body-secondary ms-4">Days logged</span> <span class="fs-5 fw-semibold">{{report.DaysLogged}}</span>
              <span class="text-body-secondary ms-4">Projects</span> <span class="fs-5 fw-semibold">{{report.Projects.Count}}</span>
              <span class="text-body-secondary ms-4">Busiest day</span> <span class="fs-5 fw-semibold">{{Esc(Busiest(report))}}</span>
            </p>
            <p class="text-body-secondary small mb-0">
              {{Esc(DayText(report.Range.From))}} – {{Esc(DayText(report.Range.To))}} · generated {{Esc(Moment(generatedAt))}}
            </p>
          </section>

          """;

    /// <summary>
    /// One bar per project on the row that names it: a share is a length, and a bar of that length
    /// under the number says more in less width than a circle cut into slices.
    /// </summary>
    private static string ProjectCard(RangeReport report) =>
        $$"""
          <section class="card mb-4">
            <div class="card-body">
              <h2 class="h5 mb-3">Where the time went</h2>
              {{(report.Projects.Count == 0
                  ? "<p class=\"text-body-secondary mb-0\">Nothing was logged in this range.</p>\n"
                  : ProjectBars(report))}}
            </div>
          </section>

          """;

    private static string ProjectBars(RangeReport report)
    {
        var rows = new StringBuilder();

        foreach (var project in report.Projects)
        {
            rows.AppendLine(BarRow([project.Project], project.Minutes, report.TotalMinutes));
        }

        var total =
            $"    <tr class=\"table-light fw-semibold\"><td>Total</td><td></td>"
            + $"<td class=\"text-end\">{Esc(Shares.Hours(report.TotalMinutes))}</td>"
            + "<td class=\"text-end\">100.0 %</td></tr>\n";

        return Table(["Project"], rows.ToString(), total);
    }

    private static string TopicCard(RangeReport report)
    {
        var rows = TopicRows(report);

        return $$"""
          <section class="card mb-4">
            <div class="card-body">
              <h2 class="h5 mb-3">By project and topic</h2>
              {{(rows.Length == 0
                  ? "<p class=\"text-body-secondary mb-0\">No project was used yet in this range.</p>\n"
                  : Table(["Project", "Topic"], rows))}}
            </div>
          </section>

          """;
    }

    /// <summary>A project is named once, on the first of its rows, so the bars start close to the names.</summary>
    private static string TopicRows(RangeReport report)
    {
        var rows = new StringBuilder();
        string? named = null;

        foreach (var project in report.Projects)
        {
            foreach (var topic in project.Topics)
            {
                var cell = project.Project == named ? string.Empty : project.Project;
                named = project.Project;

                rows.AppendLine(BarRow([cell, topic.Label], topic.Minutes, report.TotalMinutes));
            }
        }

        return rows.ToString();
    }

    /// <summary>
    /// A table of bars: the cells that name a row, the bar, the time it stands for, its share. Every
    /// heading but the bar sorts, and the footer stays where it is while the rows move.
    /// </summary>
    private static string Table(string[] headings, string rows, string footer = "") =>
        "<table class=\"table table-sm align-middle mb-0\" data-sortable>\n"
        + "  <thead><tr>"
        + string.Concat(headings.Select(heading => $"<th scope=\"col\">{Sorting(heading, "text", string.Empty)}</th>"))
        + "<th scope=\"col\" style=\"width:45%\"></th>"
        + $"<th scope=\"col\" class=\"text-end\">{Sorting("Time", "number", "text-end")}</th>"
        + $"<th scope=\"col\" class=\"text-end\">{Sorting("Share", "number", "text-end")}</th>"
        + "</tr></thead>\n"
        + $"  <tbody>\n{rows}  </tbody>\n"
        + (footer.Length == 0 ? string.Empty : $"  <tfoot>\n{footer}  </tfoot>\n")
        + "</table>\n";

    /// <summary>A heading that sorts its column when pressed, and says which way it is sorted now.</summary>
    private static string Sorting(string heading, string kind, string classes) =>
        $"<button type=\"button\" class=\"btn btn-link p-0 fw-semibold text-reset {classes}\""
        + $" data-sort=\"{kind}\">{Esc(heading)}</button>";

    private static string BarRow(string[] cells, int minutes, int total) =>
        "    <tr>"
        + string.Concat(cells.Select(cell => $"<td>{Esc(cell)}</td>"))
        + $"<td><div class=\"progress\" role=\"progressbar\" aria-valuenow=\"{Esc(Shares.Percent(minutes, total))}\""
        + " aria-valuemin=\"0\" aria-valuemax=\"100\" style=\"height:10px\">"
        + $"<div class=\"progress-bar\" style=\"width:{Shares.Width(minutes, total)}\"></div></div></td>"
        + $"<td class=\"text-end text-nowrap\" data-value=\"{minutes}\">{Esc(Shares.Hours(minutes))}</td>"
        + $"<td class=\"text-end text-nowrap\" data-value=\"{minutes}\">{Esc(Shares.Percent(minutes, total))}</td>"
        + "</tr>";

    private static string TimelineCard(RangeReport report) =>
        $$"""
          <section class="card mb-4">
            <div class="card-body">
              <h2 class="h5 mb-3">The range as it was worked</h2>
              {{(report.Days.Count == 0 || report.Projects.Count == 0
                  ? "<p class=\"text-body-secondary mb-0\">Nothing to draw.</p>\n"
                  : Gantt(report))}}
            </div>
          </section>

          """;

    /// <summary>
    /// The range as a gantt, one swimlane per project: mermaid draws the bars, the axis and the
    /// section bands, which is more picture than a page carried by hand can justify — and its
    /// sections are the thing the hand-drawn lanes could not do. Up to two months every range keeps
    /// its own bar; beyond that a bar per project per day, because a year of single afternoons is a
    /// thicket rather than a picture.
    /// </summary>
    private static string Gantt(RangeReport report)
    {
        var perEntry = report.Range.Days <= 62;
        var lines = new StringBuilder();
        var id = 0;

        lines.AppendLine("gantt");
        lines.AppendLine(CultureInfo.InvariantCulture, $"    title {Label(report.Range.Label)}");
        lines.AppendLine(CultureInfo.InvariantCulture, $"    dateFormat {(perEntry ? "YYYY-MM-DD HH:mm" : "YYYY-MM-DD")}");
        lines.AppendLine(CultureInfo.InvariantCulture, $"    axisFormat {(report.Range.Days == 1 ? "%H:%M" : "%d %b")}");
        lines.AppendLine("    todayMarker off");

        foreach (var project in report.Projects)
        {
            lines.AppendLine(CultureInfo.InvariantCulture, $"    section {Label(project.Project)}");

            foreach (var bar in Bars(report, project.Project, perEntry))
            {
                lines.AppendLine(CultureInfo.InvariantCulture, $"        {Label(bar.Label)} :e{++id}, {bar.Start}, {bar.Minutes}m");
            }
        }

        return MermaidBlock(lines.ToString());
    }

    private static IEnumerable<(string Label, string Start, int Minutes)> Bars(RangeReport report, string project, bool perEntry) =>
        perEntry ? EntryBars(report, project) : DayBars(report, project);

    /// <summary>One bar per range, named for its topic and its length: the picture the day itself makes.</summary>
    private static IEnumerable<(string Label, string Start, int Minutes)> EntryBars(RangeReport report, string project) =>
        report.Days
            .SelectMany(day => day.Entries.Select(entry => (Day: day.Date, Entry: entry)))
            .Where(pair => pair.Entry.Project == project)
            .Select(pair => (
                $"{pair.Entry.Topic ?? pair.Entry.Project} {Shares.Hours(pair.Entry.Minutes)}",
                $"{IsoDay(pair.Day)} {Clock(pair.Entry.Start)}",
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

    private static string Clock(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Detail(RangeReport report) =>
        report.Days.Count == 0
            ? string.Empty
            : report.Range.SpansMoreThanADay
                ? Days(report)
                : Entries(report.Days[0]);

    private static string Days(RangeReport report)
    {
        var page = new StringBuilder();

        page.AppendLine("  <section class=\"mb-4\">");
        page.AppendLine("    <h2 class=\"h5\">Days</h2>");

        foreach (var day in report.Days)
        {
            page.AppendLine($"    <details class=\"card mb-1\"><summary class=\"card-body py-2\">");
            page.AppendLine(
                CultureInfo.InvariantCulture,
                $"      <span class=\"fw-semibold\">{Esc(day.Date.ToString("dddd MMM yyyy", CultureInfo.InvariantCulture))}</span>");
            page.AppendLine(
                CultureInfo.InvariantCulture,
                $"      <span class=\"text-body-secondary\">· {Esc(day.Kind)} · {Esc(Shares.Hours(day.Minutes))}</span>");
            page.AppendLine("    </summary>");
            page.AppendLine("    <div class=\"card-body py-2\">");
            page.Append(Entries(day));
            page.AppendLine("    </div></details>");
        }

        page.AppendLine("  </section>");

        return page.ToString();
    }

    private static string Entries(DayLine day)
    {
        var rows = new StringBuilder();

        foreach (var entry in day.Entries)
        {
            rows.AppendLine(
                CultureInfo.InvariantCulture,
                $"      <tr><td class=\"text-nowrap\">{Clock(entry.Start)}–{Clock(entry.End)}</td>"
                + $"<td>{Esc(entry.Project)}</td><td>{Esc(entry.Topic ?? string.Empty)}</td>"
                + $"<td class=\"text-end\">{Esc(Shares.Hours(entry.Minutes))}</td>"
                + $"<td class=\"text-body-secondary\">{Esc(entry.Note ?? string.Empty)}</td></tr>");
        }

        return $$"""
                   <table class="table table-sm align-middle mb-0">
                     <thead>
                       <tr><th scope="col">From–to</th><th scope="col">Project</th><th scope="col">Topic</th><th scope="col" class="text-end">Time</th><th scope="col">Note</th></tr>
                     </thead>
                     <tbody>
                 {{rows.ToString()}}    </tbody>
                   </table>

                 """;
    }

    private static string Footer(RangeReport report) =>
        $$"""
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
          {{Sorter()}}

          """;

    /// <summary>Pressing a heading sorts its column, by the number in the cell when it is time or share.</summary>
    private static string Sorter() =>
        $$"""
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
        report.Days.Count == 0
            ? "—"
            : report.Days
                .OrderByDescending(day => day.Minutes)
                .Select(day => $"{day.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture)} · {Shares.Hours(day.Minutes)}")
                .First();

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
