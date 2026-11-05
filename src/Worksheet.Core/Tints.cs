// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;

namespace Worksheet.Core;

/// <summary>
/// A pastel for every name, so a day can be read at a glance: the same project carries the same tint
/// in the window and on the exported page, on every run and on every machine. The recipe is the one
/// the departures page uses — a hash of the name picks a hue, a low saturation and a high lightness —
/// with a hash of this application's own, because the framework's is seeded per process and the
/// colours would then differ between launches.
/// </summary>
public static class Tints
{
    /// <summary>Where a pastel lives: light enough for dark text to sit on, quiet enough to repeat.</summary>
    private const double Saturation = 0.48;

    private const double Lightness = 0.66;

    /// <summary>
    /// How much of the tint survives in a page's row. More than half the row is carried towards
    /// white: on the page the tint fills every cell, while in the window the fields cover most of
    /// their row, so the same tint must be paler here to stay a background rather than a paint.
    /// </summary>
    private const double WashShare = 0.55;

    /// <summary>The warm a break's row is washed in, which is the theme's own value in its light half.</summary>
    public const string BreakWash = "#fceedf";

    /// <summary>
    /// The tint of a name as <c>#rrggbb</c>, or nothing for a break: a pause is the absence of work,
    /// and a colour of its own would make it look like a project.
    /// </summary>
    public static string? Of(string? name) =>
        name is null || Break.IsBreak(name) ? null : Hex(Hue(name.Trim()), Saturation, Lightness);

    /// <summary>
    /// The wash a row of a page is painted with: the project's tint laid over white, or the warm a
    /// break has instead. The window reaches the same colour by drawing the tint over whatever
    /// surface its theme chose, which is why the share is a number and not a second palette.
    /// </summary>
    public static string Wash(string name) =>
        Of(name) is { } tint ? Blend(tint, WashShare) : BreakWash;

    /// <summary>The hue a name falls on, as a fraction of the circle, so two projects rarely share one.</summary>
    private static double Hue(string name) => StableHash(name) % 360 / 360.0;

    /// <summary>
    /// FNV-1a over the name: short, spread well enough for a hue, and the same number on every run
    /// and every machine, which is the whole point of it.
    /// </summary>
    private static ulong StableHash(string name) =>
        name.Aggregate(14695981039346656037UL, (hash, character) => (hash ^ character) * 1099511628211UL);

    /// <summary>Hue, saturation and lightness — each between 0 and 1 — as a colour.</summary>
    private static string Hex(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var second = chroma * (1 - Math.Abs(((hue * 6) % 2) - 1));
        var floor = lightness - (chroma / 2);

        var (red, green, blue) = Wheel(hue, chroma, second);

        return $"#{Channel(red, floor)}{Channel(green, floor)}{Channel(blue, floor)}";
    }

    /// <summary>Which of the six sectors of the wheel the hue is in, and the three channels it implies.</summary>
    private static (double Red, double Green, double Blue) Wheel(double hue, double chroma, double second) =>
        hue switch
        {
            < 1 / 6d => (chroma, second, 0d),
            < 2 / 6d => (second, chroma, 0d),
            < 3 / 6d => (0d, chroma, second),
            < 4 / 6d => (0d, second, chroma),
            < 5 / 6d => (second, 0d, chroma),
            _ => (chroma, 0d, second),
        };

    /// <summary>One channel as two hex digits.</summary>
    private static string Channel(double value, double floor) =>
        Math.Clamp((int)Math.Round((value + floor) * 255), 0, 255).ToString("x2", CultureInfo.InvariantCulture);

    /// <summary>A tint mixed into white by <paramref name="share"/>, so text keeps its contrast.</summary>
    private static string Blend(string tint, double share) =>
        $"#{Washed(tint[1..3], share)}{Washed(tint[3..5], share)}{Washed(tint[5..7], share)}";

    /// <summary>One channel of a colour, mixed into white and written as two hex digits.</summary>
    private static string Washed(string channel, double share) =>
        Math.Clamp((int)Math.Round(Mixed(channel, share)), 0, 255).ToString("x2", CultureInfo.InvariantCulture);

    /// <summary>One channel's value, carried that share of the way to white.</summary>
    private static double Mixed(string channel, double share)
    {
        var value = Convert.ToInt32(channel, 16);

        return value + ((255 - value) * share);
    }
}
