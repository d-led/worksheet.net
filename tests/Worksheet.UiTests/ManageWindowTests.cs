// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Worksheet.Core;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.UiTests;

/// <summary>
/// The manage window: what is in use with what it holds, and one decision taken through the real
/// controls rather than the view model.
/// </summary>
public sealed class ManageWindowTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private static ManageWindow Opened(TempDirectory temp, out LocalLedger local)
    {
        local = LocalLedger.Open(temp.Path, SnapshotPolicy.Disabled);

        new EntryComposer(local.Ledger).Compose(
            EntryCommandParser.Parse("Website (login) 09:00 1h", Day).Command!);

        var window = new ManageWindow { DataContext = new ManageViewModel(local.Ledger) };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static IEnumerable<string> Labels(Window window, string list) =>
        window.FindControl<ListBox>(list)!.ItemsSource!.Cast<CatalogueRow>().Select(row => row.Label);

    [AvaloniaFact]
    public void The_window_lists_the_projects_and_the_topics_of_the_chosen_one()
    {
        using var temp = new TempDirectory("worksheet-manage-window");

        var window = Opened(temp, out var local);

        Assert.Equal(["Website (1 entry)"], Labels(window, "ProjectList"));
        Assert.Equal(["login (1 entry)"], Labels(window, "TopicList"));

        local.Dispose();
    }

    [AvaloniaFact]
    public void Renaming_the_chosen_project_through_the_window_changes_the_list()
    {
        using var temp = new TempDirectory("worksheet-manage-window");

        var window = Opened(temp, out var local);
        window.FindControl<TextBox>("NewProjectName")!.Text = "Client portal";

        Mouse.Click(window, window.FindControl<Button>("RenameProject")!);

        Assert.Equal(["Client portal (1 entry)"], Labels(window, "ProjectList"));
        Assert.Equal("The project is called 'Client portal' now.", window.FindControl<TextBlock>("Message")!.Text);

        local.Dispose();
    }
}
