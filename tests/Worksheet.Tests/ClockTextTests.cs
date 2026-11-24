// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

public sealed class ClockTextTests
{
    [Theory]
    [InlineData("24:00")]
    [InlineData("25:00")]
    [InlineData("24:30pm")]
    public void Refuses_a_clock_the_day_cannot_show(string written) =>
        Assert.False(ClockText.TryParse(written, out _));

    [Theory]
    [InlineData("0:00", 0, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData("12:0", 12, 0)]
    [InlineData("9:05pm", 21, 5)]
    public void Reads_a_clock_as_the_minute_it_names(string written, int hour, int minute)
    {
        Assert.True(ClockText.TryParse(written, out var time));

        Assert.Equal(new TimeOnly(hour, minute), time);
    }
}
