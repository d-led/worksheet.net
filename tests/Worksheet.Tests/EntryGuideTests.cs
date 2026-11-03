// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

public sealed class EntryGuideTests
{
    [Theory]
    [InlineData("", EntryStep.Project)]
    [InlineData("   ", EntryStep.Project)]
    [InlineData("web", EntryStep.Topic)]
    [InlineData("web (", EntryStep.Topic)]
    [InlineData("web (login)", EntryStep.Start)]
    [InlineData("web (login) 09:00", EntryStep.Span)]
    [InlineData("web 09:00", EntryStep.Span)]
    [InlineData("web 09:00-09:00", EntryStep.Span)]
    [InlineData("web 09:00 1h", EntryStep.Complete)]
    [InlineData("web 09:00-10:00", EntryStep.Complete)]
    [InlineData("web (login) 09:00 1h30m", EntryStep.Complete)]
    public void Says_what_to_write_next(string typed, EntryStep expected)
    {
        Assert.Equal(expected, EntryGuide.Next(typed));
    }

    [Theory]
    [InlineData(EntryStep.Project, "project")]
    [InlineData(EntryStep.Topic, "topic in (brackets), optional")]
    [InlineData(EntryStep.Start, "start, e.g. 09:00")]
    [InlineData(EntryStep.Span, "length 1h30m, or an end 09:00-11:00")]
    [InlineData(EntryStep.Complete, "press Enter")]
    public void Every_step_has_a_sentence(EntryStep step, string expected)
    {
        Assert.Equal(expected, EntryGuide.Label(step));
    }
}
