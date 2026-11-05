// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;

namespace Worksheet.Core;

/// <summary>
/// How this application writes a date and orders a name, in one place. The interface is English, so a
/// date is worded in English whatever language the machine is set to — a weekday in German beside
/// the word "Today" is what a machine's own culture buys — and day first, because a journal is read
/// as a sequence of days. Pinning it here also makes two machines produce the same report.
/// </summary>
public static class Dates
{
    /// <summary>English, day first: "Thursday 5 November", "5 Nov 2026".</summary>
    public static CultureInfo English { get; } = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>Names in that alphabet, so two projects never swap places between machines.</summary>
    public static StringComparer Names { get; } = StringComparer.Create(English, ignoreCase: true);
}
