// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;

namespace Worksheet.Core;

/// <summary>
/// One clock reading: <c>9:05</c>, <c>09:05</c>, <c>9:05pm</c>. The framework does the parsing, so
/// the rules about what a time is (and is not) are the framework's, not ours.
/// </summary>
public static class ClockText
{
    private static readonly string[] Shapes = ["H:mm", "HH:mm", "h:mmtt", "hh:mmtt"];

    public static bool TryParse(string? text, out TimeOnly time)
    {
        time = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return TimeOnly.TryParseExact(
            UpperDesignator(text.Trim()),
            Shapes,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
    }

    /// <summary>A clock as the entry language writes it.</summary>
    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The designator is matched in either case, so normalise it and let the format decide.</summary>
    private static string UpperDesignator(string text) =>
        text.EndsWith("am", StringComparison.OrdinalIgnoreCase) || text.EndsWith("pm", StringComparison.OrdinalIgnoreCase)
            ? text[..^2] + text[^2..].ToUpperInvariant()
            : text;
}
