// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.Text;

namespace Worksheet.Export;

/// <summary>
/// The report as one page: Bootstrap from a CDN to read it, mermaid from a CDN for the two
/// pictures — a pie for the shares and a gantt for the range as it was worked — and no script of
/// its own. The page is written to stand alone: opened from a file, it needs a connection for the
/// two libraries, and says in its footer what it depends on.
/// </summary>
public static class ReportHtml
{
    private const string Bootstrap = "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css";

    private const string Mermaid = "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs";

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

    private static string ProjectCard(RangeReport report) =>
        $$"""
          <section class="card mb-4">
            <div class="card-body">
              <h2 class="h5 mb-3">Where the time went</h2>
              <div class="row g-4 align-items-center">
                <div class="col-md-4">{{Pie(report)}}</div>
                <div class="col-md-8">{{ProjectsTable(report)}}</div>
              </div>
            </div>
          </section>

          """;

    private static string ProjectsTable(RangeReport report) =>
        report.Projects.Count == 0
            ? "<p class=\"text-body-secondary mb-0\">Nothing was logged in this range.</p>\n"
            : $$"""
              <table class="table table-sm align-middle w-auto mb-0">
                <thead>
                  <tr><th scope="col">Project</th><th scope="col" class="text-end">Time</th><th scope="col" class="text-end">Share</th></tr>
                </thead>
                <tbody>
                  {{Rows(report, report.Projects.Select(project => (project.Project, project.Minutes)))}}
                  <tr class="table-light fw-semibold">
                    <td>Total</td>
                    <td class="text-end">{{Esc(Shares.Hours(report.TotalMinutes))}}</td>
                    <td class="text-end">100.0 %</td>
                  </tr>
                </tbody>
              </table>

              """;

    private static string TopicCard(RangeReport report)
    {
        var pairs = report.Projects
            .SelectMany(project => project.Topics.Select(topic => (Project: project.Project, topic.Label, topic.Minutes)))
            .ToArray();

        return $$"""
          <section class="card mb-4">
            <div class="card-body">
              <h2 class="h5">By project and topic</h2>
              {{(pairs.Length == 0
                  ? "<p class=\"text-body-secondary mb-0\">No project was used yet in this range.</p>\n"
                  : TopicsTable(report, pairs))}}
            </div>
          </section>

          """;
    }

    private static string TopicsTable(RangeReport report, (string Project, string Topic, int Minutes)[] pairs)
    {
        var rows = new StringBuilder();

        foreach (var (project, topic, minutes) in pairs)
        {
            rows.AppendLine(
                $"      <tr><td>{Esc(project)}</td><td>{Esc(topic)}</td>"
                + $"<td class=\"text-end\">{Esc(Shares.Hours(minutes))}</td>"
                + $"<td class=\"text-end\">{Esc(Shares.Percent(minutes, report.TotalMinutes))}</td></tr>");
        }

        return $$"""
                   <table class="table table-sm align-middle w-auto mb-0">
                     <thead>
                       <tr><th scope="col">Project</th><th scope="col">Topic</th><th scope="col" class="text-end">Time</th><th scope="col" class="text-end">Share</th></tr>
                     </thead>
                     <tbody>
                 {{rows.ToString()}}    </tbody>
                   </table>

                 """;
    }

    private static string Rows(RangeReport report, IEnumerable<(string Label, int Minutes)> lines)
    {
        var rows = new StringBuilder();

        foreach (var (label, minutes) in lines)
        {
            rows.AppendLine(
                $"      <tr><td>{Esc(label)}</td>"
                + $"<td class=\"text-end\">{Esc(Shares.Hours(minutes))}</td>"
                + $"<td class=\"text-end\">{Esc(Shares.Percent(minutes, report.TotalMinutes))}</td></tr>");
        }

        return rows.ToString();
    }

    /// <summary>
    /// A pie rather than bars: the shares are of one whole, and a pie says that in the least width.
    /// The absolutes are in the table beside it, so nothing is lost to the picture.
    /// </summary>
    private static string Pie(RangeReport report)
    {
        var projects = report.Projects.Where(project => project.Minutes > 0).ToArray();

        if (projects.Length == 0)
        {
            return "<p class=\"text-body-secondary mb-0\">No hours to share out.</p>\n";
        }

        var chart = new StringBuilder("pie showData\n    title Hours by project\n");

        foreach (var project in projects)
        {
            var hours = (project.Minutes / 60.0).ToString("0.00", CultureInfo.InvariantCulture);

            chart.AppendLine($"    \"{Label(project.Project)}\" : {hours}");
        }

        return MermaidBlock(chart.ToString());
    }

    /// <summary>
    /// The range as it was worked. Up to a month every entry keeps its own bar; beyond that a bar
    /// per project per day, because a year of bars is a thicket rather than a picture.
    /// </summary>
    private static string Gantt(RangeReport report)
    {
        if (report.Days.Count == 0)
        {
            return "<p class=\"text-body-secondary mb-0\">Nothing to draw.</p>\n";
        }

        var perEntry = report.Range.Days <= 62;
        var lines = new StringBuilder();
        lines.AppendLine("gantt");
        lines.AppendLine($"    title {Label(report.Range.Label)}");
        lines.AppendLine(perEntry ? "    dateFormat YYYY-MM-DD HH:mm" : "    dateFormat YYYY-MM-DD");
        lines.AppendLine("    axisFormat %d %b");
        lines.AppendLine("    todayMarker off");

        var id = 0;

        foreach (var project in report.Projects)
        {
            lines.AppendLine($"    section {Label(project.Project)}");

            foreach (var bar in Bars(report, project.Project, perEntry))
            {
                lines.AppendLine($"        {bar.Label} :e{++id}, {bar.Start}, {bar.Minutes}m");
            }
        }

        return MermaidBlock(lines.ToString());
    }

    private static IEnumerable<(string Label, string Start, int Minutes)> Bars(RangeReport report, string project, bool perEntry) =>
        perEntry ? EntryBars(report, project) : DayBars(report, project);

    private static IEnumerable<(string Label, string Start, int Minutes)> EntryBars(RangeReport report, string project) =>
        report.Days
            .SelectMany(day => day.Entries.Select(entry => (Day: day.Date, Entry: entry)))
            .Where(pair => pair.Entry.Project == project)
            .Select(pair => (
                $"{Label(pair.Entry.Topic ?? pair.Entry.Project)} {Shares.Hours(pair.Entry.Minutes)}",
                $"{IsoDay(pair.Day)} {Clock(pair.Entry.Start)}",
                pair.Entry.Minutes));

    private static IEnumerable<(string Label, string Start, int Minutes)> DayBars(RangeReport report, string project) =>
        report.Days
            .Select(day => (day.Date, Minutes: day.Entries.Where(entry => entry.Project == project).Sum(entry => entry.Minutes)))
            .Where(day => day.Minutes > 0)
            .Select(day => (
                $"{Reports(day.Date)} ({Shares.Hours(day.Minutes)})",
                IsoDay(day.Date),
                Math.Min(day.Minutes, 24 * 60)));

    private static string TimelineCard(RangeReport report) =>
        $$"""
          <section class="card mb-4">
            <div class="card-body">
              <h2 class="h5">The range as it was worked</h2>
              {{Gantt(report)}}
            </div>
          </section>

          """;

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
            <p class="mb-0">This page loads Bootstrap and mermaid from jsDelivr. Without a connection the tables still read; the two pictures do not.</p>
          </footer>

          """;

    private static string Scripts() =>
        $$"""
          <script type="module">
            import mermaid from '{{Mermaid}}';
            mermaid.initialize({
              startOnLoad: true,
              securityLevel: 'strict',
              theme: 'default',
              gantt: { barHeight: 16, barGap: 6, topPadding: 40, gridLineStartPadding: 20, fontSize: 11, sectionFontSize: 13, numberSectionStyles: 2 },
              pie: { textPosition: 0.62 }
            });
          </script>

          """;

    private static string MermaidBlock(string diagram) =>
        $"<div class=\"overflow-auto\"><pre class=\"mermaid\">\n{diagram.Replace("<", "&lt;").Replace(">", "&gt;")}</pre></div>\n";

    /// <summary>A label mermaid can read: no colon (it starts a task's data), no quote, one line.</summary>
    private static string Label(string text) =>
        text.Replace("\r", " ").Replace("\n", " ").Replace(":", "·").Replace("\"", "'").Trim();

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

    /// <summary>How a day is written for mermaid, which parses the format its own chart declares.</summary>
    private static string IsoDay(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Clock(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

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
