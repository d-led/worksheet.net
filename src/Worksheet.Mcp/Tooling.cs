// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Worksheet.Mcp;

/// <summary>What a client may assume about a tool before calling one, as the specification names it.</summary>
public sealed record ToolAnnotations(
    [property: JsonPropertyName("readOnlyHint")] bool ReadOnly = false,
    [property: JsonPropertyName("destructiveHint")] bool Destructive = false)
{
    public static ToolAnnotations Readings { get; } = new(ReadOnly: true);

    public static ToolAnnotations Writes { get; } = new();

    public static ToolAnnotations Removes { get; } = new(Destructive: true);
}

/// <summary>One property of a tool's input: what it holds, what it is for, and whether a call must carry it.</summary>
public sealed record ToolProperty(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description)
{
    /// <summary>Whether <c>required</c> names this property. Only the schema says it, never the property itself.</summary>
    [JsonIgnore]
    public bool Required { get; init; } = true;

    public static ToolProperty Text(string description, bool required = true) =>
        new("string", description) { Required = required };
}

/// <summary>
/// The input a tool takes: an object of named properties, with the ones a call must carry named in
/// <c>required</c>. Written as a type rather than an anonymous shape, because the listing is part of
/// what a client reads and the names in it are a contract.
/// </summary>
public sealed record ToolSchema(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, ToolProperty> Properties,
    [property: JsonPropertyName("required")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Required = null)
{
    /// <summary>A tool that takes nothing at all.</summary>
    public static ToolSchema Nothing { get; } = new("object", new Dictionary<string, ToolProperty>());

    /// <summary>An input of these properties, with exactly the ones marked required.</summary>
    public static ToolSchema Of(params (string Name, ToolProperty Property)[] properties) =>
        new(
            "object",
            properties.ToDictionary(pair => pair.Name, pair => pair.Property),
            [.. properties.Where(pair => pair.Property.Required).Select(pair => pair.Name)]);
}

/// <summary>
/// A tool as a client meets it: a name, a title for a list of them, when to reach for it, the shape
/// of its arguments, and whether it only reads. An agent that never spoke to this server before has
/// nothing but these lines.
/// </summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    ToolSchema InputSchema,
    string? Title = null,
    ToolAnnotations? Annotations = null);

/// <summary>
/// One tool call as it arrived. Arguments stay JSON until a tool asks for a name, so a client that
/// sends the wrong shape is refused with a sentence rather than an exception.
/// </summary>
public sealed record ToolCall(string Name, JsonElement? Arguments)
{
    /// <summary>A string argument, or nothing when the client did not send one.</summary>
    public string? Text(string name) => Raw(name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    public JsonElement? Raw(string name) =>
        Arguments is { ValueKind: JsonValueKind.Object } arguments && arguments.TryGetProperty(name, out var value)
            ? value
            : null;
}

/// <summary>What a tool answers: text for the caller, and whether it refused to do what was asked.</summary>
public sealed record ToolOutcome(string Text, bool IsError)
{
    public static ToolOutcome Said(string text) => new(text, IsError: false);

    public static ToolOutcome Refused(params string[] problems) => new(string.Join(" ", problems), IsError: true);
}

/// <summary>The tools this server offers, and the way to call one.</summary>
public interface IToolkit
{
    IReadOnlyList<ToolDefinition> Tools { get; }

    ToolOutcome Run(ToolCall toolCall);
}

/// <summary>
/// Runs every call where the ledger may be touched. The fold has one writer and no lock, so the
/// transport thread must never reach it directly; the host decides which thread calls arrive on.
/// </summary>
public sealed class SerialToolkit(IToolkit inner, Func<Func<ToolOutcome>, ToolOutcome> serialise) : IToolkit
{
    public IReadOnlyList<ToolDefinition> Tools => inner.Tools;

    public ToolOutcome Run(ToolCall toolCall) => serialise(() => inner.Run(toolCall));
}
