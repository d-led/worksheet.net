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
/// The notice a person gets the first time they close the window: what happens next, and the two
/// answers they can give it.
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
    public void Closing_the_window_is_the_answer_that_leaves_the_application_running()
    {
        var window = Opened();

        Mouse.Click(window, window.FindControl<Button>("CloseWindow")!);

        Assert.False(window.QuitInstead);
        Assert.True(window.DoNotTellAgain);
    }

    [AvaloniaFact]
    public void Quitting_is_the_answer_that_leaves()
    {
        var window = Opened();

        Mouse.Click(window, window.FindControl<Button>("Quit")!);

        Assert.True(window.QuitInstead);
    }

    [AvaloniaFact]
    public void Escape_takes_the_harmless_answer()
    {
        var window = Opened();

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.QuitInstead);
    }

    [AvaloniaFact]
    public void The_notice_says_where_the_icon_is_and_offers_not_to_return()
    {
        var window = Opened();

        Assert.Contains("brings it back", window.FindControl<TextBlock>("Body")!.Text, StringComparison.Ordinal);
        Assert.True(window.FindControl<CheckBox>("Quiet")!.IsChecked);
    }
}
