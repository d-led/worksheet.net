// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Worksheet.Storage;

/// <summary>The one JSON shape for both the journal and snapshots, so a change reads the same in either.</summary>
public static class LedgerJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.Converters.Add(new EntryIdConverter());

        // A day's kind is stored as its name: the journal is meant to be readable in any viewer.
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}

/// <summary>An entry id travels as a plain string; the journal should stay readable.</summary>
public sealed class EntryIdConverter : JsonConverter<Core.EntryId>
{
    public override Core.EntryId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(Guid.Parse(reader.GetString() ?? throw new JsonException("An entry id must be a string.")));

    public override void Write(Utf8JsonWriter writer, Core.EntryId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
