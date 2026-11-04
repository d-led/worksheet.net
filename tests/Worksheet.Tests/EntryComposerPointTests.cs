// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>
/// Pointing a row at a project or a topic by name: the name is resolved the way a line resolves
/// it, a name that is new is opened, and a name taken out of the catalogue is not an answer.
/// </summary>
public sealed class EntryComposerPointTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private readonly Ledger _ledger = Core.Ledger.Open(new InMemoryJournal());
    private readonly EntryComposer _composer;

    public EntryComposerPointTests() => _composer = new EntryComposer(_ledger);

    private Entry Logged(string line)
    {
        var parse = EntryCommandParser.Parse(line, Day);

        Assert.True(parse.Succeeded, parse.Error);

        var composed = _composer.Compose(parse.Command!);

        Assert.True(composed.Created, string.Join(" ", composed.Problems));

        return composed.Entry!;
    }

    [Fact]
    public void A_row_pointed_at_a_new_project_opens_it_and_keeps_its_times()
    {
        var entry = Logged("Website 09:00 1h");

        var pointed = _composer.Point(entry, "Client portal", null);

        Assert.True(pointed.Created, string.Join(" ", pointed.Problems));
        Assert.Equal("Client portal", _ledger.State.ProjectName(pointed.Entry!.ProjectId));
        Assert.Equal(entry.Start, pointed.Entry.Start);
        Assert.Equal(entry.Duration, pointed.Entry.Duration);
    }

    [Fact]
    public void A_row_pointed_at_a_topic_of_its_project_keeps_the_project()
    {
        var entry = Logged("Website 09:00 1h");

        var pointed = _composer.Point(entry, "Website", "login");

        Assert.Equal(entry.ProjectId, pointed.Entry!.ProjectId);
        Assert.Equal("login", _ledger.State.TopicName(pointed.Entry.TopicId));
    }

    [Fact]
    public void A_row_pointed_at_what_it_already_says_writes_nothing()
    {
        var entry = Logged("Website (login) 09:00 1h");
        var before = _ledger.Version;

        Assert.True(_composer.Point(entry, "Website", "login").Created);

        Assert.Equal(before, _ledger.Version);
    }

    [Fact]
    public void A_break_takes_no_topic_when_a_row_is_pointed_at_one()
    {
        var entry = Logged("Website 09:00 1h");

        var pointed = _composer.Point(entry, Break.Project, "login");

        Assert.Equal(Break.RefusedTopic, Assert.Single(pointed.Problems));
    }

    [Fact]
    public void A_row_needs_a_project_name()
    {
        var entry = Logged("Website 09:00 1h");

        var pointed = _composer.Point(entry, "  ", null);

        Assert.Equal("An entry needs a project.", Assert.Single(pointed.Problems));
    }

    [Fact]
    public void A_prefix_that_fits_two_projects_points_nowhere()
    {
        Logged("Website 09:00 1h");
        var second = Logged("Website archive 10:00 1h");

        var pointed = _composer.Point(second, "Web", null);

        Assert.Equal("'Web' fits more than one project; write more of the name.", Assert.Single(pointed.Problems));
    }

    [Fact]
    public void A_line_cannot_be_logged_on_a_project_taken_out_of_the_catalogue()
    {
        Logged("Website 09:00 1h");
        Logged("Mobile 10:00 1h");
        new Catalogue(_ledger).RemoveProject("Website", "Mobile");

        var composed = _composer.Compose(EntryCommandParser.Parse("Website 11:00 1h", Day).Command!);

        Assert.Equal("'Website' is already out of the catalogue.", Assert.Single(composed.Problems));
    }

    [Fact]
    public void A_row_cannot_be_pointed_at_a_project_taken_out_of_the_catalogue()
    {
        var entry = Logged("Website 09:00 1h");
        Logged("Mobile 10:00 1h");
        new Catalogue(_ledger).RemoveProject("Website", "Mobile");

        var pointed = _composer.Point(entry, "Website", null);

        Assert.Equal("'Website' is already out of the catalogue.", Assert.Single(pointed.Problems));
    }

    [Fact]
    public void A_row_cannot_be_pointed_at_a_topic_taken_out_of_the_catalogue()
    {
        var entry = Logged("Website (login) 09:00 1h");
        new Catalogue(_ledger).RemoveTopic("Website", "login");

        var pointed = _composer.Point(_ledger.State.Entries[entry.Id], "Website", "login");

        Assert.Equal("'login' is already out of the catalogue.", Assert.Single(pointed.Problems));
    }
}
