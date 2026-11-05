// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Worksheet.Core;
using Worksheet.Export;

namespace Worksheet.Tests;

/// <summary>
/// The export: which days a period covers, what the range is worth per project and per topic, what
/// the data file holds, what the page says, and that both arrive in one zip.
/// </summary>
public sealed class ExportTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    private static readonly DateTimeOffset Generated = new(2026, 11, 4, 9, 30, 0, TimeSpan.FromHours(1));

    private readonly Ledger _ledger = Core.Ledger.Open(new InMemoryJournal());

    private void Logged(string line) =>
        Assert.True(new EntryComposer(_ledger).Compose(EntryCommandParser.Parse(line, Tuesday).Command!).Created, line);

    private RangeReport Report(ExportPeriod period, DateOnly? anchor = null) =>
        RangeReport.Of(_ledger.State, ExportRange.For(period, anchor ?? Tuesday));

    private static string Json(RangeReport report) => ReportJson.Of(report, Generated);

    private static string Html(RangeReport report) => ReportHtml.Of(report, Generated);

    [Fact]
    public void A_month_is_the_whole_month_the_day_belongs_to()
    {
        var range = ExportRange.For(ExportPeriod.Month, Tuesday);

        Assert.Equal(new DateOnly(2026, 11, 1), range.From);
        Assert.Equal(new DateOnly(2026, 11, 30), range.To);
        Assert.Equal("November 2026", range.Label);
        Assert.Equal("worksheet-2026-11", range.FileStem);
    }

    [Fact]
    public void A_quarter_runs_from_the_first_of_its_first_month_to_the_end_of_its_last()
    {
        var range = ExportRange.For(ExportPeriod.Quarter, new DateOnly(2026, 12, 20));

        Assert.Equal(new DateOnly(2026, 10, 1), range.From);
        Assert.Equal(new DateOnly(2026, 12, 31), range.To);
        Assert.Equal("Q4 2026", range.Label);
        Assert.Equal("worksheet-2026-Q4", range.FileStem);
    }

    [Fact]
    public void A_year_is_the_whole_year_and_its_last_day_is_not_next_years()
    {
        var range = ExportRange.For(ExportPeriod.Year, new DateOnly(2024, 2, 29));

        Assert.Equal(new DateOnly(2024, 1, 1), range.From);
        Assert.Equal(new DateOnly(2024, 12, 31), range.To);
        Assert.Equal(366, range.Days);
    }

    [Fact]
    public void A_day_is_the_day_itself()
    {
        var range = ExportRange.For(ExportPeriod.Day, Tuesday);

        Assert.Equal(Tuesday, range.From);
        Assert.Equal(Tuesday, range.To);
        Assert.False(range.SpansMoreThanADay);
        Assert.Equal("worksheet-2026-11-03", range.FileStem);
    }

    [Fact]
    public void The_report_adds_up_per_project_and_per_topic_sorted_by_name()
    {
        Logged("Website (login) 09:00 1h");
        Logged("Website (reports) 10:00 30m");
        Logged("Mobile 11:00 45m");

        var report = Report(ExportPeriod.Day);

        Assert.Equal(135, report.TotalMinutes);
        Assert.Equal(["Mobile", "Website"], report.Projects.Select(project => project.Project));
        Assert.Equal(45, report.Projects[0].Minutes);
        Assert.Equal(["login", "reports"], report.Projects[1].Topics.Select(topic => topic.Topic));
        Assert.Equal(60, report.Projects[1].Topics[0].Minutes);
    }

    [Fact]
    public void Hours_logged_without_a_topic_are_named_as_such()
    {
        Logged("Mobile 11:00 45m");

        var report = Report(ExportPeriod.Day);

        Assert.Equal("without a topic", Assert.Single(report.Projects[0].Topics).Label);
    }

    [Fact]
    public void Days_with_nothing_on_them_are_left_out_and_the_others_are_in_order()
    {
        Logged("4.11. Website 09:00 1h");
        Logged("2.11. Website 09:00 1h");
        Logged("1.12. Website 09:00 1h");

        var report = Report(ExportPeriod.Month);

        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4)], report.Days.Select(day => day.Date));
        Assert.Equal(2, report.DaysLogged);
        Assert.Equal(120, report.TotalMinutes);
    }

    [Fact]
    public void The_data_file_holds_the_range_the_totals_the_shares_and_the_days()
    {
        Logged("Website (login) 09:00 1h");
        Logged("Mobile 11:00 3h");

        using var json = JsonDocument.Parse(Json(Report(ExportPeriod.Month)));
        var root = json.RootElement;

        Assert.Equal("2026-11-01", root.GetProperty("range").GetProperty("from").GetString());
        Assert.Equal("month", root.GetProperty("range").GetProperty("period").GetString());
        Assert.Equal(240, root.GetProperty("totalMinutes").GetInt32());
        Assert.Equal("4h", root.GetProperty("total").GetString());

        var mobile = root.GetProperty("projects")[0];
        Assert.Equal("Mobile", mobile.GetProperty("project").GetString());
        Assert.Equal(0.75, mobile.GetProperty("share").GetDouble(), 3);

        var day = root.GetProperty("days")[0];
        Assert.Equal("2026-11-03", day.GetProperty("date").GetString());
        Assert.Equal("09:00", day.GetProperty("entries")[0].GetProperty("start").GetString());
        Assert.Equal("login", day.GetProperty("entries")[0].GetProperty("topic").GetString());
    }

    [Fact]
    public void The_data_file_states_the_version_of_the_shape_it_is_written_in()
    {
        using var json = JsonDocument.Parse(Json(Report(ExportPeriod.Month)));

        Assert.True(json.RootElement.GetProperty("version").TryGetInt32(out var version), "version is a whole number");
        Assert.True(version >= 1, $"version {version} is at least the first");
    }

    /// <summary>
    /// Version 1 of the data file, written down: every member, in the order it appears, at every
    /// depth. A change here is a change to a file other people read, so it fails the build rather
    /// than surprising a script — and the version only moves when the change is a break.
    /// </summary>
    [Fact]
    public void The_data_file_keeps_the_shape_the_first_version_promised()
    {
        Logged("Website (login) 09:00 1h");

        using var json = JsonDocument.Parse(Json(Report(ExportPeriod.Month)));
        var root = json.RootElement;

        Assert.Equal(
            ["version", "range", "generatedAt", "totalMinutes", "total", "daysLogged", "projects", "days"],
            Keys(root));
        Assert.Equal(["from", "to", "period", "label", "days"], Keys(root.GetProperty("range")));
        Assert.Equal(["project", "minutes", "hours", "share", "topics"], Keys(root.GetProperty("projects")[0]));
        Assert.Equal(["topic", "minutes", "hours", "share"], Keys(root.GetProperty("projects")[0].GetProperty("topics")[0]));
        Assert.Equal(["date", "kind", "minutes", "hours", "entries"], Keys(root.GetProperty("days")[0]));
        Assert.Equal(
            ["start", "end", "minutes", "hours", "project", "topic", "note"],
            Keys(root.GetProperty("days")[0].GetProperty("entries")[0]));
    }

    private static string[] Keys(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name)];

    [Fact]
    public void The_zip_is_named_for_when_it_was_taken_and_what_it_covers()
    {
        var report = Report(ExportPeriod.Month);

        Assert.Equal("2026-11-04-0930-worksheet-2026-11.zip", ExportBundle.ZipName(report, Generated));
    }

    [Fact]
    public void Two_exports_of_one_range_are_named_apart_by_when_they_were_taken()
    {
        var report = Report(ExportPeriod.Month);

        Assert.NotEqual(
            ExportBundle.ZipName(report, Generated),
            ExportBundle.ZipName(report, Generated.AddMinutes(1)));
    }

    [Fact]
    public void The_page_carries_the_tables_the_figures_and_the_bars()
    {
        Logged("Website (login) 09:00 1h");
        Logged("Mobile 11:00 3h");

        var report = Report(ExportPeriod.Month);
        var page = Html(report);

        Assert.Contains("https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css", page);
        Assert.Contains("class=\"progress-bar\" style=\"width:75.0%;background:#", page);
        Assert.Contains($"<tr style=\"--bs-table-bg:{Tints.Wash(report.Catalogue, "Website")}\">", page);
        Assert.Contains(">Website</td>", page);
        Assert.Contains("75.0 %", page);
        Assert.Contains("4h", page);
    }

    [Fact]
    public void A_day_row_is_washed_in_its_projects_tint_and_a_break_row_in_the_warm_one()
    {
        Logged("Website 09:00 1h");
        Logged("Break 10:00 30m");

        var report = Report(ExportPeriod.Day);
        var page = Html(report);

        Assert.Contains($"<tr style=\"--bs-table-bg:{Tints.Wash(report.Catalogue, "Website")}\">", page);
        Assert.Contains($"<tr style=\"--bs-table-bg:{Tints.BreakWash}\">", page);
    }

    [Fact]
    public void The_page_marks_each_project_in_the_pastel_the_window_gives_it_and_a_break_in_none()
    {
        Logged("Website 09:00 1h");
        Logged("Break 10:00 30m");

        var report = Report(ExportPeriod.Day);
        var page = Html(report);

        Assert.Contains($"background:{Tints.Of(report.Catalogue, "Website")}", page);
        Assert.Contains("background:#f3c89a", page);
    }

    [Fact]
    public void The_page_draws_nothing_that_has_to_be_fetched_but_bootstrap_and_mermaid()
    {
        var page = Html(Report(ExportPeriod.Month));

        Assert.Single(Regex.Matches(page, "rel=\"stylesheet\""));
        Assert.DoesNotContain("src=", page);
        Assert.Equal(2, Regex.Count(page, "https://cdn.jsdelivr.net/npm/"));
    }

    [Fact]
    public void The_tables_sort_by_name_and_by_the_number_in_the_cell()
    {
        Logged("Website (login) 09:00 1h");

        var page = Html(Report(ExportPeriod.Day));

        Assert.Contains("data-sortable", page);
        Assert.Contains("data-sort=\"text\">Project</button>", page);
        Assert.Contains("data-sort=\"number\">Share</button>", page);
        Assert.Contains("data-value=\"60\"", page);    }

    [Fact]
    public void A_range_of_one_day_lists_its_entries_and_a_longer_one_lists_days()
    {
        Logged("Website (login) 09:00 1h");
        Logged("2.11. Website (login) 09:00 1h");

        var day = Html(Report(ExportPeriod.Day));
        var month = Html(Report(ExportPeriod.Month));

        Assert.DoesNotContain(">Days<", day);
        Assert.Contains(">Days<", month);
        Assert.Contains("<summary", month);
    }

    [Fact]
    public void The_timeline_is_a_gantt_in_a_swimlane_per_project()
    {
        Logged("Website (login) 09:00 1h");
        Logged("Website (login) 10:00 1h");
        Logged("Mobile (checkout) 11:00 1h");

        var page = Html(Report(ExportPeriod.Day));

        Assert.Contains("https://cdn.jsdelivr.net/npm/mermaid@12.3.0/dist/mermaid.esm.min.mjs", page);
        Assert.Contains("<pre class=\"mermaid\">", page);

        // Compact mode, asked for in the frontmatter, so the bars of a project share a row.
        Assert.Contains("<pre class=\"mermaid\">\n---\ndisplayMode: compact\n---\ngantt", page);

        // One swimlane per project, and a bar per range inside it — not a row per range, and not a
        // lane per range either.
        Assert.Contains("    section Website", page);
        Assert.Contains("    section Mobile", page);
        Assert.Equal(2, Regex.Count(page, "\n    section "));
        Assert.Equal(3, Regex.Count(page, " :e"));

        // A day's axis is its clocks; a range's is its days.
        Assert.Contains("axisFormat %H:%M", page);

        // A swimlane per project, and inside it a bar per range, in the order the day holds them.
        Assert.Matches(
            "section Website\n +login 1h :e\\d+, 2026-11-03 09:00, 60m\n +login 1h :e\\d+, 2026-11-03 10:00, 60m",
            page);
        Assert.Matches("section Mobile\n +checkout 1h :e\\d+, 2026-11-03 11:00, 60m", page);
    }

    [Fact]
    public void Markup_in_a_name_cannot_break_out_of_the_page()
    {
        Logged("<script>alert(1)</script> 09:00 1h");

        var page = Html(Report(ExportPeriod.Day));

        Assert.DoesNotContain("<script>alert(1)</script>", page);
        Assert.Contains("&lt;script&gt;", page);
    }

    [Fact]
    public void An_empty_range_says_so_rather_than_drawing_nothing()
    {
        var page = Html(Report(ExportPeriod.Month));

        Assert.Contains("Nothing was logged in this range.", page);
        Assert.Contains("Nothing to draw.", page);
        Assert.DoesNotContain("pie showData", page);
    }

    [Fact]
    public void The_export_is_one_zip_holding_the_data_and_the_page()
    {
        Logged("Website (login) 09:00 1h");

        var report = Report(ExportPeriod.Month);
        var zip = ExportBundle.Zip(report, Generated);

        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var names = archive.Entries.Select(entry => entry.FullName).Order().ToArray();

        Assert.Equal(["worksheet-2026-11.html", "worksheet-2026-11.json"], names);
        Assert.Equal("2026-11-04-0930-worksheet-2026-11.zip", ExportBundle.ZipName(report, Generated));
    }

    [Fact]
    public void The_files_in_the_zip_are_utf8_and_read_back_whole()
    {
        Logged("Website (login) 09:00 1h");

        var report = Report(ExportPeriod.Month);
        using var archive = new ZipArchive(new MemoryStream(ExportBundle.Zip(report, Generated)), ZipArchiveMode.Read);

        using var reader = new StreamReader(archive.Entries.Single(entry => entry.Name.EndsWith(".json", StringComparison.Ordinal)).Open(), Encoding.UTF8);
        var text = reader.ReadToEnd();

        Assert.Contains("\"totalMinutes\": 60", text);
    }
}
