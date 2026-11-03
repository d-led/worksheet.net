// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Worksheet.Desktop.Components;
using Worksheet.Desktop.Views;

namespace Worksheet.UiTests;

public sealed class MainWindowTests : UiTestBase
{
    private MainWindow OpenWindow()
    {
        var window = new MainWindow { DataContext = NewDay() };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [AvaloniaFact]
    public void The_window_is_the_four_components_and_nothing_else()
    {
        var window = OpenWindow();

        Assert.NotNull(window.FindControl<DayHeader>("Header"));
        Assert.NotNull(window.FindControl<QuickEntry>("Entry"));
        Assert.NotNull(window.FindControl<DayTable>("Table"));
        Assert.NotNull(window.FindControl<StatusLine>("Status"));

        window.AllowClose = true;
        window.Close();
    }

    [AvaloniaFact]
    public void Closing_the_window_hides_it_and_only_quitting_really_closes_it()
    {
        var window = OpenWindow();
        var closed = false;
        window.Closed += (_, _) => closed = true;

        window.Close();
        Dispatcher.UIThread.RunJobs();

        // Closing was cancelled, so the window is hidden rather than gone.
        Assert.False(closed);
        Assert.False(window.IsVisible);

        window.AllowClose = true;
        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    }

    [AvaloniaFact]
    public void Hiding_and_showing_again_keeps_the_same_day()
    {
        var window = OpenWindow();
        var day = (Worksheet.Desktop.ViewModels.DayViewModel)window.DataContext!;
        var title = day.DayTitle;

        window.Close();
        Dispatcher.UIThread.RunJobs();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(title, day.DayTitle);
        Assert.True(window.IsVisible);

        window.AllowClose = true;
        window.Close();
    }
}
