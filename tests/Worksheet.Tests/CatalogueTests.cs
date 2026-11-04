// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>
/// The catalogue as a person meets it: renaming a project or a topic, taking one out of use, and
/// saying where the hours go when a project is dropped. Each decision is one journal change, so
/// each decision is one undo.
/// </summary>
public sealed class CatalogueTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);
    private static readonly Guid Website = Guid.NewGuid();
    private static readonly Guid Mobile = Guid.NewGuid();
    private static readonly Guid Login = Guid.NewGuid();

    private sealed record OpenedLedger(Ledger Ledger, Catalogue Catalogue, Entry Login, Entry Plain);

    /// <summary>Website with the topic login and two entries, beside Mobile; both in use.</summary>
    private static OpenedLedger Opened()
    {
        var ledger = Core.Ledger.Open(new InMemoryJournal());

        ledger.Commit(
            [
                new ProjectOpened(Website, "Website"),
                new ProjectOpened(Mobile, "Mobile"),
                new TopicAdded(Login, Website, "login"),
            ],
            ChangeOrigin.User);

        var login = EntryAt(Website, Login, "09:00");
        var plain = EntryAt(Website, null, "10:00");
        ledger.Commit([new EntryLogged(login), new EntryLogged(plain)]);

        return new OpenedLedger(ledger, new Catalogue(ledger), login, plain);
    }

    private static Entry EntryAt(Guid projectId, Guid? topicId, string start) =>
        new()
        {
            Id = EntryId.Create(),
            ProjectId = projectId,
            TopicId = topicId,
            Date = Day,
            Start = TimeOnly.Parse(start),
            Duration = TimeSpan.FromHours(1),
        };

    private static void Open(Ledger ledger, string project) =>
        ledger.Commit(new ProjectOpened(Guid.NewGuid(), project));

    [Fact]
    public void Renaming_a_project_keeps_its_entries_and_answers_to_the_new_name()
    {
        var opened = Opened();

        Assert.True(opened.Catalogue.RenameProject("website", "Client portal").Accepted);

        Assert.Equal("Client portal", opened.Ledger.State.ProjectName(Website));
        Assert.Equal(Website, opened.Ledger.State.Entries[opened.Login.Id].ProjectId);
    }

    [Fact]
    public void A_project_cannot_be_renamed_to_a_name_another_project_answers_to()
    {
        var opened = Opened();
        var before = opened.Ledger.Version;

        var refusal = opened.Catalogue.RenameProject("Website", " mobile ");

        Assert.Equal("There is already a project called 'mobile'.", Assert.Single(refusal.Problems));
        Assert.Equal(before, opened.Ledger.Version);
    }

    [Fact]
    public void A_project_needs_a_name_to_be_renamed_to()
    {
        var opened = Opened();

        var refusal = opened.Catalogue.RenameProject("Website", "   ");

        Assert.Equal("A project needs a name.", Assert.Single(refusal.Problems));
    }

    [Fact]
    public void A_rename_that_only_changes_the_case_is_not_written()
    {
        var opened = Opened();
        var before = opened.Ledger.Version;

        Assert.True(opened.Catalogue.RenameProject("Website", " website ").Accepted);

        Assert.Equal(before, opened.Ledger.Version);
    }

    [Fact]
    public void A_prefix_that_fits_two_projects_is_refused()
    {
        var opened = Opened();
        Open(opened.Ledger, "Website archive");

        var refusal = opened.Catalogue.RenameProject("Web", "Nothing");

        Assert.Equal("'Web' fits more than one project; write more of the name.", Assert.Single(refusal.Problems));
    }

    [Fact]
    public void Renaming_a_project_is_one_undo()
    {
        var opened = Opened();
        opened.Catalogue.RenameProject("Website", "Client portal");

        Assert.True(opened.Ledger.TryUndo(out var reason), reason);

        Assert.Equal("Website", opened.Ledger.State.ProjectName(Website));
    }

    [Fact]
    public void Removing_a_project_leaves_its_entries_and_topics_exactly_as_they_were()
    {
        var opened = Opened();

        Assert.True(opened.Catalogue.RemoveProject("Website").Accepted);

        Assert.True(opened.Ledger.State.Projects[Website].Archived);
        Assert.True(opened.Ledger.State.Topics[Login].Archived);
        Assert.Equal(Website, opened.Ledger.State.Entries[opened.Login.Id].ProjectId);
        Assert.Equal(Login, opened.Ledger.State.Entries[opened.Login.Id].TopicId);
    }

    [Fact]
    public void Removing_a_project_and_undoing_it_puts_everything_back_in_one_step()
    {
        var opened = Opened();
        opened.Catalogue.RemoveProject("Website");

        Assert.True(opened.Ledger.TryUndo(out var reason), reason);

        Assert.False(opened.Ledger.State.Projects[Website].Archived);
        Assert.False(opened.Ledger.State.Topics[Login].Archived);
        Assert.Equal(Website, opened.Ledger.State.Entries[opened.Login.Id].ProjectId);
        Assert.Equal(Login, opened.Ledger.State.Entries[opened.Login.Id].TopicId);
    }

    [Fact]
    public void A_project_without_entries_is_removed_as_readily_as_one_with_them()
    {
        var opened = Opened();
        Open(opened.Ledger, "Reading");
        var reading = opened.Ledger.State.Projects.Values.Single(project => project.Name == "Reading");

        Assert.True(opened.Catalogue.RemoveProject("Reading").Accepted);

        Assert.True(opened.Ledger.State.Projects[reading.Id].Archived);
        Assert.Equal(Website, opened.Ledger.State.Entries[opened.Login.Id].ProjectId);
    }

    [Fact]
    public void A_project_already_out_of_the_catalogue_is_not_removed_again()
    {
        var opened = Opened();
        opened.Catalogue.RemoveProject("Website");

        var refusal = opened.Catalogue.RemoveProject("Website");

        Assert.Equal("'Website' is already out of the catalogue.", Assert.Single(refusal.Problems));
    }

    [Fact]
    public void An_unknown_project_is_refused_by_name()
    {
        var opened = Opened();

        var refusal = opened.Catalogue.RenameProject("Nowhere", "Somewhere");

        Assert.Equal("There is no project called 'Nowhere'.", Assert.Single(refusal.Problems));
    }

    [Fact]
    public void Renaming_a_topic_keeps_its_entries_and_can_be_undone()
    {
        var opened = Opened();

        Assert.True(opened.Catalogue.RenameTopic("Website", "login", "sign-in").Accepted);

        Assert.Equal("sign-in", opened.Ledger.State.TopicName(Login));
        Assert.Equal(Login, opened.Ledger.State.Entries[opened.Login.Id].TopicId);

        Assert.True(opened.Ledger.TryUndo(out var reason), reason);
        Assert.Equal("login", opened.Ledger.State.TopicName(Login));
    }

    [Fact]
    public void A_topic_cannot_be_renamed_to_another_topic_of_the_same_project()
    {
        var opened = Opened();
        opened.Ledger.Commit(new TopicAdded(Guid.NewGuid(), Website, "logout"));

        var refusal = opened.Catalogue.RenameTopic("Website", "login", "Logout");

        Assert.Equal("There is already a topic in Website called 'Logout'.", Assert.Single(refusal.Problems));
    }

    [Fact]
    public void A_topic_another_project_answers_to_can_be_taken_in_this_one()
    {
        var opened = Opened();
        opened.Ledger.Commit(new TopicAdded(Guid.NewGuid(), Mobile, "login"));

        Assert.True(opened.Catalogue.RenameTopic("Website", "login", "Login attempt").Accepted);
        Assert.Equal("Login attempt", opened.Ledger.State.TopicName(Login));
    }

    [Fact]
    public void Removing_a_topic_leaves_its_entries_with_the_topic_they_had()
    {
        var opened = Opened();

        Assert.True(opened.Catalogue.RemoveTopic("Website", "login").Accepted);

        Assert.True(opened.Ledger.State.Topics[Login].Archived);
        Assert.Equal(Login, opened.Ledger.State.Entries[opened.Login.Id].TopicId);
        Assert.Equal(Website, opened.Ledger.State.Entries[opened.Login.Id].ProjectId);
    }

    [Fact]
    public void A_topic_already_out_of_the_catalogue_is_not_removed_again()
    {
        var opened = Opened();
        opened.Catalogue.RemoveTopic("Website", "login");

        var refusal = opened.Catalogue.RemoveTopic("Website", "login");

        Assert.Equal("'login' is already out of the catalogue.", Assert.Single(refusal.Problems));
    }
}
