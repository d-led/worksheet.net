// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Worksheet.Desktop.Components;

namespace Worksheet.UiTests;

/// <summary>The strip under the day: each project it was spent on, and how much of it went there.</summary>
public sealed class ProjectTotalsTests : UiTestBase
{
    private static string[] Chips(Control strip) =>
        [.. Descendants<TextBlock>(strip).Select(text => text.Text ?? string.Empty).Where(text => text.Length > 0)];

    [AvaloniaFact]
    public void A_day_with_nothing_on_it_shows_no_shares()
    {
        var strip = Show<ProjectTotals>(NewDay());

        Assert.False(strip.FindControl<ItemsControl>("Totals")!.IsVisible);
        Assert.Empty(Chips(strip));
    }

    [AvaloniaFact]
    public void Each_project_of_the_day_is_named_with_the_hours_it_took()
    {
        var day = NewDay();
        var strip = Show<ProjectTotals>(day);

        Seed(day, "Website 09:00 2h");
        Seed(day, "Mobile 11:00 30m");

        Assert.True(strip.FindControl<ItemsControl>("Totals")!.IsVisible);
        Assert.Equal(["Website", "2h", "Mobile", "30m"], Chips(strip));
    }

    [AvaloniaFact]
    public void A_break_is_on_the_day_but_is_not_a_share_of_the_work()
    {
        var day = NewDay();
        var strip = Show<ProjectTotals>(day);

        Seed(day, "Website 09:00 2h");
        Seed(day, "Break 11:00 30m");

        Assert.Equal(["Website", "2h"], Chips(strip));
    }
}
