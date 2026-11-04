// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;

namespace Worksheet.Export;

/// <summary>How much of the calendar an export covers, counted from the day on screen.</summary>
public enum ExportPeriod
{
    Day,
    Month,
    Quarter,
    Year,
}

/// <summary>One stretch of days, named the way a person would say it.</summary>
public sealed record ExportRange(DateOnly From, DateOnly To, string Label, ExportPeriod Period)
{
    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>Whether the daily detail is worth printing: one day is its own detail.</summary>
    public bool SpansMoreThanADay => Days > 1;

    /// <summary>The whole month, quarter or year the day belongs to; a day is itself.</summary>
    public static ExportRange For(ExportPeriod period, DateOnly anchor) =>
        period switch
        {
            ExportPeriod.Day => new ExportRange(anchor, anchor, anchor.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture), period),
            ExportPeriod.Month => MonthOf(anchor),
            ExportPeriod.Quarter => QuarterOf(anchor),
            _ => YearOf(anchor),
        };

    /// <summary>The name a file takes: short, sortable, and free of anything a file system minds.</summary>
    public string FileStem =>
        Period switch
        {
            ExportPeriod.Day => $"worksheet-{From:yyyy-MM-dd}",
            ExportPeriod.Month => $"worksheet-{From:yyyy-MM}",
            ExportPeriod.Quarter => $"worksheet-{From:yyyy}-Q{Quarter}",
            _ => $"worksheet-{From:yyyy}",
        };

    public int Quarter => (From.Month - 1) / 3 + 1;

    private static ExportRange MonthOf(DateOnly anchor)
    {
        var from = new DateOnly(anchor.Year, anchor.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        return new ExportRange(from, to, from.ToString("MMMM yyyy", CultureInfo.InvariantCulture), ExportPeriod.Month);
    }

    private static ExportRange QuarterOf(DateOnly anchor)
    {
        var firstMonth = (anchor.Month - 1) / 3 * 3 + 1;
        var from = new DateOnly(anchor.Year, firstMonth, 1);
        var to = from.AddMonths(3).AddDays(-1);
        var quarter = (firstMonth - 1) / 3 + 1;

        return new ExportRange(from, to, $"Q{quarter} {anchor.Year}", ExportPeriod.Quarter);
    }

    private static ExportRange YearOf(DateOnly anchor)
    {
        var from = new DateOnly(anchor.Year, 1, 1);

        return new ExportRange(from, from.AddYears(1).AddDays(-1), $"{anchor.Year}", ExportPeriod.Year);
    }
}
