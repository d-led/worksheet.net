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
    /// <summary>The span the hints put in their examples; the one a shorthand would name.</summary>
    private static readonly TimeSpan ExampleLength = TimeSpan.FromMinutes(90);

    private static readonly string[] Labels =
    [
        "project",
        "topic in (brackets), optional",
        "start, e.g. 09:00",
        "length 1h30m, or an end 09:00-11:00",
        "press Enter",
    ];

    /// <param name="continuesFrom">
    /// Where the last entry on the day ended, when one did: the start a line may leave out.
    /// </param>
    public static EntryStep Next(string? text, TimeOnly? continuesFrom = null)
    {
        var typed = (text ?? string.Empty).Trim();

        return typed.Length == 0 ? EntryStep.Project : Written(typed, continuesFrom) ?? Typed(typed);
    }

    /// <summary>A line the grammar already accepts, or nothing while it is still half-written.</summary>
    private static EntryStep? Written(string typed, TimeOnly? continuesFrom)
    {
        var parse = EntryCommandParser.Parse(typed, DateOnly.MinValue, _ => continuesFrom);

        if (!parse.Succeeded)
        {
            return null;
        }

        // A start with no length is legal, but it is not finished work.
        return parse.Command!.Duration > TimeSpan.Zero ? EntryStep.Complete : EntryStep.Span;
    }

    /// <summary>
    /// A half-written line: the brackets around the topic, then the clock that comes after them.
    /// A trailing "till" or "for" is already asking for the other half of the span.
    /// </summary>
    private static EntryStep Typed(string typed) =>
        Tail(typed) is not null ? EntryStep.Span
        : Clock().IsMatch(typed) ? EntryStep.Span
        : typed.Contains('(') && !typed.Contains(')') ? EntryStep.Topic
        : typed.Contains(')') ? EntryStep.Start
        : EntryStep.Topic;

    /// <summary>
    /// What to write at this step, for the hint line. Once the day has an end to continue from,
    /// the start step offers the shorthands as well as a clock.
    /// </summary>
    public static string Label(EntryStep step, string? text = null, TimeOnly? continuesFrom = null) =>
        step switch
        {
            EntryStep.Start when continuesFrom is { } from =>
                $"start 09:00, or from {ClockText.Format(from)} \"till {Example(from)}\" / \"for 1h30m\"",
            EntryStep.Span => SpanLabel(Tail(text ?? string.Empty), continuesFrom),
            _ => Labels[(int)step],
        };

    /// <summary>The half of a span that a trailing shorthand is still missing.</summary>
    private static string SpanLabel(string? tail, TimeOnly? continuesFrom) =>
        tail switch
        {
            "for" => "a length, e.g. 1h30m",
            "till" => $"an end, e.g. {Example(continuesFrom)}",
            _ => Labels[(int)EntryStep.Span],
        };

    /// <summary>
    /// A clock an example may name: one span after where the day stopped, so the example reads as a
    /// time later than the start it continues from. Without such a start, a plain 13:30 will do.
    /// </summary>
    private static string Example(TimeOnly? continuesFrom) =>
        continuesFrom is { } from && from.ToTimeSpan() + ExampleLength < TimeSpan.FromDays(1)
            ? ClockText.Format(TimeOnly.FromTimeSpan(from.ToTimeSpan() + ExampleLength))
            : "13:30";

    /// <summary>The shorthand the line already ends with: "till" (or "until"), "for", or nothing.</summary>
    private static string? Tail(string typed) =>
        TailWord().Match(typed) is { Success: true } tail
            ? tail.Groups["word"].Value.Equals("for", StringComparison.OrdinalIgnoreCase) ? "for" : "till"
            : null;

    [GeneratedRegex(@"\d{1,2}:\d{1,2}", RegexOptions.CultureInvariant)]
    private static partial Regex Clock();

    // The keyword that lets the line leave its start out, at the end of what has been typed.
    [GeneratedRegex(@"(?:\s|^)(?<word>till|until|for)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TailWord();
}
