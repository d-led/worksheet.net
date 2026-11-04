// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Worksheet.Mcp;

/// <summary>One tool as <c>tools/list</c> hands it over: the names here are the protocol's, not ours.</summary>
internal sealed record ToolListing(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] ToolSchema InputSchema,
    [property: JsonPropertyName("annotations")] ToolAnnotations Annotations);

/// <summary>
/// MCP as JSON-RPC 2.0 over one body in and one body out: <c>initialize</c>, <c>tools/list</c> and
/// <c>tools/call</c>. Nothing here knows about sockets, so the whole protocol is testable with a
/// string, and the transport is free to be the smallest thing that carries a body.
/// </summary>
public sealed class McpServer(IToolkit toolkit)
{
    public const string ProtocolVersion = "2025-06-18";

    /// <summary>The protocol versions this server understands, newest first.</summary>
    private static readonly string[] KnownVersions = ["2025-06-18", "2025-03-26", "2024-11-05"];

    public const string ServerName = "worksheet";

    /// <summary>
    /// What a client is told before it sees a single tool: the one thing the tool names cannot say —
    /// that a range is one line of the application's own syntax, and that a refusal is the whole
    /// answer, with nothing written. Terse on purpose: it is pasted into a prompt, not read as prose.
    /// </summary>
    private const string Manual =
        "Log the working day: one line per time range, on projects and on the topics inside them. "
        + "A range is written in the application's own syntax, for example 'Client portal (login) 08:30 2h30m', "
        + "'Client portal (reports) 11:00-12:30' or 'Break 12:30 45m' — a pause is recorded under the project 'Break'. "
        + "One call records one range, so a day of four ranges is four calls. "
        + "A line may name its own day ('3.11. Reading 14:00 45m'); otherwise the call's date decides, and it is today "
        + "when the call does not give one. Dates are written 2026-11-03. "
        + "A range that cannot be recorded is refused with a sentence saying why, and nothing is written. "
        + "Read back with 'day' and 'totals'; see the catalogue with 'list_projects' and 'list_topics'; "
        + "take back the last change, whoever made it, with 'undo'.";

    /// <summary>
    /// The answer to one request, or nothing when the request was a notification. A notification
    /// has no id and expects no body back, which the transport turns into an empty 202.
    /// </summary>
    public string? Answer(string? body)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(body ?? string.Empty);
        }
        catch (JsonException error)
        {
            return Answered(id: null, envelope => envelope["error"] = Error(-32700, $"That is not JSON: {error.Message}"));
        }

        using (document)
        {
            return Answer(document.RootElement);
        }
    }

    private string? Answer(JsonElement request)
    {
        var id = IdOf(request);

        if (MethodOf(request) is not { } method)
        {
            return Answered(id, envelope => envelope["error"] = Error(-32600, "A request needs a method."));
        }

        switch (method)
        {
            case "initialize":
                return Answered(id, envelope => envelope["result"] = Initialize(request));
            case "ping":
                return Answered(id, envelope => envelope["result"] = new JsonObject());
            case "tools/list":
                return Answered(id, envelope => envelope["result"] = Listed());
            case "tools/call":
                return CallTool(request, id);
        }

        // Notifications are one-way: an unknown one is dropped, the way the specification asks.
        if (id is null || method.StartsWith("notifications/", StringComparison.Ordinal))
        {
            return null;
        }

        return Answered(id, envelope => envelope["error"] = Error(-32601, $"'{method}' is not a method this server has."));
    }

    /// <summary>
    /// The client's own version is echoed back when this server speaks it; otherwise the server
    /// says which one it does speak, and the client decides whether that is good enough.
    /// </summary>
    private static JsonObject Initialize(JsonElement request) =>
        new()
        {
            ["protocolVersion"] = Negotiated(request),
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = ServerName,
                ["title"] = "Worksheet",
                ["version"] = typeof(McpServer).Assembly.GetName().Version?.ToString(3) ?? "0.1.0",
            },
            ["instructions"] = Manual,
        };

    private static string Negotiated(JsonElement request) =>
        request.TryGetProperty("params", out var parameters)
        && parameters.ValueKind == JsonValueKind.Object
        && parameters.TryGetProperty("protocolVersion", out var version)
        && version.ValueKind == JsonValueKind.String
        && KnownVersions.Contains(version.GetString())
            ? version.GetString()!
            : ProtocolVersion;

    private JsonObject Listed() =>
        new() { ["tools"] = JsonSerializer.SerializeToNode(toolkit.Tools.Select(ToolJson)) };

    private string CallTool(JsonElement request, JsonElement? id)
    {
        if (ParametersOf(request) is not { } parameters
            || parameters.TryGetProperty("name", out var nameElement) is false
            || nameElement.ValueKind != JsonValueKind.String)
        {
            return Answered(id, envelope => envelope["error"] = Error(-32602, "tools/call needs params.name."));
        }

        // The arguments are cloned out of the parsed body: a tool may be run on another thread, and
        // it must not be reading a document this method is about to dispose.
        var call = parameters.TryGetProperty("arguments", out var given)
            ? new ToolCall(nameElement.GetString()!, given.Clone())
            : new ToolCall(nameElement.GetString()!, null);
        var outcome = toolkit.Call(call);

        return Answered(
            id,
            envelope => envelope["result"] = new JsonObject
            {
                ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = outcome.Text } },
                ["isError"] = outcome.IsError,
            });
    }

    private static ToolListing ToolJson(ToolDefinition tool) =>
        new(
            tool.Name,
            tool.Title ?? tool.Name,
            tool.Description,
            tool.InputSchema,
            tool.Annotations ?? ToolAnnotations.Writes);

    private static string? MethodOf(JsonElement request) =>
        request.ValueKind == JsonValueKind.Object
        && request.TryGetProperty("method", out var method)
        && method.ValueKind == JsonValueKind.String
            ? method.GetString()
            : null;

    private static JsonElement? ParametersOf(JsonElement request) =>
        request.TryGetProperty("params", out var parameters) && parameters.ValueKind == JsonValueKind.Object
            ? parameters
            : null;

    /// <summary>An id is echoed exactly as it came: a number stays a number, a string a string.</summary>
    private static JsonElement? IdOf(JsonElement request) =>
        request.ValueKind == JsonValueKind.Object
        && request.TryGetProperty("id", out var id)
        && id.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? id
            : null;

    private static string Answered(JsonElement? id, Action<JsonObject> body)
    {
        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id is { } value ? JsonNode.Parse(value.GetRawText()) : null,
        };

        body(envelope);

        return envelope.ToJsonString();
    }

    private static JsonObject Error(int code, string message) => new() { ["code"] = code, ["message"] = message };
}
