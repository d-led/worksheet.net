// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>
/// The pastel a name carries: the same one on every run and in every window, light enough for dark
/// text to sit on, and none at all for a break — a pause is not a project.
/// </summary>
public sealed class TintsTests
{
    [Fact]
    public void The_same_name_always_gets_the_same_tint()
    {
        Assert.Equal(Tints.Of("Website"), Tints.Of("Website"));
        Assert.Equal(Tints.Of("Website"), Tints.Of("  Website "));
    }

    [Fact]
    public void Two_names_get_two_tints() => Assert.NotEqual(Tints.Of("Website"), Tints.Of("Mobile app"));

    [Fact]
    public void A_tint_does_not_change_between_runs() => Assert.Equal("#bed27f", Tints.Of("Website"));

    [Fact]
    public void Whatever_the_name_the_tint_is_a_light_colour()
    {
        var names = Enumerable.Range(1, 24).Select(index => $"project {index}");

        Assert.All(names, name =>
        {
            var tint = Tints.Of(name)!;

            Assert.Matches("^#[0-9a-f]{6}$", tint);
            Assert.All(Channels(tint), channel => Assert.InRange(channel, 110, 255));
        });
    }

    [Fact]
    public void A_wash_is_the_projects_tint_carried_towards_white()
    {
        var tint = Channels(Tints.Of("Website"));
        var wash = Channels(Tints.Wash("Website"));

        Assert.All(tint.Zip(wash), pair => Assert.True(pair.Second > pair.First, $"a wash is paler: {pair}"));
        Assert.Equal(Tints.Wash("Website"), Tints.Wash("Website"));
    }

    [Fact]
    public void A_break_is_washed_warm_instead() => Assert.Equal("#fceedf", Tints.Wash(Break.Project));

    [Fact]
    public void A_break_has_no_tint_of_its_own()
    {
        Assert.Null(Tints.Of(Break.Project));
        Assert.Null(Tints.Of("break"));
        Assert.Null(Tints.Of(null));
    }

    /// <summary>A tint as the three numbers it is made of; nothing to read when it has none.</summary>
    private static IEnumerable<int> Channels(string? tint) =>
        tint is null ? [] : new[] { tint[1..3], tint[3..5], tint[5..7] }.Select(pair => Convert.ToInt32(pair, 16));
}
