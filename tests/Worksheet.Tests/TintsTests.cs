// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>
/// The pastel a project carries: the same one in every window and on every page of the same
/// catalogue, far enough from its neighbours to be told apart, light enough for dark text to sit
/// on, and none at all for a break — a pause is not a project.
/// </summary>
public sealed class TintsTests
{
    [Fact]
    public void The_same_name_always_gets_the_same_tint()
    {
        IReadOnlyCollection<string> catalogue = ["Mobile app", "Website"];

        Assert.Equal(Tints.Of(catalogue, "Website"), Tints.Of(catalogue, "Website"));
        Assert.Equal(Tints.Of(catalogue, "Website"), Tints.Of(catalogue, "  Website "));
    }

    /// <summary>
    /// A project typed for the first time shows the colour it keeps once it is written down, so a
    /// row does not change shade under the writer's hands when the entry is accepted.
    /// </summary>
    [Fact]
    public void A_name_the_catalogue_does_not_hold_yet_takes_the_tint_it_will_have()
    {
        IReadOnlyCollection<string> today = ["General", "wiki"];
        IReadOnlyCollection<string> tomorrow = ["General", "Jira", "wiki"];

        Assert.Equal(Tints.Of(tomorrow, "Jira"), Tints.Of(today, "Jira"));
    }

    /// <summary>
    /// The two projects that were one colour: their hashes fell eighteen degrees apart, a twentieth
    /// of the wheel.
    /// </summary>
    [Fact]
    public void Two_projects_are_never_the_same_colour()
    {
        IReadOnlyCollection<string> catalogue = ["General", "wiki"];

        var apart = Distance(HueOf(Tints.Of(catalogue, "General")), HueOf(Tints.Of(catalogue, "wiki")));

        Assert.True(apart >= 120, $"General and wiki are {apart:F0}° apart");
    }

    /// <summary>
    /// However many projects the journal holds, no two of them are crammed together: the hues are
    /// walked by the golden angle, which keeps the closest pair further apart than a third of an
    /// even share of the wheel.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    public void No_two_projects_of_a_catalogue_are_crammed_together(int count)
    {
        var catalogue = Enumerable.Range(1, count).Select(index => $"project {index}").ToArray();
        var hues = catalogue.Select(name => HueOf(Tints.Of(catalogue, name))).ToArray();
        var closest = hues
            .SelectMany((hue, index) => hues.Skip(index + 1).Select(other => Distance(hue, other)))
            .Min();

        Assert.True(closest >= 360 / (3.0 * count), $"the closest two of {count} colours are {closest:F1}° apart");
    }

    /// <summary>
    /// The colours themselves, one project for each of the six sectors of the wheel, worked out from
    /// the recipe rather than from the code: a tint that changes is a palette the journal has quietly
    /// repainted, and a sector or a rounding that drifts shows up here as one name's colour moving.
    /// </summary>
    [Theory]
    [InlineData(0, "#d27f7f")]
    [InlineData(1, "#7fd297")]
    [InlineData(2, "#af7fd2")]
    [InlineData(3, "#d2c87f")]
    [InlineData(4, "#7fc4d2")]
    [InlineData(5, "#d27fac")]
    public void A_project_takes_the_colour_of_its_place_in_the_catalogue(int place, string expected) =>
        Assert.Equal(expected, Tints.Of(Catalogue(place), "target"));

    [Fact]
    public void Whatever_the_name_the_tint_is_a_light_colour()
    {
        var catalogue = Enumerable.Range(1, 24).Select(index => $"project {index}").ToArray();

        Assert.All(catalogue, name =>
        {
            var tint = Tints.Of(catalogue, name)!;

            Assert.Matches("^#[0-9a-f]{6}$", tint);
            Assert.All(Channels(tint), channel => Assert.InRange(channel, 110, 255));
        });
    }

    [Fact]
    public void A_wash_is_the_projects_tint_carried_towards_white()
    {
        IReadOnlyCollection<string> catalogue = ["Website"];

        var tint = Channels(Tints.Of(catalogue, "Website"));
        var wash = Channels(Tints.Wash(catalogue, "Website"));

        Assert.All(tint.Zip(wash), pair => Assert.True(pair.Second > pair.First, $"a wash is paler: {pair}"));
    }

    [Fact]
    public void A_wash_is_what_it_was() => Assert.Equal("#ebc5c5", Tints.Wash(["Website"], "Website"));

    [Fact]
    public void A_break_is_washed_in_nothing_at_all() => Assert.Equal("transparent", Tints.Wash([], Break.Project));

    [Fact]
    public void A_break_has_no_tint_of_its_own()
    {
        IReadOnlyCollection<string> catalogue = ["Break", "Website"];

        Assert.Null(Tints.Of(catalogue, Break.Project));
        Assert.Null(Tints.Of(catalogue, "break"));
        Assert.Null(Tints.Of(catalogue, null));
    }

    /// <summary>A catalogue of <paramref name="place"/> names, every one of them sorting before "target".</summary>
    private static IReadOnlyCollection<string> Catalogue(int place) =>
        [.. Enumerable.Range(0, place).Select(index => $"p{index}"), "target"];

    /// <summary>A tint as the three numbers it is made of; nothing to read when it has none.</summary>
    private static IEnumerable<int> Channels(string? tint) =>
        tint is null ? [] : new[] { tint[1..3], tint[3..5], tint[5..7] }.Select(pair => Convert.ToInt32(pair, 16));

    /// <summary>Where a pastel sits on the wheel, in degrees, read back out of the colour itself.</summary>
    private static double HueOf(string? tint)
    {
        var channels = Channels(tint).ToArray();
        Assert.Equal(3, channels.Length);

        var (red, green, blue) = (channels[0], channels[1], channels[2]);
        var highest = Math.Max(red, Math.Max(green, blue));
        var span = highest - Math.Min(red, Math.Min(green, blue));

        if (span == 0)
        {
            return 0;
        }

        var sector = red == highest ? (green - blue) / (double)span
            : green == highest ? ((blue - red) / (double)span) + 2
            : ((red - green) / (double)span) + 4;

        return ((sector * 60) + 360) % 360;
    }

    /// <summary>How far apart two hues are, on a wheel that joins at 360.</summary>
    private static double Distance(double one, double other) =>
        Math.Min(Math.Abs(one - other), 360 - Math.Abs(one - other));
}
