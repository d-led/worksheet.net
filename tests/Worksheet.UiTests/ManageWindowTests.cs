// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Worksheet.Core;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.UiTests;

/// <summary>
/// The projects and topics window taken through its own controls: a name is typed where it stands,
/// the window says what Enter will do while it is still being typed, and one keystroke settles it.
/// </summary>
public sealed class ManageWindowTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private static ManageWindow Opened(TempDirectory temp, out LocalLedger local)
    {
        local = LocalLedger.Open(temp.Path, SnapshotPolicy.Disabled);

        var composer = new EntryComposer(local.Ledger);
        composer.Compose(EntryCommandParser.Parse("Website (login) 09:00 1h", Day).Command!);
        composer.Compose(EntryCommandParser.Parse("Mobile 11:00 1h", Day).Command!);

        var model = new ManageViewModel(local.Ledger);
        model.SelectedProject = model.Projects.Single(project => project.Name == "Website");

        var window = new ManageWindow { DataContext = model };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static string[] Names(Window window, string list) =>
        [.. Rows(window, list).Select(row => row.Name)];

    private static CatalogueRow[] Rows(Window window, string list) =>
        [.. window.FindControl<ListBox>(list)!.ItemsSource!.Cast<CatalogueRow>()];

    /// <summary>The field of one row, found the way a person finds it: by the name in it.</summary>
    private static TextBox Field(Window window, string list, string name) =>
        window.FindControl<ListBox>(list)!
            .GetVisualDescendants()
            .OfType<TextBox>()
            .Single(field => field.DataContext is CatalogueRow row && row.Name == name);

    private static void Type(Window window, TextBox field, string text)
    {
        field.Focus();
        field.Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void The_window_lists_the_projects_and_the_topics_of_the_chosen_one()
    {
        using var temp = new TempDirectory("worksheet-manage-window");

        var window = Opened(temp, out var local);

        Assert.Equal(["Mobile", "Website"], Names(window, "ProjectList"));
        Assert.Equal(["login"], Names(window, "TopicList"));

        local.Dispose();
    }

    [AvaloniaFact]
    public void A_name_typed_into_the_row_and_settled_with_enter_renames_it()
    {
        using var temp = new TempDirectory("worksheet-manage-window");

        var window = Opened(temp, out var local);
        var field = Field(window, "ProjectList", "Website");

        Type(window, field, "Client portal");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Client portal", "Mobile"], Names(window, "ProjectList"));
        Assert.Equal("The project is called 'Client portal' now.", window.FindControl<TextBlock>("Message")!.Text);

        local.Dispose();
    }

    [AvaloniaFact]
    public void A_name_that_is_already_taken_merges_the_two_through_the_window()
    {
        using var temp = new TempDirectory("worksheet-manage-window");

        var window = Opened(temp, out var local);
        var field = Field(window, "ProjectList", "Mobile");

        Type(window, field, "Website");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Website"], Names(window, "ProjectList"));
        Assert.Equal("'Mobile' merged into 'Website'; its 1 entry · 1h moved there.", window.FindControl<TextBlock>("Message")!.Text);

        local.Dispose();
    }

    [AvaloniaFact]
    public void The_window_says_what_enter_would_do_while_the_name_is_being_typed()
    {
        using var temp = new TempDirectory("worksheet-manage-window");

        var window = Opened(temp, out var local);
        var proposal = window.FindControl<TextBlock>("Proposal")!;

        Assert.False(proposal.IsVisible);

        Type(window, Field(window, "ProjectList", "Mobile"), "website");

        Assert.True(proposal.IsVisible);
        Assert.Equal("Enter merges 'Mobile' into 'Website' — its 1 entry · 1h move there.", proposal.Text);

        local.Dispose();
    }

    [AvaloniaFact]
    public void Choosing_a_project_by_its_field_shows_that_project_s_topics()
    {
        using var temp = new TempDirectory("worksheet-manage-window");

        var window = Opened(temp, out var local);
        var model = (ManageViewModel)window.DataContext!;

        Assert.Equal(["login"], Names(window, "TopicList"));

        Field(window, "ProjectList", "Mobile").Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Mobile", model.SelectedProject!.Name);
        Assert.Empty(Names(window, "TopicList"));

        local.Dispose();
    }
}
