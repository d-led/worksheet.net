// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>What a line would do to the day before it is entered.</summary>
public sealed class EntryPreviewTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private static EntryCommand Command(string start, int minutes) =>
        new(Day, "Website", null, Clocks.At(start), TimeSpan.FromMinutes(minutes), null);

    private static Core.Ledger NewLedger() => Core.Ledger.Open(new InMemoryJournal());

    private static void Log(Core.Ledger ledger, string line)
    {
        var parse = EntryCommandParser.Parse(line, Day);
        Assert.True(parse.Succeeded, parse.Error);
        Assert.True(new EntryComposer(ledger).Compose(parse.Command!).Created);
    }

    [Fact]
    public void A_free_span_has_nothing_to_report()
    {
        var ledger = NewLedger();
        Log(ledger, "Website 09:00 1h");

        Assert.Empty(new EntryComposer(ledger).Yielded(Command("11:00", 60)));
    }

    [Fact]
    public void A_crossing_span_says_what_it_will_take_before_being_entered()
    {
        var ledger = NewLedger();
        Log(ledger, "Website (login) 09:00 2h");

        var taking = new EntryComposer(ledger).Yielded(Command("10:00", 30));

        Assert.Equal(
            "splits Website (login) 09:00-11:00 into 09:00-10:00 and 10:30-11:00",
            Assert.Single(taking));
    }

    [Fact]
    public void Saying_what_a_line_takes_writes_nothing()
    {
        var ledger = NewLedger();
        Log(ledger, "Website 09:00 2h");
        var version = ledger.Version;

        new EntryComposer(ledger).Yielded(Command("10:00", 60));

        Assert.Equal(version, ledger.Version);
        Assert.Single(ledger.State.Entries);
    }

    [Fact]
    public void A_span_with_no_length_takes_nothing()
    {
        var ledger = NewLedger();
        Log(ledger, "Website 09:00 2h");

        Assert.Empty(new EntryComposer(ledger).Yielded(Command("10:00", 0)));
    }
}
