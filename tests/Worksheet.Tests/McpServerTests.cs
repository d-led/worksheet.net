// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;
using Worksheet.Mcp;

namespace Worksheet.Tests;

/// <summary>
/// The protocol half of the MCP server, with the transport left out: one body in, one body out.
/// </summary>
public sealed class McpServerTests
{
    private const string Handshake = """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}
        """;

    /// <summary>A toolkit with one tool, so the protocol can be watched without a ledger.</summary>
    private sealed class EchoToolkit : IToolkit
    {
        public List<ToolCall> Calls { get; } = [];

        public IReadOnlyList<ToolDefinition> Tools { get; } =
        [
            new(
                "echo",
                "Says it back.",
                ToolSchema.Of(("text", ToolProperty.Text("What to say."))),
                Title: "Echo something",
                Annotations: ToolAnnotations.Readings),
        ];

        public ToolOutcome Run(ToolCall call)
        {
            Calls.Add(call);

            return call.Text("text") is { } text ? ToolOutcome.Said($"said {text}") : ToolOutcome.Refused("echo needs some text.");
        }
    }

    private readonly EchoToolkit _toolkit = new();

    private string? Answer(string body) => new McpServer(_toolkit).Answer(body);

    private JsonElement Asked(string body) => JsonDocument.Parse(Answer(body)!).RootElement;

    [Fact]
    public void Initialize_answers_with_the_tool_capability_and_a_name()
    {
        var result = Asked(Handshake).GetProperty("result");

        Assert.Equal("worksheet", result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.True(result.GetProperty("capabilities").TryGetProperty("tools", out _));
    }

    [Fact]
    public void The_handshake_tells_a_client_how_a_line_is_written_before_it_can_call_one()
    {
        var instructions = Asked(Handshake).GetProperty("result").GetProperty("instructions").GetString();

        Assert.Contains("one call records one range", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-11-03", instructions);
        Assert.Contains("'Break 12:30 45m'", instructions);
    }

    [Fact]
    public void The_handshake_says_what_to_do_when_nothing_answers()
    {
        var instructions = Asked(Handshake).GetProperty("result").GetProperty("instructions").GetString();

        Assert.Contains("check that Worksheet is running", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same port", instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_tool_is_listed_with_its_title_and_whether_it_only_reads()
    {
        var listed = Asked("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""")
            .GetProperty("result")
            .GetProperty("tools")[0];

        Assert.Equal("Echo something", listed.GetProperty("title").GetString());
        Assert.True(listed.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.False(listed.GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
    }

    [Fact]
    public void The_id_of_a_request_comes_back_unchanged()
    {
        var answer = Asked(Handshake);

        Assert.Equal(1, answer.GetProperty("id").GetInt32());
    }

    [Fact]
    public void A_string_id_comes_back_as_a_string()
    {
        var answer = Asked("""{"jsonrpc":"2.0","id":"7","method":"ping"}""");

        Assert.Equal("7", answer.GetProperty("id").GetString());
    }

    [Fact]
    public void A_protocol_version_the_server_speaks_is_echoed_back()
    {
        var answer = Asked(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}""");

        Assert.Equal("2024-11-05", answer.GetProperty("result").GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public void A_protocol_version_the_server_does_not_know_is_answered_with_its_own()
    {
        var answer = Asked(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"1999-01-01"}}""");

        Assert.Equal(McpServer.ProtocolVersion, answer.GetProperty("result").GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public void Tools_list_announces_each_tool_with_its_arguments()
    {
        var tools = Asked("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""").GetProperty("result").GetProperty("tools");

        var echo = Assert.Single(tools.EnumerateArray());

        Assert.Equal("echo", echo.GetProperty("name").GetString());
        Assert.Equal("Says it back.", echo.GetProperty("description").GetString());
        Assert.True(echo.GetProperty("inputSchema").GetProperty("properties").TryGetProperty("text", out _));
    }

    [Fact]
    public void A_tool_call_reaches_the_tool_with_its_arguments()
    {
        Asked("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hi"}}}""");

        Assert.Equal("echo", _toolkit.Calls[0].Name);
        Assert.Equal("hi", _toolkit.Calls[0].Text("text"));
    }

    [Fact]
    public void A_tool_call_answers_the_text_and_that_it_worked()
    {
        var result = Asked(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hi"}}}""")
            .GetProperty("result");

        Assert.Equal("said hi", result.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.False(result.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public void A_tool_that_refuses_answers_that_it_failed()
    {
        var result = Asked("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo"}}""")
            .GetProperty("result");

        Assert.Equal("echo needs some text.", result.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.True(result.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public void A_notification_gets_no_answer()
    {
        Assert.Null(Answer("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));
    }

    [Fact]
    public void An_unknown_notification_is_dropped_without_an_answer()
    {
        Assert.Null(Answer("""{"jsonrpc":"2.0","method":"notifications/something-new"}"""));
    }

    [Fact]
    public void An_unknown_method_is_refused_and_the_id_still_comes_back()
    {
        var answer = Asked("""{"jsonrpc":"2.0","id":4,"method":"resources/list"}""");

        Assert.Equal(4, answer.GetProperty("id").GetInt32());
        Assert.Equal(-32601, answer.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public void A_body_that_is_not_json_is_a_parse_error()
    {
        var answer = Asked("this is not json");

        Assert.Equal(-32700, answer.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("id").ValueKind);
    }

    [Fact]
    public void A_request_without_a_method_is_refused()
    {
        var answer = Asked("""{"jsonrpc":"2.0","id":5}""");

        Assert.Equal(-32600, answer.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public void tools_call_without_a_tool_name_is_an_invalid_params_error()
    {
        var answer = Asked("""{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{}}""");

        Assert.Equal(-32602, answer.GetProperty("error").GetProperty("code").GetInt32());
    }
}
