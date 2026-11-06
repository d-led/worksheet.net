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
/// every input yields either a command or one sentence saying what is missing. A line may name
/// only its end or only its length — <c>till 13:30</c>, <c>for 1h30m</c> — and then starts where
/// the last entry on that day ended.
/// </summary>
public static partial class EntryCommandParser
{
    public const string EmptyInputMessage = "Type an entry, for example \"Website (login) 09:00 1h30m\".";

    public const string MissingTimeMessage = "No time found. Finish the line with \"09:00 1h30m\" or \"09:00-13:30\".";

    public const string MissingProjectMessage = "A project name is needed before the time.";

    public const string NothingToContinueMessage =
        "\"till\" and \"for\" start where the last entry ended, and this day has none; write the start, e.g. 09:00 1h30m.";

    /// <param name="continueFrom">
    /// When work stopped on a day: the end of its last entry, which a line that names no start
    /// continues from. It is asked per day, because a line may name its own.
    /// </param>
    public static EntryParse Parse(string? text, DateOnly today, Func<DateOnly, TimeOnly?>? continueFrom = null)
    {
        var rest = (text ?? string.Empty).Trim();

        return rest.Length == 0 ? EntryParse.Fail(EmptyInputMessage) : Read(rest, today, continueFrom);
    }

    /// <summary>A line with something on it, read from its note inwards: the day, then the span.</summary>
    private static EntryParse Read(string rest, DateOnly today, Func<DateOnly, TimeOnly?>? continueFrom)
    {
        var note = TakeNote(ref rest);
        var (date, dateError) = TakeDate(ref rest, today);

        return dateError is null ? ReadSpan(rest, date, note, continueFrom) : EntryParse.Fail(dateError);
    }

    /// <summary>
    /// The span is the last one a line carries — the fields are read from the right — and whatever
    /// follows it is the note, whether or not the line spent a "#" on the mark: nothing else may
    /// follow a span, so those words cannot be anything else. They are taken one at a time, and only
    /// when they carry nothing a span is made of, so a length typed wrong is still refused rather
    /// than written down as a comment.
    /// </summary>
    private static EntryParse ReadSpan(string rest, DateOnly date, string? note, Func<DateOnly, TimeOnly?>? continueFrom)
    {
        var found = SpanIn(rest, continueFrom?.Invoke(date), peel: note is null);

        return found.Span is { Matched: true } span
            ? Build(found.Line[..span.Index], span, date, note ?? Words(found.Trailing))
            : EntryParse.Fail(found.Span.Error ?? MissingTimeMessage);
    }

    /// <summary>The words a line carried after its span, or nothing when it carried none.</summary>
    private static string? Words(string trailing) => trailing.Length > 0 ? trailing : null;

    /// <summary>The line a span was found in, the span, and the words peeled off its end.</summary>
    private readonly record struct FoundSpan(string Line, SpanParse Span, string Trailing);

    /// <summary>
    /// Walks in from the end of a line until a span is at its end, answering where the span is and
    /// what was taken off to reach it. Words are taken only while the line says nothing about its
    /// note — a "#" is the person saying where the note begins — and only words no span is made of.
    /// </summary>
    private static FoundSpan SpanIn(string line, TimeOnly? continuesFrom, bool peel)
    {
        var body = line;
        var trailing = string.Empty;

        while (true)
        {
            var span = MatchSpan(body, continuesFrom);

            if (span.Matched || span.Error is not null || !peel || !PeelLastWord(ref body, ref trailing))
            {
                return new FoundSpan(body, span, trailing);
            }
        }
    }

    /// <summary>
    /// Takes the last word off a line when it cannot be part of a span — no digit and no colon —
    /// answering whether it did, and putting it in front of the words already taken.
    /// </summary>
    private static bool PeelLastWord(ref string body, ref string trailing)
    {
        var split = body.LastIndexOf(' ');

        if (split < 1)
        {
            return false;
        }

        var word = body[(split + 1)..];

        if (word.Any(char.IsDigit) || word.Contains(':'))
        {
            return false;
        }

        body = body[..split].TrimEnd();
        trailing = trailing.Length == 0 ? word : $"{word} {trailing}";

        return true;
    }

    private static EntryParse Build(string head, SpanParse span, DateOnly date, string? note)
    {
        var (project, topic, error) = NameOf(head);

        if (error is not null)
        {
            return EntryParse.Fail(error);
        }

        return project.Length == 0
            ? EntryParse.Fail(MissingProjectMessage)
            : EntryParse.Ok(new EntryCommand(date, project, topic, span.Start, span.Duration, note));
    }

    /// <summary>
    /// The name a line carries: its topic split off, and a "-" the person wrote to say where the
    /// name ends taken off. A name that still ends in something the line reads as a span is refused:
    /// keeping it would swallow a time into a project nobody meant to open.
    /// </summary>
    private static (string Project, string? Topic, string? Error) NameOf(string head)
    {
        var line = head.TrimEnd();
        var closedAtEnd = line.EndsWith('-');

        var (project, topic) = SplitTopic(closedAtEnd ? line[..^1].TrimEnd() : line);
        var closedBeforeTopic = project.EndsWith('-');

        project = (closedBeforeTopic ? project[..^1] : project).TrimEnd();

        return closedAtEnd || closedBeforeTopic || Swallowed(project) is not { } swallowed
            ? (project, topic, null)
            : (project, topic, SwallowedName(project, swallowed));
    }

    /// <summary>What a name ends in that the line would otherwise read as part of its span.</summary>
    private static string? Swallowed(string project) =>
        OpenStart().IsMatch(project) ? "a time"
        : SpanWordAtEnd(project)?.Word is { } word ? $"\"{word}\", which the line reads as its span"
        : null;

    private static string SwallowedName(string project, string swallowed) =>
        $"\"{project}\" ends in {swallowed}; end the project name with \"-\" if that is part of it, or write one start and one span.";

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

    private static SpanParse MatchSpan(string text, TimeOnly? continuesFrom)
    {
        var attempt = MatchRange(text)
            ?? MatchLength(text)
            ?? MatchTill(text, continuesFrom)
            ?? MatchFor(text, continuesFrom)
            ?? MatchOpenStart(text);

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

        return TryReadClock(match.Groups["from"], out var from)
            ? SpanOf(match.Index, from, match.Groups["dur"])
            : SpanParse.Fail(NotATime(match.Groups["from"].Value));
    }

    /// <summary>A span of the duration the line wrote, from the start it has.</summary>
    private static SpanParse SpanOf(int index, TimeOnly from, Group written) =>
        Durations.TryParse(written.Value, out var duration)
            ? SpanParse.Match(index, from, duration)
            : SpanParse.Fail(NotADuration(written.Value));

    /// <summary>
    /// The start of a line that says "till" or "for": the clock written in front of the keyword, else
    /// where the last entry of the day ended. Neither is assumed when neither is there.
    /// </summary>
    private static (TimeOnly Start, string? Error) StartOf(Match match, TimeOnly? continuesFrom)
    {
        if (match.Groups["from"] is { Success: true } written)
        {
            return TryReadClock(written, out var start) ? (start, null) : (default, NotATime(written.Value));
        }

        return continuesFrom is { } last ? (last, null) : (default, NothingToContinueMessage);
    }

    /// <summary>"till 13:30": the end is written, the start is in front of it or where the last entry ended.</summary>
    private static SpanParse? MatchTill(string text, TimeOnly? continuesFrom)
    {
        var match = Till().Match(text);

        if (!match.Success)
        {
            return null;
        }

        var (start, error) = StartOf(match, continuesFrom);

        if (error is not null)
        {
            return SpanParse.Fail(error);
        }

        return TryReadClock(match.Groups["to"], out var to)
            ? Ended(match.Index, start, to)
            : SpanParse.Fail(NotATime(match.Groups["to"].Value));
    }

    /// <summary>A span that stops at a written clock, if that clock is after where it starts.</summary>
    private static SpanParse Ended(int index, TimeOnly from, TimeOnly to) =>
        to <= from
            ? SpanParse.Fail($"{Clock(to)} is not after {Clock(from)}, where the last entry ended.")
            : SpanParse.Match(index, from, to.ToTimeSpan() - from.ToTimeSpan());

    /// <summary>"for 1h30m": the length is written, the start is in front of it or where the last entry ended.</summary>
    private static SpanParse? MatchFor(string text, TimeOnly? continuesFrom)
    {
        var match = For().Match(text);

        if (!match.Success)
        {
            return null;
        }

        var (start, error) = StartOf(match, continuesFrom);

        return error is not null ? SpanParse.Fail(error) : SpanOf(match.Index, start, match.Groups["dur"]);
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

    /// <summary>
    /// Where the span word at the end of a line begins — "till", "until" or "for" — and which it is,
    /// spelled as the language spells it: "until" reads as "till". Nothing when the line does not
    /// end with one.
    /// </summary>
    internal static (int Index, string Word)? SpanWordAtEnd(string text) =>
        SpanWord().Match(text) is { Success: true } match
            ? (match.Index, match.Groups["word"].Value.Equals("for", StringComparison.OrdinalIgnoreCase) ? "for" : "till")
            : null;

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

    private static string NotADuration(string text) => $"'{text}' is not a duration; write it like 3h30m, 90m or 2h.";

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

    // A project name optionally followed by a parenthesised topic. Empty brackets are no topic: a
    // person who types them means the project alone, not a project called 'Website ()'.
    [GeneratedRegex(@"^(?<project>.*?)\s*\(\s*(?<topic>[^()]{0,64})\s*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex Topic();

    // "09:00-13:30", "9:00am-1:30pm" — and "09:00 13:30", because a dash is a keystroke.
    [GeneratedRegex(@"(?<from>\d{1,2}:\d{1,2}(?:[apAP][mM])?)(?:\s*-\s*|\s+)(?<to>\d{1,2}:\d{1,2}(?:[apAP][mM])?)$", RegexOptions.CultureInvariant)]
    private static partial Regex Range();

    // "09:00 3h30m" and "09:00 3h 30m".
    [GeneratedRegex(@"(?<from>\d{1,2}:\d{1,2}(?:[apAP][mM])?)\s+(?<dur>(?:\d+\s*[hHmM]\s*)+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Length();

    // "till 13:30" and "until 13:30", from where the last entry ended — or from the clock written in
    // front of the keyword: "09:00 till 13:30" is "09:00-13:30" spelt with a word. A "-" after the
    // name breaks the pattern, so a time that belongs to the name stays in it.
    [GeneratedRegex("(?:(?<from>\\d{1,2}:\\d{1,2}(?:[apAP][mM])?)\\s+)?(?<!\\w)(?:till|until)\\s+(?<to>\\d{1,2}:\\d{1,2}(?:[apAP][mM])?)\\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Till();

    // "for 1h30m", from the same start.
    [GeneratedRegex(@"(?:(?<from>\d{1,2}:\d{1,2}(?:[apAP][mM])?)\s+)?(?<!\w)for\s+(?<dur>(?:\d+\s*[hHmM]\s*)+)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex For();

    // The word that asks for the other half of a span, at the end of what has been typed.
    [GeneratedRegex(@"(?:\s|^)(?<word>till|until|for)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SpanWord();

    // A bare "09:00": a start with no length yet.
    [GeneratedRegex(@"(?<from>\d{1,2}:\d{1,2}(?:[apAP][mM])?)$", RegexOptions.CultureInvariant)]
    private static partial Regex OpenStart();
}
