// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;

namespace Worksheet.Core;

/// <summary>Durations travel as text in the Go shape: <c>3h30m</c>, <c>90m</c>, <c>2h</c>.</summary>
public static partial class Durations
{
    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;

        if (!TryReadUnits(text, out var hours, out var minutes) || (hours == 0 && minutes == 0))
        {
            return false;
        }

        value = new TimeSpan(hours, minutes, 0);
        return true;
    }

    private static bool TryReadUnits(string? text, out int hours, out int minutes)
    {
        hours = 0;
        minutes = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Units().Match(text.Trim());

        if (!match.Success)
        {
            return false;
        }

        hours = match.Groups["h"].Success ? Digits(match.Groups["h"].Value) : 0;
        minutes = match.Groups["m"].Success ? Digits(match.Groups["m"].Value) : 0;

        return true;
    }

    public static string Format(TimeSpan value)
    {
        var sign = value < TimeSpan.Zero ? "-" : string.Empty;
        value = value.Duration();

        var hours = (int)value.TotalHours;
        var minutes = value.Minutes;

        return (hours, minutes) switch
        {
            (> 0, > 0) => $"{sign}{hours}h{minutes}m",
            (> 0, _) => $"{sign}{hours}h",
            _ => $"{sign}{minutes}m",
        };
    }

    private static int Digits(string text) => int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);

    [System.Text.RegularExpressions.GeneratedRegex(
        @"^(?:(?<h>\d+)\s*[hH]\s*)?(?:(?<m>\d+)\s*[mM])?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex Units();
}
