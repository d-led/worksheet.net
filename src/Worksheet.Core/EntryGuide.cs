// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.RegularExpressions;

namespace Worksheet.Core;

/// <summary>The four things a line is made of, in the order they are written.</summary>
public enum EntryStep
{
    Project,
    Topic,
    Start,
    Span,
    Complete,
}

/// <summary>
/// Where the person is in a half-written line. The window uses it to say what comes next instead
/// of leaving them to guess the grammar.
/// </summary>
public static partial class EntryGuide
{
    private static readonly string[] Labels =
    [
        "project",
        "topic in (brackets), optional",
        "start, e.g. 09:00",
        "length 1h30m, or an end 09:00-11:00",
        "press Enter",
    ];

    public static EntryStep Next(string? text)
    {
        var typed = (text ?? string.Empty).Trim();

        return typed.Length == 0 ? EntryStep.Project : Written(typed) ?? Typed(typed);
    }

    /// <summary>A line the grammar already accepts, or nothing while it is still half-written.</summary>
    private static EntryStep? Written(string typed)
    {
        var parse = EntryCommandParser.Parse(typed, DateOnly.MinValue);

        if (!parse.Succeeded)
        {
            return null;
        }

        // A start with no length is legal, but it is not finished work.
        return parse.Command!.Duration > TimeSpan.Zero ? EntryStep.Complete : EntryStep.Span;
    }

    /// <summary>A half-written line: the brackets around the topic, and the clock that comes after them.</summary>
    private static EntryStep Typed(string typed) =>
        Clock().IsMatch(typed) ? EntryStep.Span
        : typed.Contains('(') && !typed.Contains(')') ? EntryStep.Topic
        : typed.Contains(')') ? EntryStep.Start
        : EntryStep.Topic;

    /// <summary>What to write at this step, for the hint line.</summary>
    public static string Label(EntryStep step) => Labels[(int)step];

    [GeneratedRegex(@"\d{1,2}:\d{1,2}", RegexOptions.CultureInvariant)]
    private static partial Regex Clock();
}
