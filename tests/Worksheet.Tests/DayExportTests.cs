// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.IO.Compression;
using Worksheet.Core;
using Worksheet.Desktop.ViewModels;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>
/// Exporting from the day: the range comes from the day on screen, the file is one zip, and where it
/// is written is not the day's decision — it hands the file over and says where it went.
/// </summary>
public sealed class DayExportTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    /// <summary>The moment the export is taken, so the file names do not depend on the clock.</summary>
    private static readonly DateTimeOffset Exported = new(2026, 11, 4, 9, 30, 0, TimeSpan.FromHours(1));

    private readonly TempDirectory _temp = new("worksheet-export");
    private readonly LocalLedger _local;

    public DayExportTests() => _local = LocalLedger.Open(_temp.Path, SnapshotPolicy.Disabled);

    public void Dispose()
    {
        _local.Dispose();
        _temp.Dispose();
    }

    private DayViewModel DayWith(params string[] lines)
    {
        var day = new DayViewModel(_local, Today, () => Exported);

        foreach (var line in lines)
        {
            day.EntryText = line;
            day.LogCommand.Execute(null);
        }

        return day;
    }

    [Fact]
    public async Task Exporting_a_month_hands_over_the_zip_and_says_where_it_went()
    {
        var day = DayWith("Website (login) 09:00 1h");
        ExportFile? handed = null;
        day.SaveExport = file =>
        {
            handed = file;
            return Task.FromResult<string?>("/somewhere/2026-11-04-0930-worksheet-2026-11.zip");
        };

        await day.ExportMonthCommand.ExecuteAsync(null);

        Assert.Equal("2026-11-04-0930-worksheet-2026-11.zip", handed!.FileName);
        Assert.Equal("Exported 2026-11-04-0930-worksheet-2026-11.zip.", day.Message);
        Assert.False(day.MessageIsProblem);
    }

    [Fact]
    public async Task What_is_handed_over_is_the_month_as_data_and_page_in_one_zip()
    {
        var day = DayWith("Website (login) 09:00 1h");
        ExportFile? handed = null;
        day.SaveExport = file =>
        {
            handed = file;
            return Task.FromResult<string?>("/somewhere/x.zip");
        };

        await day.ExportMonthCommand.ExecuteAsync(null);

        using var archive = new ZipArchive(new MemoryStream(handed!.Bytes), ZipArchiveMode.Read);

        Assert.Equal(
            ["worksheet-2026-11.html", "worksheet-2026-11.json"],
            archive.Entries.Select(entry => entry.Name).Order());
    }

    [Fact]
    public async Task The_day_and_the_year_are_the_other_ranges_the_button_offers()
    {
        var day = DayWith("Website (login) 09:00 1h");
        var names = new List<string>();
        day.SaveExport = file =>
        {
            names.Add(file.FileName);
            return Task.FromResult<string?>("/somewhere/x.zip");
        };

        await day.ExportDayCommand.ExecuteAsync(null);
        await day.ExportQuarterCommand.ExecuteAsync(null);
        await day.ExportYearCommand.ExecuteAsync(null);

        Assert.Equal(
            ["2026-11-04-0930-worksheet-2026-11-03.zip", "2026-11-04-0930-worksheet-2026-Q4.zip", "2026-11-04-0930-worksheet-2026.zip"],
            names);
    }

    [Fact]
    public async Task A_cancelled_export_writes_nothing_and_says_so()
    {
        var day = DayWith("Website 09:00 1h");
        day.SaveExport = _ => Task.FromResult<string?>(null);

        await day.ExportDayCommand.ExecuteAsync(null);

        Assert.Equal("Export cancelled.", day.Message);
        Assert.False(day.MessageIsProblem);
    }

    [Fact]
    public async Task An_export_with_nowhere_to_go_is_refused()
    {
        var day = DayWith("Website 09:00 1h");

        await day.ExportDayCommand.ExecuteAsync(null);

        Assert.Equal("There is nowhere to save an export from this window.", day.Message);
        Assert.True(day.MessageIsProblem);
    }
}
