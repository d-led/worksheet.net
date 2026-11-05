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
    /// <summary>A line that is only a note: it asks for the words after the "#", then it is whole.</summary>
    Note,}

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
        "press Enter, or \"#\" and a note",
    ];

    /// <param name="continuesFrom">
    /// Where the last entry on the day ended, when one did: the start a line may leave out.
    /// </param>
    public static EntryStep Next(string? text, TimeOnly? continuesFrom = null)
    {
        var typed = (text ?? string.Empty).Trim();

        return typed.Length == 0
            ? EntryStep.Project
            : typed.StartsWith('#')
                ? NoteStep(typed)
                : Written(typed, continuesFrom) ?? Missing(typed, continuesFrom);
    }

    /// <summary>A note of its own is whole once something follows the mark; a bare "#" is asking for it.</summary>
    private static EntryStep NoteStep(string typed) => typed.Length > 1 ? EntryStep.Complete : EntryStep.Note;

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
    /// What a line that does not yet read as an entry is short of. A whole span that only becomes
    /// readable once some start is assumed is missing that start, which is a different thing to
    /// say than "write an end" to someone who has just written one.
    /// </summary>
    private static EntryStep Missing(string typed, TimeOnly? continuesFrom) =>
        continuesFrom is null && ReadsFromAnyStart(typed) ? EntryStep.Start : Typed(typed);

    /// <summary>Whether the line is a whole span whose only missing piece is the start it continues from.</summary>
    private static bool ReadsFromAnyStart(string typed) =>
        EntryCommandParser.Parse(typed, DateOnly.MinValue, _ => TimeOnly.MinValue).Succeeded;

    /// <summary>
    /// A half-written line: the brackets around the topic, then the clock that comes after them.
    /// A trailing "till" or "for" is already asking for the other half of the span, and a name
    /// closed with "-" has said where it ends, so its start comes next.
    /// </summary>
    private static EntryStep Typed(string typed) =>
        EntryCommandParser.SpanWordAtEnd(typed) is not null ? EntryStep.Span
        : Clock().IsMatch(typed) ? EntryStep.Span
        : typed.Contains('(') ? Bracketed(typed)
        : Closed(typed) ? EntryStep.Start
        : EntryStep.Topic;

    /// <summary>An open bracket is waiting for its topic; a closed one leaves the start.</summary>
    private static EntryStep Bracketed(string typed) => typed.Contains(')') ? EntryStep.Start : EntryStep.Topic;

    /// <summary>A name closed with "-" has said where it ends, so the start comes next.</summary>
    private static bool Closed(string typed) => typed.TrimEnd().EndsWith('-');

    /// <summary>
    /// What to write at this step, for the hint line. Once the day has an end to continue from,
    /// the start step offers the shorthands as well as a clock; the span step needs the word the
    /// line ends with to know which half of the span is missing.
    /// </summary>
    public static string Label(EntryStep step, string? text = null, TimeOnly? continuesFrom = null) =>
        step switch
        {
            EntryStep.Start when continuesFrom is { } from => StartLabel(from),
            EntryStep.Span => SpanLabel(WordAtEndOf(text), continuesFrom),
            EntryStep.Note => NoteLabel,
            _ => Labels[(int)step],
        };

    /// <summary>The "till" or "for" the line ends with, spelled the way the language spells it.</summary>
    private static string? WordAtEndOf(string? text) =>
        EntryCommandParser.SpanWordAtEnd(text ?? string.Empty)?.Word;

    /// <summary>What a note of its own still needs: the words after the mark.</summary>
    private const string NoteLabel = "the note after \"#\"";

    /// <summary>A clock, and the two shorthands — the one that names an end only while an end fits.</summary>
    private static string StartLabel(TimeOnly from) =>
        Example(from) is { } endsAt
            ? $"start 09:00, or from {ClockText.Format(from)} \"till {endsAt}\" / \"for 1h30m\""
            : $"start 09:00, or from {ClockText.Format(from)} \"for 1h30m\"";

    /// <summary>The half of a span that a trailing shorthand is still missing.</summary>
    private static string SpanLabel(string? tail, TimeOnly? continuesFrom) =>
        tail switch
        {
            "for" => "a length, e.g. 1h30m",
            "till" => $"an end, e.g. {Example(continuesFrom) ?? PlainClock}",
            _ => Labels[(int)EntryStep.Span],
        };

    /// <summary>The clock an example falls back to when there is no end of the day to build one from.</summary>
    private const string PlainClock = "13:30";

    /// <summary>
    /// A clock an example may name: one span after where the day stopped, so the example reads as a
    /// time later than the start it continues from — and nothing when the day has no room left, or
    /// nothing to continue from at all.
    /// </summary>
    private static string? Example(TimeOnly? continuesFrom) =>
        continuesFrom is { } from && from.ToTimeSpan() + ExampleLength < TimeSpan.FromDays(1)
            ? ClockText.Format(TimeOnly.FromTimeSpan(from.ToTimeSpan() + ExampleLength))
            : null;

    [GeneratedRegex(@"\d{1,2}:\d{1,2}", RegexOptions.CultureInvariant)]
    private static partial Regex Clock();
}
