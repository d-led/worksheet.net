// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;

namespace Worksheet.Desktop.ViewModels;

/// <summary>What a piece of a JSON body is, which is all the colouring needs to know.</summary>
public enum JsonTone
{
    Name,
    Text,
    Number,
    Keyword,
    Punctuation,
}

/// <summary>One run of characters of one kind, ready to be drawn in one colour.</summary>
public sealed record JsonRun(string Text, JsonTone Tone);

/// <summary>
/// A stored body written out with an indent and cut into runs a view can colour. Read here rather
/// than fetched from a highlighting library: the body is JSON in one shape, the application ships as
/// one binary, and a page that needed a connection before it could be read would be a strange thing
/// to put in front of somebody looking at their own journal.
/// </summary>
public static class JsonText
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>The body with an indent, or the body exactly as it is when it does not read as JSON.</summary>
    public static string Pretty(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return JsonSerializer.Serialize(document.RootElement, Indented);
        }
        catch (JsonException)
        {
            return body;
        }
    }

    /// <summary>The pretty body, cut into runs of one kind each.</summary>
    public static IReadOnlyList<JsonRun> Runs(string body) => Merged(Tokens(Pretty(body)));

    private static IEnumerable<JsonRun> Tokens(string pretty)
    {
        var index = 0;

        while (index < pretty.Length)
        {
            var (end, tone) = Next(pretty, index);

            yield return new JsonRun(pretty[index..end], tone);

            index = end;
        }
    }

    /// <summary>Where the run that starts here ends, and what kind it is.</summary>
    private static (int End, JsonTone Tone) Next(string pretty, int start) =>
        pretty[start] switch
        {
            var character when char.IsWhiteSpace(character) => (EndOfSpace(pretty, start), JsonTone.Punctuation),
            '"' => (EndOfText(pretty, start), NamedText(pretty, start)),
            var character when char.IsDigit(character) || character == '-' => (EndOfNumber(pretty, start), JsonTone.Number),
            var character when char.IsLetter(character) => (EndOfWord(pretty, start), JsonTone.Keyword),
            _ => (start + 1, JsonTone.Punctuation),
        };

    /// <summary>A piece of text in front of a colon names a member; anywhere else it is the value.</summary>
    private static JsonTone NamedText(string pretty, int start) =>
        FollowedByColon(pretty, EndOfText(pretty, start)) ? JsonTone.Name : JsonTone.Text;

    private static int EndOfSpace(string pretty, int start)
    {
        var index = start;

        while (index < pretty.Length && char.IsWhiteSpace(pretty[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>Where a piece of text ends, with the escapes it contains kept inside it.</summary>
    private static int EndOfText(string pretty, int start)
    {
        var index = start + 1;

        while (index < pretty.Length)
        {
            if (pretty[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (pretty[index] == '"')
            {
                return index + 1;
            }

            index++;
        }

        return pretty.Length;
    }

    private static int EndOfNumber(string pretty, int start)
    {
        var index = start + 1;

        while (index < pretty.Length && IsNumberPart(pretty[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>What a number may go on with, which is more than digits once the point and the exponent arrive.</summary>
    private static bool IsNumberPart(char character) =>
        char.IsDigit(character) || NumberParts.Contains(character);

    private const string NumberParts = ".eE+-";
    private static int EndOfWord(string pretty, int start)
    {
        var index = start + 1;

        while (index < pretty.Length && char.IsLetter(pretty[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>Nothing but spaces between a name and its colon, which is what tells the two apart.</summary>
    private static bool FollowedByColon(string pretty, int end)
    {
        var index = EndOfSpace(pretty, end);

        return index < pretty.Length && pretty[index] == ':';
    }

    /// <summary>Neighbouring runs of one kind are one run, so a view builds fewer pieces.</summary>
    private static IReadOnlyList<JsonRun> Merged(IEnumerable<JsonRun> runs)
    {
        var merged = new List<JsonRun>();

        foreach (var run in runs)
        {
            if (merged.Count > 0 && merged[^1].Tone == run.Tone)
            {
                merged[^1] = merged[^1] with { Text = merged[^1].Text + run.Text };
                continue;
            }

            merged.Add(run);
        }

        return merged;
    }
}
