// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

public sealed class DurationsTests
{
    [Theory]
    [InlineData("3h30m", 210)]
    [InlineData("90m", 90)]
    [InlineData("2h", 120)]
    [InlineData("1H15M", 75)]
    [InlineData("3h 30m", 210)]
    [InlineData("1h0m", 60)]
    [InlineData("0h30m", 30)]
    [InlineData("  45m  ", 45)]
    public void Reads_the_go_duration_shape(string text, int minutes)
    {
        var parsed = Durations.TryParse(text, out var value);

        Assert.True(parsed);
        Assert.Equal(TimeSpan.FromMinutes(minutes), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0m")]
    [InlineData("3h 30")]
    [InlineData("3x")]
    [InlineData("h")]
    public void Refuses_anything_else(string text)
    {
        Assert.False(Durations.TryParse(text, out _));
    }

    [Theory]
    [InlineData(210, "3h30m")]
    [InlineData(120, "2h")]
    [InlineData(45, "45m")]
    [InlineData(0, "0m")]
    [InlineData(-45, "-45m")]
    public void Writes_the_same_shape_back(int minutes, string expected)
    {
        Assert.Equal(expected, Durations.Format(TimeSpan.FromMinutes(minutes)));
    }
}
