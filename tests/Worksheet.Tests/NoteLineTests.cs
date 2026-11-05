// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>
/// A line that is only a note. It is read before the entry parser is asked, so a line opening with
/// the mark never becomes an entry, and a line without one is never a note.
/// </summary>
public sealed class NoteLineTests
{
    [Fact]
    public void A_line_that_opens_with_the_mark_is_the_words_after_it()
    {
        var note = NoteLine.Parse("# the LiteDB paper");

        Assert.True(note!.Value.Succeeded);
        Assert.Equal("the LiteDB paper", note.Value.Note);
    }

    [Fact]
    public void The_mark_is_taken_without_the_padding_around_the_words()
    {
        Assert.Equal("the paper", NoteLine.Parse("  #   the paper  ")!.Value.Note);
    }

    [Fact]
    public void A_bare_mark_asks_for_the_note()
    {
        var note = NoteLine.Parse("#");

        Assert.False(note!.Value.Succeeded);
        Assert.Equal(NoteLine.MissingNoteMessage, note.Value.Error);
    }

    [Fact]
    public void A_mark_the_ledger_reads_as_part_of_a_name_is_not_the_start_of_a_note()
    {
        Assert.Null(NoteLine.Parse("C# course 09:00 1h"));
        Assert.Null(NoteLine.Parse(string.Empty));
        Assert.Null(NoteLine.Parse(null));
    }
}
