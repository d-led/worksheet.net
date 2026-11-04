// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.Tests;

/// <summary>
/// The audit's free-text filter: the words are the query, and every one of them has to be somewhere
/// in the change — its kind, its origin, its version, its time, or the stored body.
/// </summary>
public sealed class BlobFilterTests
{
    private static readonly JournalBlob Login = new(
        7,
        "entry.logged",
        """{"Entry":{"ProjectId":"a1","Note":"first light"}}""",
        new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero),
        ChangeOrigin.User);

    private static readonly JournalBlob Merge = new(
        8,
        "batch.changes",
        """[{"Kind":"project.removed"},{"Kind":"topic.moved"}]""",
        new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero),
        ChangeOrigin.User);

    private static readonly JournalBlob Undone = new(
        9,
        "topic.moved",
        """{"TopicId":"b2","FromProjectId":"a1","ToProjectId":"c3"}""",
        new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.Zero),
        ChangeOrigin.Undo);

    private static JournalBlob[] All() => [Login, Merge, Undone];

    private static long[] Kept(string? filter) =>
        [.. BlobFilter.Apply(All(), filter).Select(blob => blob.Version)];

    [Fact]
    public void An_empty_filter_keeps_everything()
    {
        Assert.Equal([7L, 8L, 9L], Kept(null));
        Assert.Equal([7L, 8L, 9L], Kept("   "));
    }

    [Fact]
    public void A_word_is_looked_for_in_the_body_as_it_was_written()
    {
        Assert.Equal([7L], Kept("first light"));
    }

    [Fact]
    public void Every_word_of_the_filter_has_to_be_there()
    {
        Assert.Empty(Kept("first moved"));
    }

    [Fact]
    public void The_words_may_land_in_different_parts_of_a_change()
    {
        Assert.Equal([8L], Kept("batch move"));
    }

    [Fact]
    public void The_origin_is_searchable()
    {
        Assert.Equal([9L], Kept("undo"));
    }

    [Fact]
    public void A_version_narrows_the_list_together_with_a_kind()
    {
        Assert.Equal([8L], Kept("8 batch"));
    }

    [Fact]
    public void Case_is_not_a_difference_worth_making()
    {
        Assert.Equal([7L], Kept("FIRST LIGHT"));
        Assert.Equal([7L], Kept("entry.LOGGED"));
    }

    [Fact]
    public void The_pieces_of_a_filter_are_what_survives_a_run_of_spaces()
    {
        Assert.Equal(["first", "light"], BlobFilter.Tokens("  first   light  "));
        Assert.Empty(BlobFilter.Tokens("   "));
    }
}
