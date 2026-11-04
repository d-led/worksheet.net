// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;

namespace Worksheet.Mcp;

/// <summary>What a client may assume about a tool before calling one, as the specification names it.</summary>
public sealed record ToolAnnotations(bool ReadOnly = false, bool Destructive = false)
{
    public static ToolAnnotations Readings { get; } = new(ReadOnly: true);

    public static ToolAnnotations Writes { get; } = new();

    public static ToolAnnotations Removes { get; } = new(Destructive: true);
}

/// <summary>
/// A tool as a client meets it: a name, a title for a list of them, when to reach for it, the shape
/// of its arguments, and whether it only reads. An agent that never spoke to this server before has
/// nothing but these lines.
/// </summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    object InputSchema,
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

    ToolOutcome Call(ToolCall call);
}

/// <summary>
/// Runs every call where the ledger may be touched. The fold has one writer and no lock, so the
/// transport thread must never reach it directly; the host decides which thread calls arrive on.
/// </summary>
public sealed class SerialToolkit(IToolkit inner, Func<Func<ToolOutcome>, ToolOutcome> run) : IToolkit
{
    public IReadOnlyList<ToolDefinition> Tools => inner.Tools;

    public ToolOutcome Call(ToolCall call) => run(() => inner.Call(call));
}
