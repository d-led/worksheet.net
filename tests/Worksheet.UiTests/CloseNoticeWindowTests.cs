// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;

namespace Worksheet.UiTests;

/// <summary>
/// The notice a person gets the first time the window is put aside: what happens next, and the one
/// answer it takes. Leaving is not among the answers — the tray's menu is where that lives.
/// </summary>
public sealed class CloseNoticeWindowTests
{
    private static CloseNoticeWindow Opened()
    {
        var window = new CloseNoticeWindow { DataContext = new CloseNoticeViewModel() };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [AvaloniaFact]
    public void The_one_answer_closes_the_window_and_keeps_the_day_running()
    {
        var window = Opened();

        Mouse.Click(window, window.FindControl<Button>("CloseWindow")!);

        Assert.False(window.IsVisible);
        Assert.True(window.DoNotTellAgain);
    }

    [AvaloniaFact]
    public void The_notice_offers_no_way_to_leave()
    {
        var window = Opened();

        Assert.Null(window.FindControl<Button>("Quit"));
    }

    [AvaloniaFact]
    public void Escape_is_an_answer_too()
    {
        var window = Opened();

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public void The_notice_says_where_the_icon_is_and_offers_not_to_return()
    {
        var window = Opened();

        Assert.Contains("brings it back", window.FindControl<TextBlock>("Body")!.Text, StringComparison.Ordinal);
        Assert.True(window.FindControl<CheckBox>("Quiet")!.IsChecked);
    }
}
