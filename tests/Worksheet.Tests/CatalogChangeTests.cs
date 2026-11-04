// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>
/// Taking a project or a topic out of the catalogue, renaming one, and putting it back: the entries
/// move, a project's topics go with it, and undo is one step because the change carries both sides.
/// </summary>
public sealed class CatalogChangeTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);
    private static readonly Guid Website = Guid.NewGuid();
    private static readonly Guid Mobile = Guid.NewGuid();
    private static readonly Guid Login = Guid.NewGuid();

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

    /// <summary>Two projects, one topic, and two entries under Website.</summary>
    private static Ledger Opened(out Entry login, out Entry plain)
    {
        var ledger = Core.Ledger.Open(new InMemoryJournal());

        ledger.Commit(
            [
                new ProjectOpened(Website, "Website"),
                new ProjectOpened(Mobile, "Mobile"),
                new TopicAdded(Login, Website, "login"),
            ],
            ChangeOrigin.User);

        login = EntryAt(Website, Login, "09:00");
        plain = EntryAt(Website, null, "10:00");
        ledger.Commit([new EntryLogged(login), new EntryLogged(plain)]);

        return ledger;
    }

    private static void Remove(Core.Ledger ledger, Entry[] before, Guid into)
    {
        var project = ledger.State.Projects[Website];
        var topics = ledger.State.Topics.Values.Where(topic => topic.ProjectId == Website).ToArray();
        var after = before.Select(entry => entry with { ProjectId = into, TopicId = null }).ToArray();

        ledger.Commit([new ProjectRemoved(project, topics, before, after)]);
    }

    [Fact]
    public void Removing_a_project_sends_its_entries_somewhere_and_archives_it_with_its_topics()
    {
        var ledger = Opened(out var login, out var plain);

        Remove(ledger, [login, plain], Mobile);

        Assert.True(ledger.State.Projects[Website].Archived);
        Assert.True(ledger.State.Topics[Login].Archived);
        Assert.Equal(Mobile, ledger.State.Entries[login.Id].ProjectId);
        Assert.Equal(Mobile, ledger.State.Entries[plain.Id].ProjectId);
        Assert.Null(ledger.State.Entries[login.Id].TopicId);
    }

    [Fact]
    public void Undo_puts_a_removed_project_topics_and_entries_back_in_one_step()
    {
        var ledger = Opened(out var login, out var plain);
        Remove(ledger, [login, plain], Mobile);

        Assert.True(ledger.TryUndo(out var reason), reason);

        Assert.False(ledger.State.Projects[Website].Archived);
        Assert.False(ledger.State.Topics[Login].Archived);
        Assert.Equal(Website, ledger.State.Entries[login.Id].ProjectId);
        Assert.Equal(Login, ledger.State.Entries[login.Id].TopicId);
    }

    [Fact]
    public void Redo_takes_the_project_away_again()
    {
        var ledger = Opened(out var login, out var plain);
        Remove(ledger, [login, plain], Mobile);
        ledger.TryUndo(out _);

        Assert.True(ledger.TryRedo(out var reason), reason);

        Assert.True(ledger.State.Projects[Website].Archived);
        Assert.Equal(Mobile, ledger.State.Entries[login.Id].ProjectId);
    }

    [Fact]
    public void Removing_a_topic_leaves_its_entries_without_one_and_undo_restores_it()
    {
        var ledger = Opened(out var login, out _);
        var topic = ledger.State.Topics[Login];
        var entry = ledger.State.Entries[login.Id];

        ledger.Commit([new TopicRemoved(topic, [entry], [entry with { TopicId = null }])]);

        Assert.True(ledger.State.Topics[Login].Archived);
        Assert.Null(ledger.State.Entries[login.Id].TopicId);

        Assert.True(ledger.TryUndo(out var reason), reason);

        Assert.False(ledger.State.Topics[Login].Archived);
        Assert.Equal(Login, ledger.State.Entries[login.Id].TopicId);
    }

    [Fact]
    public void A_project_rename_keeps_the_identity_and_undo_takes_the_name_back()
    {
        var ledger = Opened(out _, out _);

        ledger.Commit([new ProjectRenamed(Website, "Website", "Client portal")]);

        Assert.Equal("Client portal", ledger.State.ProjectName(Website));
        Assert.Equal(2, ledger.State.Entries.Count);

        Assert.True(ledger.TryUndo(out var reason), reason);
        Assert.Equal("Website", ledger.State.ProjectName(Website));
    }

    [Fact]
    public void A_topic_rename_keeps_the_topic_and_undo_takes_the_name_back()
    {
        var ledger = Opened(out var login, out _);

        ledger.Commit([new TopicRenamed(Login, "login", "sign-in")]);

        Assert.Equal("sign-in", ledger.State.TopicName(Login));
        Assert.Equal(Login, ledger.State.Entries[login.Id].TopicId);

        Assert.True(ledger.TryUndo(out var reason), reason);
        Assert.Equal("login", ledger.State.TopicName(Login));
    }
}
