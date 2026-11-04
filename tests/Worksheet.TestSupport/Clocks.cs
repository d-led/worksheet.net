// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;

namespace Worksheet.TestSupport;

/// <summary>
/// Times and days written in the tests, read and written with no culture at all. A test that only
/// passes where the machine happens to write times a particular way is a test that fails on someone
/// else's machine, and the failure looks like a bug in the application.
/// </summary>
public static class Clocks
{
    public const string TimeFormat = "HH:mm";

    public const string DateFormat = "yyyy-MM-dd";

    public static TimeOnly At(string text) => TimeOnly.Parse(text, CultureInfo.InvariantCulture);

    public static string Text(TimeOnly time) => time.ToString(TimeFormat, CultureInfo.InvariantCulture);

    public static string Text(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
