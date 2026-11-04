// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>
/// What a half-written line is offered, and what taking an offer does to it: projects first, their
/// own topics once the bracket is open, and the bracket that comes next written in already.
/// </summary>
public sealed class EntryCompletionTests
{
    private static readonly Guid Website = Guid.NewGuid();
    private static readonly Guid Portal = Guid.NewGuid();
    private static readonly Guid Private = Guid.NewGuid();
    private static readonly Guid Login = Guid.NewGuid();
    private static readonly Guid Reports = Guid.NewGuid();
    private static readonly Guid Checkout = Guid.NewGuid();

    private static LedgerState Ledger()
    {
        var state = LedgerState.Create();

        state.Apply(new ProjectOpened(Website, "Website"));
        state.Apply(new ProjectOpened(Portal, "Client portal"));
        state.Apply(new ProjectOpened(Private, "Private"));
        state.Apply(new TopicAdded(Login, Website, "login"));
        state.Apply(new TopicAdded(Reports, Website, "reports"));
        state.Apply(new TopicAdded(Checkout, Portal, "checkout"));

        return state;
    }

    private static IReadOnlyList<string> Names(string line) => EntryCompletion.Proposals(Ledger(), line).Names;

    [Fact]
    public void An_empty_line_offers_nothing_yet()
    {
        var proposals = EntryCompletion.Proposals(Ledger(), string.Empty);

        Assert.Equal(EntryStep.Project, proposals.Field);
        Assert.Empty(proposals.Names);
    }

    [Fact]
    public void A_project_is_offered_by_its_prefix_in_any_case() =>
        Assert.Equal(new[] { "Website" }, Names("web"));

    [Fact]
    public void A_project_already_written_in_full_is_not_offered_again() =>
        Assert.Empty(Names("Website"));

    [Fact]
    public void Once_the_bracket_is_open_the_topics_of_that_project_are_offered()
    {
        var proposals = EntryCompletion.Proposals(Ledger(), "Website (");

        Assert.Equal(EntryStep.Topic, proposals.Field);
        Assert.Equal(new[] { "login", "reports" }, proposals.Names);
    }

    [Fact]
    public void A_topic_offered_inside_another_project_is_never_reached_from_this_one() =>
        Assert.Empty(Names("Website (ch"));

    [Fact]
    public void A_topic_of_a_project_is_offered_through_its_own_bracket() =>
        Assert.Equal(new[] { "checkout" }, Names("Client (ch"));

    [Fact]
    public void A_closed_topic_leaves_nothing_to_offer_and_the_time_comes_next()
    {
        var proposals = EntryCompletion.Proposals(Ledger(), "Website (login) ");

        Assert.Equal(EntryStep.Start, proposals.Field);
        Assert.Empty(proposals.Names);
    }

    [Fact]
    public void Taking_a_project_opens_its_bracket_because_a_topic_comes_next() =>
        Assert.Equal("Website (", EntryCompletion.Take(Ledger(), "Web", "Website"));

    [Fact]
    public void Taking_a_project_without_topics_leaves_the_line_ready_for_a_time() =>
        Assert.Equal("Private ", EntryCompletion.Take(Ledger(), "Priv", "Private"));

    [Fact]
    public void Taking_a_topic_closes_the_bracket() =>
        Assert.Equal("Website (login) ", EntryCompletion.Take(Ledger(), "Website (", "login"));

    [Fact]
    public void Taking_a_topic_keeps_the_time_already_typed() =>
        Assert.Equal("Website (login) 09:00", EntryCompletion.Take(Ledger(), "Website (lo 09:00", "login"));

    [Fact]
    public void Taking_a_project_keeps_the_time_already_typed_and_opens_no_bracket() =>
        Assert.Equal("Website 09:00 1h", EntryCompletion.Take(Ledger(), "Web 09:00 1h", "Website"));

    [Fact]
    public void A_leading_date_is_kept_when_a_name_is_written() =>
        Assert.Equal("3.11. Website (", EntryCompletion.Take(Ledger(), "3.11. Web", "Website"));

    [Fact]
    public void A_name_taken_over_a_whole_line_replaces_it() =>
        Assert.Equal("Client portal (", EntryCompletion.Take(Ledger(), "port", "Client portal"));

    [Fact]
    public void The_shorthand_that_follows_a_project_is_not_part_of_its_name() =>
        Assert.Equal(new[] { "Website" }, Names("web till"));

    [Fact]
    public void Taking_a_project_keeps_the_shorthand_already_typed() =>
        Assert.Equal("Website till", EntryCompletion.Take(Ledger(), "web till", "Website"));

    [Fact]
    public void A_dash_closing_the_name_does_not_hide_the_offers() =>
        Assert.Equal(new[] { "Website" }, Names("web-"));

    [Fact]
    public void Taking_a_project_over_a_closed_name_opens_its_bracket() =>
        Assert.Equal("Website (", EntryCompletion.Take(Ledger(), "web-", "Website"));
}
