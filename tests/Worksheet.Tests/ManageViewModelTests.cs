// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.Tests;

/// <summary>
/// The manage panel: what is in use and what it holds, the four decisions it offers, and undo —
/// each decision being one journal change is what makes the last one reversible from here.
/// </summary>
public sealed class ManageViewModelTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private readonly Ledger _ledger = Core.Ledger.Open(new InMemoryJournal());
    private readonly EntryComposer _composer;

    public ManageViewModelTests() => _composer = new EntryComposer(_ledger);

    private void Logged(string line) =>
        Assert.True(
            _composer.Compose(EntryCommandParser.Parse(line, Day).Command!).Created,
            line);

    private void Opened()
    {
        Logged("Website (login) 09:00 1h");
        Logged("Website (reports) 10:00 30m");
        Logged("Mobile 11:00 1h");
    }

    [Fact]
    public void The_panel_lists_the_projects_in_use_with_what_each_holds()
    {
        Opened();

        var panel = new ManageViewModel(_ledger);

        Assert.Equal(["Mobile (1 entry)", "Website (2 entries)"], panel.Projects.Select(project => project.Label));
    }

    [Fact]
    public void The_panel_shows_a_rename_made_behind_the_window()
    {
        Opened();
        var panel = new ManageViewModel(_ledger);

        Assert.True(new Catalogue(_ledger).RenameProject("Website", "Client portal").Accepted);

        Assert.Contains(panel.Projects, row => row.Label == "Client portal (2 entries)");
    }

    [Fact]
    public void The_chosen_project_shows_its_topics_with_what_each_holds()
    {
        Opened();

        var panel = new ManageViewModel(_ledger) { SelectedProject = Row("Website") };

        Assert.Equal(["login (1 entry)", "reports (1 entry)"], panel.Topics.Select(topic => topic.Label));
    }

    [Fact]
    public void The_dropped_hours_can_go_to_any_other_project()
    {
        Opened();

        var panel = new ManageViewModel(_ledger) { SelectedProject = Row("Website") };

        Assert.Equal(["Mobile"], panel.MoveTargets);
        Assert.Equal("Mobile", panel.MoveEntriesTo);
    }

    [Fact]
    public void Renaming_the_chosen_project_keeps_it_chosen_and_says_so()
    {
        Opened();
        var panel = new ManageViewModel(_ledger) { SelectedProject = Row("Website"), NewProjectName = "Client portal" };

        panel.RenameProjectCommand.Execute(null);

        Assert.Equal("Client portal", panel.SelectedProject!.Name);
        Assert.Equal("The project is called 'Client portal' now.", panel.Message);
        Assert.False(panel.MessageIsProblem);
    }

    [Fact]
    public void A_project_that_still_holds_entries_is_not_dropped_without_them()
    {
        Opened();
        var panel = new ManageViewModel(_ledger) { SelectedProject = Row("Website"), MoveEntriesTo = string.Empty };

        panel.RemoveProjectCommand.Execute(null);

        Assert.Equal("Website still has 2 entries; name the project they move to.", panel.Message);
        Assert.True(panel.MessageIsProblem);
    }

    [Fact]
    public void Dropping_the_chosen_project_takes_it_out_of_the_list()
    {
        Opened();
        var panel = new ManageViewModel(_ledger) { SelectedProject = Row("Website") };

        panel.RemoveProjectCommand.Execute(null);

        Assert.Equal(["Mobile (3 entries)"], panel.Projects.Select(project => project.Label));
        Assert.Equal("'Website' is out of the catalogue.", panel.Message);
    }

    [Fact]
    public void A_topic_can_be_renamed_from_the_panel()
    {
        Opened();
        var panel = new ManageViewModel(_ledger) { SelectedProject = Row("Website"), NewTopicName = "sign-in" };
        panel.SelectedTopic = panel.Topics.Single(topic => topic.Name == "login");

        panel.RenameTopicCommand.Execute(null);

        Assert.Equal(["reports", "sign-in"], panel.Topics.Select(topic => topic.Name));
    }

    [Fact]
    public void A_topic_can_be_dropped_from_the_panel_and_its_entries_stay()
    {
        Opened();
        var panel = new ManageViewModel(_ledger) { SelectedProject = Row("Website") };
        panel.SelectedTopic = panel.Topics.Single(topic => topic.Name == "login");

        panel.RemoveTopicCommand.Execute(null);

        Assert.Equal(["reports (1 entry)"], panel.Topics.Select(topic => topic.Label));
        Assert.Equal("'login' is out of Website; its entries stay without a topic.", panel.Message);
    }

    [Fact]
    public void Undo_from_the_panel_puts_a_dropped_project_back()
    {
        Opened();
        var panel = new ManageViewModel(_ledger) { SelectedProject = Row("Website") };
        panel.RemoveProjectCommand.Execute(null);

        panel.UndoCommand.Execute(null);

        Assert.Equal(["Mobile (1 entry)", "Website (2 entries)"], panel.Projects.Select(project => project.Label));
        Assert.Equal("Undone.", panel.Message);
    }

    [Fact]
    public void The_panel_itself_says_what_it_did()
    {
        Opened();
        var panel = new ManageViewModel(_ledger)
        {
            SelectedProject = Row("Website"),
            NewProjectName = "Portal",
        };

        panel.RenameProjectCommand.Execute(null);

        Assert.Equal("The project is called 'Portal' now.", panel.Message);
        Assert.Contains(panel.Projects, row => row.Name == "Portal");
    }

    [Fact]
    public void Without_a_choice_the_panel_asks_for_one()    {
        Opened();
        var panel = new ManageViewModel(_ledger) { SelectedProject = null, NewProjectName = "Anything" };

        panel.RenameProjectCommand.Execute(null);

        Assert.Equal("Choose a project first.", panel.Message);
        Assert.True(panel.MessageIsProblem);
    }

    [Fact]
    public void Undo_with_nothing_behind_it_says_there_is_nothing_to_undo()
    {
        var panel = new ManageViewModel(_ledger);

        panel.UndoCommand.Execute(null);

        Assert.Equal("There is nothing to undo.", panel.Message);
        Assert.False(panel.CanUndo);
    }

    private CatalogueRow Row(string name) =>
        new ManageViewModel(_ledger).Projects.Single(project => project.Name == name);
}
