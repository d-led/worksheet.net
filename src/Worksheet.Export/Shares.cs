// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using Worksheet.Core;

namespace Worksheet.Export;

/// <summary>Hours as the application writes them, and a share of a total as a number and as a sentence.</summary>
internal static class Shares
{
    /// <summary>"3h30m" — hours, and minutes when there are any, which is how a day is spoken of.</summary>
    public static string Hours(int minutes) => Durations.Format(TimeSpan.FromMinutes(minutes));

    /// <summary>A share of the whole, kept as a fraction for the JSON and rounded to a tenth of a percent.</summary>
    public static double Of(int minutes, int total) =>
        total == 0 ? 0 : Math.Round(minutes / (double)total, 4);

    public static string Percent(int minutes, int total) =>
        (Of(minutes, total) * 100).ToString("0.0", CultureInfo.InvariantCulture) + " %";

    /// <summary>A share as a bar's width, which is what a style attribute wants: a dot, a percent sign.</summary>
    public static string Width(int minutes, int total) =>
        (total <= 0 ? 0 : minutes * 100.0 / total).ToString("0.0", CultureInfo.InvariantCulture) + "%";
}
