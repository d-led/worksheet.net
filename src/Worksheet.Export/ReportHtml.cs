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
/// CDN gives it its typography and its bars, mermaid from a CDN draws the gantt, and the page's own
/// two inline scripts sort a table when a heading is pressed and zoom the chart.
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

    /// <summary>
    /// The page, block by block in the order it is read; each block ends with its line end, and every
    /// line end is LF, whoever built it — the blocks are raw string literals, and those keep the line
    /// ends of the file they were checked out with, which on Windows is another kind.
    /// </summary>
    public static string Of(RangeReport report, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(report);

        return string.Concat(Blocks(report, generatedAt)).ReplaceLineEndings("\n");
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
        DayByProjectCard(report),
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
         {ChartStyle}</head>

         """;

    /// <summary>
    /// The viewport the chart is read in: it scrolls, and the zoom scales the canvas in place. What
    /// the chart *looks* like is mermaid's own business and is set in the diagram's frontmatter; this
    /// sheet only says how big it is on the page and how it moves.
    /// </summary>
    private const string ChartStyle = """
        <style>
          .gantt-tools { display: flex; align-items: center; gap: 4px; margin-bottom: 6px; }
          .gantt-view { overflow: auto; max-height: 70vh; cursor: grab; border: 1px solid #dee2e6; border-radius: 6px; }
          .gantt-view.dragging { cursor: grabbing; }
          .gantt-canvas svg { max-width: none !important; }
        </style>

        """;

    private static string Header(RangeReport report) =>
        $"""
         <header class="mb-3">
           <p class="text-body-secondary text-uppercase small mb-1">Worksheet · {Esc(Word(report.Range.Period))}</p>
           <h1 class="h3 mb-0">{Esc(report.Range.Label)}</h1>
         </header>

         """;

    /// <summary>
    /// The figures on one line, the way Bootstrap's own typography puts them: a muted label and a
    /// value, no boxes. The dates and the time of generation are small print below.
    /// </summary>
    private static string Summary(RangeReport report, DateTimeOffset generatedAt) =>
        $"""
         <section class="mb-4">
           <p class="mb-1">{Figures(report)}</p>
           <p class="text-body-secondary small mb-0">
             {Esc(DayText(report.Range.From))} – {Esc(DayText(report.Range.To))} · generated {Esc(Moment(generatedAt))}
           </p>
         </section>

         """;

    /// <summary>
    /// The figures a range has to show, in reading order. Total is everything the journal holds,
    /// and Working time follows it whenever a break is in the range, since a break is on the day
    /// without being work. The busiest day is the day the most was worked on, and is a figure only
    /// for a range of more than one: a day's own page already says that day in its heading, and the
    /// figure would be the heading read back.
    /// </summary>
    private static string Figures(RangeReport report)
    {
        var figures = new List<string>
        {
            Figure("Total", Esc(Shares.Hours(report.TotalMinutes)), first: true),
        };

        if (report.HoldsABreak)
        {
            figures.Add(Figure("Working time", Esc(Shares.Hours(report.WorkedMinutes))));
        }

        figures.Add(Figure("Days logged", report.DaysLogged.ToString(CultureInfo.InvariantCulture)));
        figures.Add(Figure("Projects", report.Projects.Count.ToString(CultureInfo.InvariantCulture)));

        if (report.Range.SpansMoreThanADay)
        {
            figures.Add(Figure("Busiest day", Esc(Busiest(report))));
        }

        return string.Join(' ', figures);
    }

    /// <summary>A label with its value beside it, spaced from the figure before unless it is the first.</summary>
    private static string Figure(string label, string value, bool first = false) =>
        $"""<span class="text-body-secondary{(first ? string.Empty : " ms-4")}">{label}</span> <span class="fs-5 fw-semibold">{value}</span>""";

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
            Rows(LargestFirst(report.Projects).Select(project => BarRow(report, [project.Project], project.Minutes, project.Project))),
            $"""<tr class="table-light fw-semibold"><td>Total</td><td class="text-end">{Esc(Shares.Hours(report.TotalMinutes))}</td><td></td><td class="text-end">100.0 %</td></tr>""");

    private static string TopicCard(RangeReport report) =>
        $"""
         <section class="card mb-4">
           <div class="card-body">
             <h2 class="h5 mb-3">By project and topic</h2>
             {(report.Projects.Count == 0 ? NoProjectsYet : Table(["Project", "Topic"], TopicRows(report)))}
           </div>
         </section>

         """;

    /// <summary>
    /// A project is named once, on the first of its rows — its longest topic — so the bars start
    /// close to the names.
    /// </summary>
    private static string TopicRows(RangeReport report) =>
        Rows(LargestFirst(report.Projects).SelectMany(project => LargestFirst(project.Topics).Select((topic, index) => BarRow(
            report,
            [index == 0 ? project.Project : string.Empty, topic.Label],
            topic.Minutes,
            project.Project))));

    /// <summary>Most time first, by name where two are worth the same: the card answers where the time went.</summary>
    private static IEnumerable<ProjectLine> LargestFirst(IEnumerable<ProjectLine> projects) =>
        projects.OrderByDescending(project => project.Minutes).ThenBy(project => project.Project, Dates.Names);

    /// <summary>The same within one project, so the topic that took the longest is the row naming it.</summary>
    private static IEnumerable<TopicLine> LargestFirst(IEnumerable<TopicLine> topics) =>
        topics.OrderByDescending(topic => topic.Minutes).ThenBy(topic => topic.Label, Dates.Names);

    /// <summary>The ranges of a day, the longest first, and in clock order where two are as long.</summary>
    private static IEnumerable<EntryLine> LargestFirst(IEnumerable<EntryLine> entries) =>
        entries.OrderByDescending(entry => entry.Minutes).ThenBy(entry => entry.Start);

    /// <summary>
    /// A table of bars: the cells that name a row, the time it stands for, the bar of that length and
    /// its share. Every heading but the bar sorts, and the rows come largest first until one is
    /// pressed, which is why the time heading says it is sorted down. The footer is the table's own
    /// total, so it stays where it is while the rows move.
    /// </summary>
    private static string Table(string[] headings, string rows, string footer = "") =>
        $"""
         <table class="table table-sm align-middle mb-0" data-sortable>
           <thead><tr>{string.Concat(headings.Select(heading => Column(heading, "text", string.Empty)))}{Column("Time", "number", "text-end", "descending")}<th scope="col" style="width:45%"></th>{Column("Share", "number", "text-end")}</tr></thead>
           <tbody>
         {rows}  </tbody>
         {(footer.Length == 0 ? string.Empty : $"<tfoot>\n{footer}\n</tfoot>\n")}</table>

         """;

    /// <summary>A heading cell, carrying the classes of a number's column when it has any.</summary>
    private static string Column(string heading, string kind, string classes, string? direction = null) =>
        $"""<th scope="col"{Attribute("class", classes)}{Attribute("aria-sort", direction)}>{Sorting(heading, kind, classes, direction)}</th>""";

    /// <summary>A heading that sorts its column when pressed, and says which way it is sorted now.</summary>
    private static string Sorting(string heading, string kind, string classes, string? direction = null) =>
        $"""<button type="button" class="btn btn-link p-0 fw-semibold text-reset {classes}" data-sort="{kind}"{Attribute("data-direction", direction)}>{Esc(heading)}</button>""";

    /// <summary>An attribute, or nothing at all when there is nothing to say.</summary>
    private static string Attribute(string name, string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : $" {name}=\"{value}\"";

    /// <summary>Rows as a page writes them: one per line, each with its own line end.</summary>
    private static string Rows(IEnumerable<string> rows) => string.Concat(rows.Select(row => $"{row}\n"));

    /// <summary>
    /// A row in the project's own pastel: the wash behind it, and a bar in the tint itself — or, for
    /// a break, the warm peach, which has no project to be tinted by. The time comes after the names
    /// and before the bar, so a row is read as "this much, drawn this long" from left to right.
    /// </summary>
    private static string BarRow(RangeReport report, string[] cells, int minutes, string project) =>
        $"""
         <tr style="--bs-table-bg:{Tints.Wash(report.Catalogue, project)}">{string.Concat(cells.Select(cell => $"<td>{Esc(cell)}</td>"))}<td class="text-end text-nowrap" data-value="{minutes}">{Esc(Shares.Hours(minutes))}</td><td><div class="progress" role="progressbar" aria-valuenow="{Esc(Shares.Percent(minutes, report.TotalMinutes))}" aria-valuemin="0" aria-valuemax="100" style="height:10px"><div class="progress-bar" style="width:{Shares.Width(minutes, report.TotalMinutes)};background:{Tints.Of(report.Catalogue, project) ?? BreakTint}"></div></div></td><td class="text-end text-nowrap" data-value="{minutes}">{Esc(Shares.Percent(minutes, report.TotalMinutes))}</td></tr>
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
    /// thicket rather than a picture.
    ///
    /// The chart is styled the way mermaid styles one: compact mode and the colours in the
    /// frontmatter, where `themeVariables` sets what every chart of the page shares — the font, the
    /// grid, the bands behind the bars — and `themeCSS` gives each bar the pastel of its project, the
    /// very colour the window stripes that project's row in.
    /// </summary>
    private static string Gantt(RangeReport report)
    {
        var perEntry = report.Range.Days <= 62;

        // The bars are numbered before they are grouped, so a section's tasks keep one running id —
        // and the same id names the bar in the stylesheet the frontmatter carries.
        var bars = Bars(report, perEntry)
            .Select((bar, index) => new ChartBar(bar.Project, $"e{index + 1}", bar.Label, bar.Start, bar.Minutes))
            .ToList();

        var sections = bars
            .GroupBy(bar => bar.Project)
            .SelectMany(project => new[] { $"    section {Label(project.Key)}" }
                .Concat(project.Select(bar => $"        {Label(bar.Label)} :{bar.Id}, {bar.Start}, {bar.Minutes}m")));

        return MermaidBlock(
            Rows(
            [
                "---",
                "displayMode: compact",
                "config:",
                "  theme: base",
                $"  themeCSS: \"{ChartCss(report, bars)}\"",
                "  themeVariables:",
                $"    fontFamily: {ChartFont}",
                "    fontSize: 12px",
                $"    titleColor: '{Ink}'",
                $"    sectionBkgColor: '{Band}'",
                $"    altSectionBkgColor: '{BandAlt}'",
                $"    sectionBkgColor2: '{BandAlt}'",
                $"    gridColor: '{Grid}'",
                $"    taskBkgColor: '{BreakTint}'",
                $"    taskBorderColor: '{BreakTint}'",
                $"    taskTextColor: '{Ink}'",
                $"    taskTextLightColor: '{Ink}'",
                $"    taskTextDarkColor: '{Ink}'",
                $"    taskTextOutsideColor: '{Ink}'",
                "    todayLineColor: 'transparent'",
                "---",
                "gantt",
                $"    title {Label(report.Range.Label)}",
                $"    dateFormat {(perEntry ? "YYYY-MM-DD HH:mm" : "YYYY-MM-DD")}",
                $"    axisFormat {(report.Range.Days == 1 ? "%H:%M" : "%d %b")}",
                "    todayMarker off",
                .. sections,
            ]));
    }

    /// <summary>One bar of the chart: what it says, where it starts, how long it is, and the id it is drawn under.</summary>
    private sealed record ChartBar(string Project, string Id, string Label, string Start, int Minutes);

    /// <summary>
    /// A rule per bar, in the pastel of its project: the colour is the one worked out for the
    /// catalogue, so a project reads the same in the window, in the page's bars and in the chart. A
    /// break has no tint of its own, so its bar takes the warm the page already gives it. Mermaid
    /// draws each bar under an id of its own, prefixed by the svg's, hence the suffix match.
    /// </summary>
    private static string ChartCss(RangeReport report, IEnumerable<ChartBar> bars) =>
        string.Concat(bars.Select(bar => BarRule(Tints.Of(report.Catalogue, bar.Project) ?? BreakTint, bar.Id)));

    private static string BarRule(string tint, string id) =>
        $"[id$='-{id}'] {{ fill: {tint}; stroke: {tint} }} ";

    /// <summary>The page's ink, for the chart's title and for the words beside a bar.</summary>
    private const string Ink = "#111827";

    /// <summary>A band behind a section, and the one beside it, quiet enough for a bar to sit on.</summary>
    private const string Band = "#f1f3f5";

    private const string BandAlt = "#f8f9fa";

    /// <summary>The chart's gridlines: the hairline a Bootstrap table draws between rows.</summary>
    private const string Grid = "#dee2e6";

    /// <summary>The font the chart's labels are set in, which is the page's own system stack.</summary>
    private const string ChartFont = "system-ui, -apple-system, Segoe UI, Roboto, sans-serif";

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

    /// <summary>
    /// The chart in a viewport of its own: a year of bars is wider than the page, so it scrolls, and
    /// the three buttons beside it zoom in, out and back to the whole width. Nothing here is fetched:
    /// the buttons and the drag are the page's own script.
    /// </summary>
    private static string MermaidBlock(string diagram) =>
        $"""
         <div class="gantt" data-gantt>
           <div class="gantt-tools">
             <button type="button" class="btn btn-sm btn-outline-secondary" data-gantt-zoom="out" title="Zoom out">−</button>
             <button type="button" class="btn btn-sm btn-outline-secondary" data-gantt-zoom="in" title="Zoom in">+</button>
             <button type="button" class="btn btn-sm btn-outline-secondary" data-gantt-zoom="reset" title="The whole width">Reset</button>
             <span class="small text-body-secondary ms-2">⌘/Ctrl + scroll to zoom · drag to pan</span>
           </div>
           <div class="gantt-view"><div class="gantt-canvas"><pre class="mermaid">
         {diagram.Replace("<", "&lt;").Replace(">", "&gt;")}</pre></div></div>
         </div>

         """;

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

    /// <summary>
    /// A day folded: its date and its figure on the summary, the ranges inside it. The figure is
    /// what the day was worked, since a break is on the day without being work, and the day's own
    /// total is said beside it only when a break makes the two differ — a fold whose rows add up
    /// to more than its figure would be the confusing thing.
    /// </summary>
    private static string Fold(IReadOnlyCollection<string> catalogue, DayLine day) =>
        $"""
         <details class="card mb-1"><summary class="card-body py-2">
           <span class="fw-semibold">{Esc(day.Date.ToString("dddd d MMM yyyy", CultureInfo.InvariantCulture))}</span>
           <span class="text-body-secondary">· {Esc(day.Kind)} · {Esc(Shares.Hours(day.WorkedMinutes))}{(day.WorkedMinutes == day.Minutes ? string.Empty : $" worked · {Esc(Shares.Hours(day.Minutes))} on the day")}</span>
         </summary>
         <div class="card-body py-2">
         {Entries(catalogue, day)}</div></details>

         """;

    /// <summary>
    /// The ranges of a day, the longest first — the order the cards above use — with the clock
    /// deciding between two of a length, so a day of equal blocks still reads forwards.
    /// </summary>
    private static string Entries(IReadOnlyCollection<string> catalogue, DayLine day) =>
        $"""
         <table class="table table-sm align-middle mb-0">
           <thead>
             <tr><th scope="col">From–to</th><th scope="col">Project</th><th scope="col">Topic</th><th scope="col" class="text-end">Time</th><th scope="col">Note</th></tr>
           </thead>
           <tbody>
         {Rows(LargestFirst(day.Entries).Select(entry => EntryRow(catalogue, entry)))}</tbody>
         </table>

         """;

    private static string EntryRow(IReadOnlyCollection<string> catalogue, EntryLine entry) =>
        $"""
         <tr style="--bs-table-bg:{Tints.Wash(catalogue, entry.Project)}"><td class="text-nowrap">{ClockText.Format(entry.Start)}–{ClockText.Format(entry.End)}</td><td>{Esc(entry.Project)}</td><td>{Esc(entry.Topic ?? string.Empty)}</td><td class="text-end">{Esc(Shares.Hours(entry.Minutes))}</td><td class="text-body-secondary">{Esc(entry.Note ?? string.Empty)}</td></tr>
         """;

    /// <summary>
    /// The range the other way round, ahead of the days themselves: a row per day and a column per
    /// project, so what each project took on each day can be read off at a glance, before the page
    /// goes on to the ranges of every day. The row ends in the day's working time rather than in a
    /// total: the projects already add up to that, and a break is a pause, not hours to be scared
    /// of. A range of one day is its own detail, so it carries no second reading of the same rows.
    /// </summary>
    private static string DayByProjectCard(RangeReport report) =>
        report.Range.SpansMoreThanADay
            ? $"""
               <section class="card mb-4">
                 <div class="card-body">
                   <h2 class="h5 mb-3">Each day, project by project</h2>
                   {DaysByProject(report)}
                 </div>
               </section>

               """
            : string.Empty;

    private static string DaysByProject(RangeReport report) =>
        $"""
         <div class="table-responsive">
           <table class="table table-sm align-middle mb-0">
             <thead><tr><th scope="col">Day</th>{string.Concat(report.Projects.Select(project => $"<th scope=\"col\" class=\"text-end\">{Esc(project.Project)}</th>"))}<th scope="col" class="text-end">Working time</th></tr></thead>
             <tbody>
         {Rows(report.Days.Select(day => DayRow(report, day)))}  </tbody>
             <tfoot><tr class="table-light fw-semibold"><td>All days</td>{string.Concat(report.Projects.Select(project => $"<td class=\"text-end\">{Esc(Shares.Hours(project.Minutes))}</td>"))}<td class="text-end">{Esc(Shares.Hours(report.WorkedMinutes))}</td></tr></tfoot>
           </table>
         </div>

         """;

    /// <summary>One day of that table: what each project took, and how much of the day was worked.</summary>
    private static string DayRow(RangeReport report, DayLine day) =>
        $"""
         <tr><td class="text-nowrap">{Esc(day.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture))}</td>{string.Concat(report.Projects.Select(project => DayCell(day, project.Project)))}<td class="text-end text-nowrap">{Esc(Shares.Hours(day.WorkedMinutes))}</td></tr>
         """;

    /// <summary>A day's cell for one project: its hours, or nothing at all when it was not worked on.</summary>
    private static string DayCell(DayLine day, string project)
    {
        var minutes = day.Entries.Where(entry => entry.Project == project).Sum(entry => entry.Minutes);

        return minutes == 0
            ? "<td></td>"
            : $"""<td class="text-end text-nowrap">{Esc(Shares.Hours(minutes))}</td>""";
    }

    /// <summary>The page's last word: where the numbers come from, and what the page needs from the net.</summary>
    private const string Footer = """
        <footer class="text-body-secondary small mt-5">
          <p class="mb-1">Made by Worksheet from its journal. Days that hold nothing are left out; the total is what the journal says, not a guess at a working day.</p>
          <p class="mb-1">A break is time on the day, so the projects and the day folds count it; Working time is the day without its breaks, and that is what the window's heading shows.</p>
          <p class="mb-0">This page loads Bootstrap and mermaid from jsDelivr. Without a connection it still reads, unstyled; the gantt is then not drawn.</p>
        </footer>

        """;

    /// <summary>
    /// The scripts: mermaid, which draws the gantt, and the page's own, which sorts a table by the
    /// number in the cell when the column is time or share and by the name otherwise, and which
    /// zooms the chart. Both are inline, so the tables and the zoom work from a file with no
    /// connection; only drawing the chart needs one.
    /// </summary>
    private static string Scripts() =>
        $$"""
          <script type="module">
            import mermaid from '{{Mermaid}}';
            mermaid.initialize({
              startOnLoad: false,
              securityLevel: 'strict',
              gantt: { barHeight: 16, barGap: 6, topPadding: 40, gridLineStartPadding: 20, fontSize: 11, sectionFontSize: 13 }
            });
            await mermaid.run();
            setupGanttZoom();
          </script>
          {{Sorter}}
          {{Zoomer}}

          """;

    /// <summary>
    /// The chart's zoom: a step in and out, back to the whole width, ⌘ or Ctrl with the wheel, and a
    /// drag that pans the viewport. Zooming widens the box the chart is drawn in, so the scroll range
    /// grows with the picture and a long range can be read a piece at a time.
    /// </summary>
    private const string Zoomer = """
        <script>
          function setupGanttZoom() {
            for (const gantt of document.querySelectorAll('[data-gantt]')) {
              const view = gantt.querySelector('.gantt-view');
              const canvas = gantt.querySelector('.gantt-canvas');

              if (!view || !canvas || !canvas.querySelector('svg')) {
                continue;
              }

              const fitted = view.clientWidth;
              let zoom = 1;

              const apply = next => {
                zoom = Math.min(4, Math.max(0.5, next));
                canvas.style.width = Math.round(fitted * zoom) + 'px';
              };

              for (const button of gantt.querySelectorAll('[data-gantt-zoom]')) {
                button.addEventListener('click', () => {
                  const step = button.dataset.ganttZoom;
                  apply(step === 'in' ? zoom * 1.25 : step === 'out' ? zoom / 1.25 : 1);
                });
              }

              view.addEventListener('wheel', event => {
                if (!event.ctrlKey && !event.metaKey) {
                  return;
                }

                event.preventDefault();
                apply(event.deltaY < 0 ? zoom * 1.1 : zoom / 1.1);
              }, { passive: false });

              let drag = null;

              view.addEventListener('pointerdown', event => {
                drag = { x: event.clientX, y: event.clientY, left: view.scrollLeft, top: view.scrollTop };
                view.classList.add('dragging');
                view.setPointerCapture(event.pointerId);
              });

              view.addEventListener('pointermove', event => {
                if (!drag) {
                  return;
                }

                view.scrollLeft = drag.left - (event.clientX - drag.x);
                view.scrollTop = drag.top - (event.clientY - drag.y);
              });

              for (const done of ['pointerup', 'pointercancel']) {
                view.addEventListener(done, () => {
                  drag = null;
                  view.classList.remove('dragging');
                });
              }
            }
          }
        </script>

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

    /// <summary>The day the most was worked on: a break there is time on the day, not time worked.</summary>
    private static string Busiest(RangeReport report) =>
        report.Days.MaxBy(day => day.WorkedMinutes) is { } busiest
            ? $"{busiest.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture)} · {Shares.Hours(busiest.WorkedMinutes)}"
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
            ExportPeriod.Week => "week",
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
