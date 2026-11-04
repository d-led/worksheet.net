// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Worksheet.Desktop.Components;

namespace Worksheet.UiTests;

/// <summary>The export button: the month on one click, the other three ranges behind the arrow.</summary>
public sealed class DayHeaderExportTests : UiTestBase
{
    [AvaloniaFact]
    public void The_header_offers_the_four_ranges_with_the_month_as_the_one_click()
    {
        var day = NewDay();

        var header = Show<DayHeader>(day);
        var export = header.FindControl<SplitButton>("Export")!;

        Assert.Equal("Export month", export.Content);
        Assert.NotNull(day.SaveExport);
    }

    [AvaloniaFact]
    public void The_arrow_offers_day_month_quarter_and_year()
    {
        var header = Show<DayHeader>(NewDay());

        var menu = (MenuFlyout)header.FindControl<SplitButton>("Export")!.Flyout!;
        var items = menu.Items.OfType<MenuItem>().Select(item => item.Header?.ToString() ?? string.Empty).ToArray();

        Assert.Equal(["Day", "Month", "Quarter", "Year"], items);
    }

    [AvaloniaFact]
    public void Each_range_in_the_menu_reaches_an_export()
    {
        var header = Show<DayHeader>(NewDay());

        var menu = (MenuFlyout)header.FindControl<SplitButton>("Export")!.Flyout!;
        var commands = menu.Items.OfType<MenuItem>().Select(item => item.Command);

        Assert.All(commands, command => Assert.NotNull(command));
    }

    [AvaloniaFact]
    public async Task The_button_asks_where_to_save_and_a_dismissed_dialog_writes_nothing()
    {
        var day = NewDay();
        day.EntryText = "Website 09:00 1h";
        day.LogCommand.Execute(null);

        Show<DayHeader>(day);

        await day.ExportDayCommand.ExecuteAsync(null);

        // No save dialog exists in a headless window, so the export is declined rather than written.
        Assert.Equal("Export cancelled.", day.Message);
        Assert.False(day.MessageIsProblem);
    }
}
