// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>
/// How a name that points at nothing is put into words. A prefix that fits several names is the
/// case worth naming separately: picking one of them by accident is the mistake to avoid.
/// </summary>
public static class Names
{
    public static string? Refusal<T>(NameMatch<T> match, string asked, string what)
        where T : class =>
        match switch
        {
            { Ambiguous: true } => $"'{asked.Trim()}' fits more than one {what}; write more of the name.",
            { Found: false } => $"There is no {what} called '{asked.Trim()}'.",
            _ => null,
        };

    /// <summary>A name as it is written down: what a person meant, without the padding.</summary>
    public static string Trimmed(string? name) => (name ?? string.Empty).Trim();
}
