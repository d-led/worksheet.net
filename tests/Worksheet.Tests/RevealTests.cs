// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Desktop;

namespace Worksheet.Tests;

/// <summary>
/// Showing an exported file where it is: each platform is asked in the words it answers to, and the
/// three answers are read here rather than on three machines.
/// </summary>
public sealed class RevealTests
{
    /// <summary>
    /// A file one folder deep, and the folder's name holds no separator, so every host reads the
    /// path the same way: <see cref="Path.GetDirectoryName(string)"/> is the host's, not the asked
    /// platform's, and a test that hard-coded "/somewhere" would fail on Windows.
    /// </summary>
    private static readonly string Exported = Path.Combine("somewhere", "worksheet-2026-11.zip");

    [Fact]
    public void The_mac_asks_the_finder_to_select_the_file()
    {
        var start = Reveal.StartInfoFor(Exported, windows: false, macOS: true);

        Assert.Equal("open", start.FileName);
        Assert.Equal(["-R", Exported], start.ArgumentList);
        Assert.True(start.UseShellExecute);
    }

    [Fact]
    public void Windows_asks_the_explorer_to_select_the_file()
    {
        var start = Reveal.StartInfoFor(Exported, windows: true, macOS: false);

        Assert.Equal("explorer.exe", start.FileName);
        Assert.Equal($"/select,\"{Exported}\"", start.Arguments);
        Assert.True(start.UseShellExecute);
    }

    [Fact]
    public void Linux_opens_the_folder_the_file_is_in()
    {
        var start = Reveal.StartInfoFor(Exported, windows: false, macOS: false);

        Assert.Equal("xdg-open", start.FileName);
        Assert.Equal(["somewhere"], start.ArgumentList);
        Assert.True(start.UseShellExecute);
    }
}
