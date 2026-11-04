// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.Text;

namespace Worksheet.Export;

/// <summary>
/// The report as one page: totals with their shares, a bar per project and a bar per project and
/// topic, a lane per project and topic over the days of the range, and a foldable list of the days.
/// Bootstrap from a CDN gives it its typography and its bars; the one script is the page's own and
/// sorts a table when a heading is pressed.
/// </summary>
public static class ReportHtml
{
    private const string Bootstrap = "https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css";

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
                  : Timeline(report))}}
            </div>
          </section>

          """;

    /// <summary>
    /// A lane per project and topic, one column per day worked, and a mark in the column for every
    /// range: the picture is read down a lane instead of down one line per range, which is what a
    /// gantt of the same data would spend its height on.
    /// </summary>
    private static string Timeline(RangeReport report)
    {
        var days = report.Days.Select(day => day.Date).ToArray();
        var lanes = Lanes(report);
        var (from, span) = Window(report);
        var table = new StringBuilder();

        table.AppendLine("<table class=\"table table-sm align-middle mb-0\">");
        table.AppendLine(
            "  <thead><tr><th scope=\"col\">Project and topic</th><th scope=\"col\" class=\"text-end\">Time</th>"
            + $"<th scope=\"col\" style=\"width:55%\">{DayHeadings(days, from, span)}</th></tr></thead>");
        table.AppendLine("  <tbody>");

        foreach (var lane in lanes)
        {
            table.AppendLine(LaneRow(lane, report, days, from, span));
        }

        table.Append("  </tbody>\n</table>\n");

        return table.ToString();
    }

    /// <summary>
    /// The hours the range was actually worked in — from the earliest start to the latest end. A day
    /// column is scaled to that window rather than to midnight, which is what keeps an hour from
    /// being a hair on a wide column.
    /// </summary>
    private static (double From, double Span) Window(RangeReport report)
    {
        var entries = report.Days.SelectMany(day => day.Entries).ToArray();

        if (entries.Length == 0)
        {
            return (0, 24 * 60);
        }

        var from = entries.Min(entry => entry.Start.ToTimeSpan().TotalMinutes);
        var to = entries.Max(entry => entry.Start.ToTimeSpan().TotalMinutes + entry.Minutes);

        return (from, Math.Max(to - from, 60));
    }

    /// <summary>One lane: a project, one of its topics, and the hours that topic holds in the range.</summary>
    private sealed record Lane(string Project, string Topic, int Minutes, string Colour)
    {
        public bool Holds(EntryLine entry) =>
            entry.Project == Project && (entry.Topic ?? TopicLine.WithoutTopic) == Topic;
    }

    /// <summary>The lanes in the order the tables name them, each with a colour that tells it apart.</summary>
    private static Lane[] Lanes(RangeReport report)
    {
        string[] colours = ["primary", "success", "info", "warning", "secondary"];
        var lanes = new List<Lane>();

        for (var index = 0; index < report.Projects.Count; index++)
        {
            var project = report.Projects[index];
            var colour = colours[index % colours.Length];

            foreach (var topic in project.Topics)
            {
                lanes.Add(new Lane(project.Project, topic.Label, topic.Minutes, colour));
            }
        }

        return [.. lanes];
    }

    private static string DayHeadings(DateOnly[] days, double from, double span) =>
        "<div class=\"d-flex gap-1 small text-body-secondary\">"
        + $"<span class=\"flex-fill text-end pe-1\">{Esc(Clock(from))}</span>"
        + string.Concat(days.Select(day => $"<span class=\"flex-fill text-center\">{Esc(Reports(day))}</span>"))
        + $"<span class=\"flex-fill ps-1\">{Esc(Clock(from + span))}</span>"
        + "</div>";

    private static string Clock(double minutes) =>
        TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes % (24 * 60)))
            .ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string LaneRow(Lane lane, RangeReport report, DateOnly[] days, double from, double span)
    {
        var cells = new StringBuilder();

        foreach (var day in days)
        {
            cells.Append(
                "<div class=\"flex-fill position-relative rounded-1\" style=\"height:22px;background:#EEF1F4\">"
                + Marks(lane, report, day, from, span)
                + "</div>");
        }

        return "    <tr>"
            + $"<td>{Esc(lane.Project)} · {Esc(lane.Topic)}</td>"
            + $"<td class=\"text-end text-nowrap\">{Esc(Shares.Hours(lane.Minutes))}</td>"
            + $"<td><div class=\"d-flex gap-1\">{cells}</div></td>"
            + "</tr>";
    }

    /// <summary>Every range of one lane on one day, placed on the hours that were worked.</summary>
    private static string Marks(Lane lane, RangeReport report, DateOnly date, double from, double span)
    {
        var marks = new StringBuilder();

        foreach (var entry in report.Days.Where(day => day.Date == date).SelectMany(day => day.Entries).Where(lane.Holds))
        {
            marks.Append(
                $"<span class=\"position-absolute top-0 bottom-0 bg-{lane.Colour} rounded-1\""
                + $" style=\"{Place(entry, from, span)}\" title=\"{Esc(When(entry))}\"></span>");
        }

        return marks.ToString();
    }

    /// <summary>Where a range sits in that window, as left and width, and never thinner than a hair.</summary>
    private static string Place(EntryLine entry, double from, double span)
    {
        var left = (entry.Start.ToTimeSpan().TotalMinutes - from) / span * 100;
        var width = Math.Max(entry.Minutes / span * 100, 2);

        return $"left:{Frac(left)}%;width:{Frac(width)}%";
    }

    private static string When(EntryLine entry) =>
        $"{entry.Start:HH:mm}–{entry.End:HH:mm} · {Shares.Hours(entry.Minutes)}";

    private static string Frac(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

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
                $"      <span class=\"fw-semibold\">{Esc(day.Date.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture))}</span>");
            page.AppendLine(
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
                $"      <tr><td class=\"text-nowrap\">{entry.Start:HH:mm}–{entry.End:HH:mm}</td>"
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
            <p class="mb-0">This page loads Bootstrap from jsDelivr. Without a connection it still reads, unstyled.</p>
          </footer>

          """;

    /// <summary>
    /// The page's one script: pressing a heading sorts its column, by the number in the cell when the
    /// column is time or share and by the name otherwise. It is inline and it is all there is, so the
    /// page sorts itself from a file with no connection.
    /// </summary>
    private static string Scripts() =>
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
