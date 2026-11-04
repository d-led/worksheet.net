// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Desktop.ViewModels;

namespace Worksheet.Tests;

/// <summary>
/// A stored body read as a person reads it: written out with an indent, and cut into pieces that a
/// view can colour — the names of members apart from their values, and numbers and words apart from
/// the text.
/// </summary>
public sealed class JsonTextTests
{
    private const string Body = """
                                {"Entry":{"ProjectId":"a1","Minutes":90,"Crosses":false,"Topic":null}}
                                """;

    private static string Of(string body, JsonTone tone) =>
        string.Concat(JsonText.Runs(body).Where(run => run.Tone == tone).Select(run => run.Text));

    [Fact]
    public void A_body_is_written_out_with_an_indent()
    {
        var pretty = JsonText.Pretty(Body);

        Assert.Contains(Environment.NewLine, pretty);
        Assert.Contains("  \"Entry\": {", pretty);
        Assert.Equal(JsonText.Pretty(pretty), pretty);
    }

    [Fact]
    public void A_body_that_does_not_read_as_json_is_left_exactly_as_it_is()
    {
        const string broken = "{not json at all";

        Assert.Equal(broken, JsonText.Pretty(broken));
        Assert.Equal(broken, string.Concat(JsonText.Runs(broken).Select(run => run.Text)));
    }

    [Fact]
    public void Names_and_text_values_are_told_apart()
    {
        Assert.Contains("\"ProjectId\"", Of(Body, JsonTone.Name));
        Assert.DoesNotContain("\"a1\"", Of(Body, JsonTone.Name));
        Assert.Contains("\"a1\"", Of(Body, JsonTone.Text));
    }

    [Fact]
    public void Numbers_and_words_get_tones_of_their_own()
    {
        Assert.Contains("90", Of(Body, JsonTone.Number));
        Assert.Contains("false", Of(Body, JsonTone.Keyword));
        Assert.Contains("null", Of(Body, JsonTone.Keyword));
    }

    [Fact]
    public void The_pieces_of_a_body_are_all_of_it_and_nothing_else()
    {
        Assert.Equal(JsonText.Pretty(Body), string.Concat(JsonText.Runs(Body).Select(run => run.Text)));
    }

    [Fact]
    public void Neighbouring_pieces_of_one_kind_are_handed_over_as_one()
    {
        var runs = JsonText.Runs(Body);

        Assert.DoesNotContain(true, runs.Zip(runs.Skip(1)).Select(pair => pair.First.Tone == pair.Second.Tone));
    }

    [Fact]
    public void An_escaped_quote_does_not_end_a_piece_of_text()
    {
        const string escaped = """{"Note":"he said \"no\" twice"}""";

        var text = Of(escaped, JsonTone.Text);

        Assert.StartsWith("\"", text);
        Assert.EndsWith("\"", text);
        Assert.Contains("twice", text);
    }
}
