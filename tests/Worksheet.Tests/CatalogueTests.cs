// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.TestSupport;

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
            Start = Clocks.At(start),
            Duration = TimeSpan.FromHours(1),
        };

    private static void Open(Ledger ledger, string project) =>
        ledger.Commit(new ProjectOpened(Guid.NewGuid(), project));

    private static string[] ProjectNames(Ledger ledger) =>
        [.. ledger.State.Projects.Values
            .Where(project => !project.Archived)
            .Select(project => project.Name)
            .Order(StringComparer.Ordinal)];

    private static string[] TopicNames(Ledger ledger, Guid projectId) =>
        [.. ledger.State.Topics.Values
            .Where(topic => topic.ProjectId == projectId && !topic.Archived)
            .Select(topic => topic.Name)
            .Order(StringComparer.Ordinal)];

    [Fact]
    public void Renaming_a_project_keeps_its_entries_and_answers_to_the_new_name()
    {
        var opened = Opened();

        Assert.True(opened.Catalogue.RenameProject("website", "Client portal").Accepted);

        Assert.Equal("Client portal", opened.Ledger.State.ProjectName(Website));
        Assert.Equal(Website, opened.Ledger.State.Entries[opened.Login.Id].ProjectId);
    }

    [Fact]
    public void Renaming_a_project_onto_a_name_in_use_merges_the_two()
    {
        var opened = Opened();
        var call = EntryAt(Mobile, null, "11:00");
        opened.Ledger.Commit(new EntryLogged(call));

        Assert.True(opened.Catalogue.RenameProject("Mobile", " website ").Accepted);

        Assert.Equal(Website, opened.Ledger.State.Entries[call.Id].ProjectId);
        Assert.True(opened.Ledger.State.Projects[Mobile].Archived);
        Assert.Equal(["Website"], ProjectNames(opened.Ledger));
    }

    [Fact]
    public void A_merge_carries_the_topics_of_both_projects_and_joins_the_ones_that_share_a_name()
    {
        var opened = Opened();
        var calls = Guid.NewGuid();
        var theirLogin = Guid.NewGuid();
        var theirEntry = EntryAt(Mobile, theirLogin, "11:00");
        var callsEntry = EntryAt(Mobile, calls, "12:00");
        opened.Ledger.Commit(
            [
                new TopicAdded(calls, Mobile, "calls"),
                new TopicAdded(theirLogin, Mobile, "login"),
                new EntryLogged(callsEntry),
                new EntryLogged(theirEntry),
            ]);

        Assert.True(opened.Catalogue.RenameProject("Mobile", "Website").Accepted);

        Assert.Equal(Website, opened.Ledger.State.Topics[calls].ProjectId);
        Assert.Equal(calls, opened.Ledger.State.Entries[callsEntry.Id].TopicId);
        Assert.Equal(Login, opened.Ledger.State.Entries[theirEntry.Id].TopicId);
        Assert.True(opened.Ledger.State.Topics[theirLogin].Archived);
        Assert.Equal(["calls", "login"], TopicNames(opened.Ledger, Website));
    }

    [Fact]
    public void A_merge_is_one_undo()
    {
        var opened = Opened();
        var call = EntryAt(Mobile, null, "11:00");
        opened.Ledger.Commit(new EntryLogged(call));
        var before = opened.Ledger.Version;

        opened.Catalogue.RenameProject("Mobile", "Website");

        Assert.Equal(before + 1, opened.Ledger.Version);
        Assert.True(opened.Ledger.TryUndo(out var reason), reason);
        Assert.Equal(Mobile, opened.Ledger.State.Entries[call.Id].ProjectId);
        Assert.False(opened.Ledger.State.Projects[Mobile].Archived);
        Assert.Equal(Website, opened.Ledger.State.Entries[opened.Login.Id].ProjectId);
    }

    [Fact]
    public void Merging_into_a_name_that_was_retired_brings_it_back()
    {
        var opened = Opened();
        opened.Catalogue.RemoveProject("Mobile");

        Assert.True(opened.Catalogue.RenameProject("Website", "Mobile").Accepted);

        Assert.False(opened.Ledger.State.Projects[Mobile].Archived);
        Assert.True(opened.Ledger.State.Projects[Website].Archived);
        Assert.Equal(Mobile, opened.Ledger.State.Entries[opened.Login.Id].ProjectId);
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
    public void A_topic_renamed_onto_another_topic_of_the_project_merges_the_two()
    {
        var opened = Opened();
        var logout = Guid.NewGuid();
        opened.Ledger.Commit(new TopicAdded(logout, Website, "logout"));

        Assert.True(opened.Catalogue.RenameTopic("Website", "login", "Logout").Accepted);

        Assert.Equal(logout, opened.Ledger.State.Entries[opened.Login.Id].TopicId);
        Assert.True(opened.Ledger.State.Topics[Login].Archived);
        Assert.Equal(["logout"], TopicNames(opened.Ledger, Website));
    }

    [Fact]
    public void A_merge_brings_back_a_topic_of_the_surviving_project_that_was_out_of_use()
    {
        var opened = Opened();
        var theirLogin = Guid.NewGuid();
        var mobileEntry = EntryAt(Mobile, theirLogin, "11:00");
        opened.Ledger.Commit([new TopicAdded(theirLogin, Mobile, "login"), new EntryLogged(mobileEntry)]);
        opened.Catalogue.RemoveTopic("Website", "login");

        Assert.True(opened.Catalogue.RenameProject("Mobile", "Website").Accepted);

        Assert.False(opened.Ledger.State.Topics[Login].Archived);
        Assert.Equal(Login, opened.Ledger.State.Entries[mobileEntry.Id].TopicId);
        Assert.True(opened.Ledger.State.Topics[theirLogin].Archived);

        Assert.True(opened.Ledger.TryUndo(out var reason), reason);
        Assert.True(opened.Ledger.State.Topics[Login].Archived);
        Assert.Equal(Mobile, opened.Ledger.State.Topics[theirLogin].ProjectId);
        Assert.False(opened.Ledger.State.Topics[theirLogin].Archived);
    }

    [Fact]
    public void A_topic_merge_brings_back_the_topic_of_that_name_that_was_out_of_use()
    {
        var opened = Opened();
        var logout = Guid.NewGuid();
        opened.Ledger.Commit(new TopicAdded(logout, Website, "logout"));
        opened.Catalogue.RemoveTopic("Website", "logout");

        Assert.True(opened.Catalogue.RenameTopic("Website", "login", "Logout").Accepted);

        Assert.False(opened.Ledger.State.Topics[logout].Archived);
        Assert.Equal(logout, opened.Ledger.State.Entries[opened.Login.Id].TopicId);
        Assert.True(opened.Ledger.State.Topics[Login].Archived);
    }

    [Fact]
    public void A_topic_merge_is_one_undo()
    {
        var opened = Opened();
        var logout = Guid.NewGuid();
        opened.Ledger.Commit(new TopicAdded(logout, Website, "logout"));
        var before = opened.Ledger.Version;

        opened.Catalogue.RenameTopic("Website", "login", "Logout");

        Assert.Equal(before + 1, opened.Ledger.Version);
        Assert.True(opened.Ledger.TryUndo(out var reason), reason);
        Assert.Equal(Login, opened.Ledger.State.Entries[opened.Login.Id].TopicId);
        Assert.False(opened.Ledger.State.Topics[Login].Archived);
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
