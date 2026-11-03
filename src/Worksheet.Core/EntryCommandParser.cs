// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Worksheet.Core;

/// <summary>
/// Reads one line of the entry language (see <c>docs/entry-syntax.md</c>). Fields are recognised
/// from the right, so a project name may contain spaces, digits and dots. The parser is total:
/// every input yields either a command or one sentence saying what is missing.
/// </summary>
public static partial class EntryCommandParser
{
    public const string EmptyInputMessage = "Type an entry, for example \"Website (login) 09:00 1h30m\".";

    public const string MissingTimeMessage = "No time found. Finish the line with \"09:00 1h30m\" or \"09:00-13:30\".";

    public const string MissingProjectMessage = "A project name is needed before the time.";

    public static EntryParse Parse(string? text, DateOnly today)
    {
        var rest = (text ?? string.Empty).Trim();

        if (rest.Length == 0)
        {
            return EntryParse.Fail(EmptyInputMessage);
        }

        var note = TakeNote(ref rest);

        var (date, dateError) = TakeDate(ref rest, today);

        if (dateError is not null)
        {
            return EntryParse.Fail(dateError);
        }

        var span = MatchSpan(rest);

        if (span.Error is not null)
        {
            return EntryParse.Fail(span.Error);
        }

        return span.Matched
            ? Build(rest[..span.Index], span, date, note)
            : EntryParse.Fail(MissingTimeMessage);
    }

    private static EntryParse Build(string head, SpanParse span, DateOnly date, string? note)
    {
        var projectAndTopic = head.TrimEnd();

        if (projectAndTopic.Length == 0)
        {
            return EntryParse.Fail(MissingProjectMessage);
        }

        var (project, topic) = SplitTopic(projectAndTopic);

        return project.Length == 0
            ? EntryParse.Fail(MissingProjectMessage)
            : EntryParse.Ok(new EntryCommand(date, project, topic, span.Start, span.Duration, note));
    }

    private static string? TakeNote(ref string text)
    {
        var hash = text.IndexOf('#');

        if (hash < 0)
        {
            return null;
        }

        var note = text[(hash + 1)..].Trim();
        text = text[..hash].TrimEnd();

        return note.Length == 0 ? null : note;
    }

    private static (DateOnly Date, string? Error) TakeDate(ref string text, DateOnly today)
    {
        var match = DatePrefix().Match(text);

        if (!match.Success)
        {
            return (today, null);
        }

        if (!TryReadDate(match, today, out var date))
        {
            return (today, $"'{match.Value.Trim()}' is not a real date.");
        }

        text = text[match.Length..].TrimStart();

        return (date, null);
    }

    private static bool TryReadDate(Match match, DateOnly today, out DateOnly date)
    {
        date = today;

        var day = Digits(match.Groups["d"].Value);
        var month = Digits(match.Groups["m"].Value);
        var year = match.Groups["y"].Success ? Digits(match.Groups["y"].Value) : today.Year;

        if (!IsRealDate(day, month, year))
        {
            return false;
        }

        date = new DateOnly(year, month, day);
        return true;
    }

    private static bool IsRealDate(int day, int month, int year) =>
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);

    private static SpanParse MatchSpan(string text)
    {
        var attempt = MatchRange(text) ?? MatchLength(text) ?? MatchOpenStart(text);

        return attempt ?? SpanParse.NoMatch;
    }

    private static SpanParse? MatchRange(string text)
    {
        var match = Range().Match(text);

        if (!match.Success)
        {
            return null;
        }

        if (!TryReadClock(match.Groups["from"], out var from))
        {
            return SpanParse.Fail(NotATime(match.Groups["from"].Value));
        }

        if (!TryReadClock(match.Groups["to"], out var to))
        {
            return SpanParse.Fail(NotATime(match.Groups["to"].Value));
        }

        return to <= from
            ? SpanParse.Fail($"{Clock(to)} is not after {Clock(from)}; a range must end on the same day.")
            : SpanParse.Match(match.Index, from, to.ToTimeSpan() - from.ToTimeSpan());
    }

    private static SpanParse? MatchLength(string text)
    {
        var match = Length().Match(text);

        if (!match.Success)
        {
            return null;
        }

        if (!TryReadClock(match.Groups["from"], out var from))
        {
            return SpanParse.Fail(NotATime(match.Groups["from"].Value));
        }

        var written = match.Groups["dur"].Value;

        return Durations.TryParse(written, out var duration)
            ? SpanParse.Match(match.Index, from, duration)
            : SpanParse.Fail($"'{written}' is not a duration; write it like 3h30m, 90m or 2h.");
    }

    private static SpanParse? MatchOpenStart(string text)
    {
        var match = OpenStart().Match(text);

        if (!match.Success)
        {
            return null;
        }

        return TryReadClock(match.Groups["from"], out var from)
            ? SpanParse.Match(match.Index, from, TimeSpan.Zero)
            : SpanParse.Fail(NotATime(match.Groups["from"].Value));
    }

    private static bool TryReadClock(Group group, out TimeOnly time) => ClockText.TryParse(group.Value, out time);

    private static (string Project, string? Topic) SplitTopic(string text)
    {
        var match = Topic().Match(text);

        if (!match.Success)
        {
            return (text.Trim(), null);
        }

        var topic = match.Groups["topic"].Value.Trim();

        return (match.Groups["project"].Value.Trim(), topic.Length == 0 ? null : topic);
    }

    private static string NotATime(string text) => $"'{text.Trim()}' is not a time; write it like 09:00.";

    private static string Clock(TimeOnly time) => ClockText.Format(time);

    private static int Digits(string text) => int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);

    private readonly record struct SpanParse(bool Matched, int Index, TimeOnly Start, TimeSpan Duration, string? Error)
    {
        public static SpanParse NoMatch => new(false, 0, default, default, null);

        public static SpanParse Match(int index, TimeOnly start, TimeSpan duration) => new(true, index, start, duration, null);

        public static SpanParse Fail(string error) => new(false, 0, default, default, error);
    }

    // A leading date, "3.11. " or "12.11.2026. ".
    [GeneratedRegex(@"^(?<d>\d{1,2})\.(?<m>\d{1,2})\.(?:(?<y>\d{4})\.?)?\s+", RegexOptions.CultureInvariant)]
    private static partial Regex DatePrefix();

    // A project name optionally followed by a parenthesised topic.
    [GeneratedRegex(@"^(?<project>.*?)\s*\(\s*(?<topic>[^()]{1,64})\s*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex Topic();

    // "09:00-13:30", "9:00am-1:30pm" — and "09:00 13:30", because a dash is a keystroke.
    [GeneratedRegex(@"(?<from>\d{1,2}:\d{1,2}(?:[apAP][mM])?)(?:\s*-\s*|\s+)(?<to>\d{1,2}:\d{1,2}(?:[apAP][mM])?)$", RegexOptions.CultureInvariant)]
    private static partial Regex Range();

    // "09:00 3h30m" and "09:00 3h 30m".
    [GeneratedRegex(@"(?<from>\d{1,2}:\d{1,2}(?:[apAP][mM])?)\s+(?<dur>(?:\d+\s*[hHmM]\s*)+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Length();

    // A bare "09:00": a start with no length yet.
    [GeneratedRegex(@"(?<from>\d{1,2}:\d{1,2}(?:[apAP][mM])?)$", RegexOptions.CultureInvariant)]
    private static partial Regex OpenStart();
}
