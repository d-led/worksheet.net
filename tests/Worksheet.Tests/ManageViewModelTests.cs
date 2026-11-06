// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.Tests;

/// <summary>
/// The projects and topics panel: a name typed into its own row, the sentence that says what Enter
/// will do before it is pressed, and undo — one decision, one journal change, one step back.
/// </summary>
public sealed class ManageViewModelTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private readonly Ledger _ledger = Core.Ledger.Open(new InMemoryJournal());
    private readonly EntryComposer _composer;

    public ManageViewModelTests() => _composer = new EntryComposer(_ledger);

    /// <summary>Website with two topics and two entries, Mobile with one entry.</summary>
    private ManageViewModel Opened()
    {
        Logged("Website (login) 09:00 1h");
        Logged("Website (reports) 10:00 30m");
        Logged("Mobile 11:00 1h");

        return new ManageViewModel(_ledger);
    }

    private void Logged(string line) =>
        Assert.True(
            _composer.Compose(EntryCommandParser.Parse(line, Day).Command!).Created,
            line);

    private static string[] Names(IEnumerable<CatalogueRow> rows) =>
        [.. rows.Select(row => row.Name)];

    private static CatalogueRow Row(ManageViewModel panel, string name) =>
        panel.Projects.Single(project => project.Name == name);

    private static void Typed(CatalogueRow row, string name) => row.Draft = name;

    [Fact]
    public void The_panel_lists_the_projects_in_use_with_what_each_holds()
    {
        var panel = Opened();

        Assert.Equal(["Mobile", "Website"], Names(panel.Projects));
        Assert.Equal("1 entry · 1h", Row(panel, "Mobile").Holds);
        Assert.Equal("2 entries · 1h30m", Row(panel, "Website").Holds);
    }

    [Fact]
    public void The_chosen_project_shows_its_topics_with_what_each_holds()
    {
        var panel = Opened();
        panel.SelectedProject = Row(panel, "Website");

        Assert.Equal(["login", "reports"], Names(panel.Topics));
        Assert.Equal("1 entry · 1h", panel.Topics[0].Holds);
    }

    [Fact]
    public void A_name_typed_into_its_row_renames_it()
    {
        var panel = Opened();
        var website = Row(panel, "Website");

        Typed(website, "Client portal");
        website.CommitCommand.Execute(null);

        Assert.Equal(["Client portal", "Mobile"], Names(panel.Projects));
        Assert.Equal("The project is called 'Client portal' now.", panel.Message);
        Assert.False(panel.MessageIsProblem);
    }

    [Fact]
    public void A_name_another_project_answers_to_merges_the_two_and_says_what_moved()
    {
        var panel = Opened();
        var mobile = Row(panel, "Mobile");

        Typed(mobile, "Website");
        mobile.CommitCommand.Execute(null);

        Assert.Equal(["Website"], Names(panel.Projects));
        Assert.Equal("3 entries · 2h30m", Row(panel, "Website").Holds);
        Assert.Equal("'Mobile' merged into 'Website'; its 1 entry · 1h moved there.", panel.Message);
    }

    [Fact]
    public void The_panel_says_what_enter_would_do_before_it_is_pressed()
    {
        var panel = Opened();

        Typed(Row(panel, "Website"), "Client portal");
        Assert.Equal("Enter renames 'Website' to 'Client portal'.", panel.Proposal);

        Typed(Row(panel, "Website"), "  ");
        Assert.Equal("A project needs a name.", panel.Proposal);
    }

    [Fact]
    public void A_name_that_was_refused_leaves_the_row_able_to_take_another()
    {
        var panel = Opened();
        var website = Row(panel, "Website");

        Typed(website, "   ");
        website.CommitCommand.Execute(null);
        Assert.True(panel.MessageIsProblem);

        Typed(website, "Client portal");
        website.CommitCommand.Execute(null);

        Assert.Equal(["Client portal", "Mobile"], Names(panel.Projects));
        Assert.False(panel.MessageIsProblem);
    }

    [Fact]
    public void A_name_that_is_already_there_is_announced_as_a_merge()
    {
        var panel = Opened();

        Typed(Row(panel, "Website"), "mobile");

        Assert.Equal("Enter merges 'Website' into 'Mobile' — its 2 entries · 1h30m move there.", panel.Proposal);
    }

    [Fact]
    public void A_row_left_as_it_was_says_nothing()
    {
        var panel = Opened();

        Typed(Row(panel, "Website"), "Website");

        Assert.Null(panel.Proposal);
        Assert.False(panel.HasProposal);
    }

    [Fact]
    public void Escape_puts_the_name_back_and_writes_nothing()
    {
        var panel = Opened();
        var before = _ledger.Version;
        var website = Row(panel, "Website");

        Typed(website, "Something else");
        website.RevertCommand.Execute(null);

        Assert.Equal("Website", website.Draft);
        Assert.Equal(before, _ledger.Version);
        Assert.Null(panel.Message);
    }

    [Fact]
    public void A_row_settled_once_is_not_renamed_again_by_leaving_the_field()
    {
        var panel = Opened();
        var website = Row(panel, "Website");

        Typed(website, "Portal");
        website.CommitCommand.Execute(null);
        website.CommitCommand.Execute(null);

        Assert.Equal(["Mobile", "Portal"], Names(panel.Projects));
        Assert.Equal("The project is called 'Portal' now.", panel.Message);
    }

    [Fact]
    public void Archiving_a_project_leaves_its_hours_where_they_are()
    {
        var panel = Opened();

        Row(panel, "Website").ArchiveCommand.Execute(null);

        Assert.Equal("'Website' is out of the catalogue; its entries stay as they are.", panel.Message);
        Assert.False(panel.MessageIsProblem);
        Assert.Equal(["Mobile"], Names(panel.Projects));
        Assert.Equal(2, _ledger.State.Entries.Values.Count(entry => _ledger.State.ProjectName(entry.ProjectId) == "Website"));
    }

    [Fact]
    public void A_topic_is_renamed_in_its_own_row()
    {
        var panel = Opened();
        panel.SelectedProject = Row(panel, "Website");
        var login = panel.Topics.Single(topic => topic.Name == "login");

        Typed(login, "sign-in");
        login.CommitCommand.Execute(null);

        Assert.Equal(["reports", "sign-in"], Names(panel.Topics));
        Assert.Equal("The topic is called 'sign-in' now.", panel.Message);
    }

    [Fact]
    public void A_topic_typing_the_name_of_its_neighbour_merges_the_two()
    {
        var panel = Opened();
        panel.SelectedProject = Row(panel, "Website");
        var login = panel.Topics.Single(topic => topic.Name == "login");

        Typed(login, "Reports");
        Assert.Equal("Enter merges 'login' into 'reports' — its 1 entry · 1h move there.", panel.Proposal);

        login.CommitCommand.Execute(null);

        Assert.Equal(["reports"], Names(panel.Topics));
        Assert.Equal("'login' merged into 'reports'; its 1 entry · 1h moved there.", panel.Message);
    }

    [Fact]
    public void A_topic_can_be_removed_and_its_entries_stay()
    {
        var panel = Opened();
        panel.SelectedProject = Row(panel, "Website");

        panel.Topics.Single(topic => topic.Name == "login").ArchiveCommand.Execute(null);

        Assert.Equal(["reports"], Names(panel.Topics));
        Assert.Equal("'login' is out of Website; its entries stay as they are.", panel.Message);
    }

    [Fact]
    public void Undo_takes_a_merge_back_in_one_step()
    {
        var panel = Opened();
        var mobile = Row(panel, "Mobile");

        Typed(mobile, "Website");
        mobile.CommitCommand.Execute(null);
        panel.UndoCommand.Execute(null);

        Assert.Equal(["Mobile", "Website"], Names(panel.Projects));
        Assert.Equal("1 entry · 1h", Row(panel, "Mobile").Holds);
        Assert.Equal("2 entries · 1h30m", Row(panel, "Website").Holds);
        Assert.Equal("Undone.", panel.Message);
    }

    [Fact]
    public void Undo_with_nothing_behind_it_says_there_is_nothing_to_undo()
    {
        var panel = new ManageViewModel(_ledger);

        panel.UndoCommand.Execute(null);

        Assert.Equal("There is nothing to undo.", panel.Message);
        Assert.False(panel.CanUndo);
    }

    [Fact]
    public void A_rename_made_behind_the_window_reaches_the_panel()
    {
        var panel = Opened();

        Assert.True(new Catalogue(_ledger).RenameProject("Website", "Client portal").Accepted);

        Assert.Equal(["Client portal", "Mobile"], Names(panel.Projects));
    }
}
