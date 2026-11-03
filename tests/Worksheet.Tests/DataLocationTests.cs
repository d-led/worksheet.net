// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>Where the journal is kept, including the one override that makes a portable copy work.</summary>
public sealed class DataLocationTests
{
    [Fact]
    public void Without_an_override_the_journal_lives_in_a_named_folder()
    {
        var directory = LocalLedger.DataDirectory(null);

        Assert.EndsWith("Worksheet", directory);
        Assert.True(Path.IsPathRooted(directory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_override_is_ignored(string overridden)
    {
        Assert.Equal(LocalLedger.DataDirectory(null), LocalLedger.DataDirectory(overridden));
    }

    [Fact]
    public void An_override_is_used_as_given_but_trimmed()
    {
        Assert.Equal(Path.Combine("tmp", "portable"), LocalLedger.DataDirectory("  tmp/portable  "));
    }

    [Fact]
    public void The_default_is_never_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(LocalLedger.DefaultDirectory));
    }

    [Fact]
    public void The_ledger_file_is_created_where_asked()
    {
        using var temp = new TempDirectory("worksheet-location");

        using (var ledger = LocalLedger.Open(temp.Path))
        {
            Assert.True(ledger.Ledger.Version == 0);
        }

        Assert.True(File.Exists(Path.Combine(temp.Path, "ledger.db")));
    }
}
