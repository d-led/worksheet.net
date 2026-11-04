// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.Desktop.ViewModels;
using Worksheet.Storage;

namespace Worksheet.Tests;

/// <summary>
/// The journal read as an audit: every change the file holds, newest first, narrowed by whatever is
/// typed into the filter — and the list follows the journal as an agent writes to it.
/// </summary>
public sealed class AuditViewModelTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private readonly InMemoryJournal _journal = new();
    private readonly Ledger _ledger;
    private readonly EntryComposer _composer;

    public AuditViewModelTests()
    {
        _ledger = Core.Ledger.Open(_journal);
        _composer = new EntryComposer(_ledger);
    }

    private void Logged(string line) =>
        Assert.True(_composer.Compose(EntryCommandParser.Parse(line, Day).Command!).Created, line);

    /// <summary>The audit over this journal, reading it the way the application reads its file.</summary>
    private AuditViewModel Opened() => new(Blobs, _ledger);

    /// <summary>The journal as the file holds it: each change's kind and body as stored text.</summary>
    private IReadOnlyList<JournalBlob> Blobs() =>
    [
        .. _journal.ReadFrom(0).Select(record => new JournalBlob(
            record.Version,
            EventCodec.KindOf(record.Change),
            EventCodec.Encode(record.Change),
            record.RecordedAt,
            record.Origin)),
    ];

    [Fact]
    public void The_list_holds_every_change_the_journal_does_newest_first()
    {
        Logged("Website 09:00 1h");
        Logged("Mobile 10:00 1h");

        var audit = Opened();

        Assert.Equal(4, audit.Rows.Count);
        Assert.Equal(4, audit.Rows[0].Version);
        Assert.Equal("entry.logged", audit.Rows[0].Kind);
        Assert.Equal("4 changes", audit.Count);
    }

    [Fact]
    public void A_row_shows_a_glimpse_of_the_body_it_holds()
    {
        Logged("Website 09:00 1h");

        var audit = Opened();

        var opened = audit.Rows.Single(row => row.Kind == "project.opened");

        Assert.Contains("Website", opened.Glimpse, StringComparison.Ordinal);
        Assert.DoesNotContain(Environment.NewLine, opened.Glimpse);
    }

    [Fact]
    public void The_filter_narrows_the_list_and_says_how_much_it_kept()
    {
        Logged("Website 09:00 1h");
        Logged("Mobile 10:00 1h");
        var audit = Opened();

        audit.Filter = "entry.logged";

        Assert.Equal(2, audit.Rows.Count);
        Assert.Equal("2 of 4 changes", audit.Count);
        Assert.All(audit.Rows, row => Assert.Equal("entry.logged", row.Kind));
    }

    [Fact]
    public void A_filter_finds_a_project_in_the_change_that_opened_it()
    {
        Logged("Website 09:00 1h");
        Logged("Mobile 10:00 1h");
        var audit = Opened();

        audit.Filter = "mobile";

        var row = Assert.Single(audit.Rows);
        Assert.Equal("project.opened", row.Kind);
        Assert.Contains("Mobile", row.Glimpse, StringComparison.Ordinal);
    }

    [Fact]
    public void A_filter_that_holds_nothing_says_so()
    {
        Logged("Website 09:00 1h");
        var audit = Opened();

        audit.Filter = "yesterday";

        Assert.Empty(audit.Rows);
        Assert.False(audit.HasRows);
    }

    [Fact]
    public void Clearing_the_filter_brings_everything_back()
    {
        Logged("Website 09:00 1h");
        var audit = Opened();
        audit.Filter = "website";

        audit.Filter = string.Empty;

        Assert.Equal(2, audit.Rows.Count);
        Assert.Equal("2 changes", audit.Count);
    }

    [Fact]
    public void A_change_written_while_the_audit_is_open_reaches_the_list()
    {
        Logged("Website 09:00 1h");
        var audit = Opened();

        Logged("Mobile 10:00 1h");

        Assert.Equal(4, audit.Rows.Count);
    }

    [Fact]
    public void Opening_a_row_is_whatever_the_shell_made_it()
    {
        Logged("Website 09:00 1h");
        var audit = Opened();
        AuditRow? opened = null;
        audit.Open = row => opened = row;

        audit.Open!.Invoke(audit.Rows[0]);

        Assert.Equal(audit.Rows[0], opened);
    }
}
