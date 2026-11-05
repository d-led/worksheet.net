// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;

namespace Worksheet.Core;

/// <summary>
/// A pastel for every project, so a day can be read at a glance: the same project carries the same
/// tint in the window and on the exported page, on every run and on every machine. The recipe is the
/// one the departures page uses — a hue from the name, a low saturation and a high lightness.
///
/// The hue is not hashed from the name. A hash has no spread to offer: it is free to hand two names
/// hues eighteen degrees apart, which is what "General" and "wiki" got, and pastels that close are
/// one colour to the eye. Walking the wheel by the golden angle in catalogue order instead keeps
/// every pair of names as far apart as their number allows, and the walk repeats exactly because
/// the catalogue it walks is the same one.
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

    /// <summary>
    /// A turn of the wheel by the golden angle — 137.508°, as a share of the circle. No whole count
    /// divides it, so the walk keeps every count of names spread: two names land most of the wheel
    /// apart, twelve are never closer than twenty degrees, and no pair is arbitrarily jammed.
    /// </summary>
    private const double GoldenStep = 0.3819660112501051;

    /// <summary>The warm a break's row is washed in, which is the theme's own value in its light half.</summary>
    public const string BreakWash = "#fceedf";

    /// <summary>
    /// The tint of a name as <c>#rrggbb</c>, or nothing for a break: a pause is the absence of work,
    /// and a colour of its own would make it look like a project. The catalogue is every project the
    /// ledger knows, archived ones included; a name it does not hold yet takes the tint it will have
    /// once it is written down.
    /// </summary>
    public static string? Of(IReadOnlyCollection<string> catalogue, string? name) =>
        name is null || Break.IsBreak(name) ? null : Hex(Hue(catalogue, name.Trim()), Saturation, Lightness);

    /// <summary>
    /// The wash a row of a page is painted with: the project's tint laid over white, or the warm a
    /// break has instead. The window reaches the same colour by drawing the tint over whatever
    /// surface its theme chose, which is why the share is a number and not a second palette.
    /// </summary>
    public static string Wash(IReadOnlyCollection<string> catalogue, string name) =>
        Of(catalogue, name) is { } tint ? Blend(tint, WashShare) : BreakWash;

    /// <summary>
    /// The hue a name falls on, as a share of the circle: one golden step for every project that
    /// sorts before it, so the hues of a catalogue are as far apart as the catalogue's size allows.
    /// A name's place among the other names is its colour, and a name not written down yet takes the
    /// place it would take — the very one it keeps once it is.
    /// </summary>
    private static double Hue(IReadOnlyCollection<string> catalogue, string name) =>
        Rank(catalogue, name) * GoldenStep % 1;

    /// <summary>How many of the catalogue's names sort before this one, listed the way the window lists them.</summary>
    private static int Rank(IReadOnlyCollection<string> catalogue, string name) =>
        catalogue.Count(other => Dates.Names.Compare(other, name) < 0);

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
