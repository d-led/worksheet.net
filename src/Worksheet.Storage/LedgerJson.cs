// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Worksheet.Core;

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
        options.Converters.Add(new ClockConverter());

        // A day's kind is stored as its name: the journal is meant to be readable in any viewer.
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}

/// <summary>An entry id travels as a plain string; the journal should stay readable.</summary>
public sealed class EntryIdConverter : JsonConverter<EntryId>
{
    public override EntryId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(Guid.Parse(reader.GetString() ?? throw new JsonException("An entry id must be a string.")));

    public override void Write(Utf8JsonWriter writer, EntryId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

/// <summary>
/// A clock is stored the way the day writes one — <c>09:05</c> — because a time of day in this
/// ledger is a whole minute, and a finer spelling in the journal would disagree with what the
/// window shows. A journal written before this spelling existed (<c>09:05:00</c>) still reads.
/// </summary>
public sealed class ClockConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ToTheMinute(Parse(reader.GetString() ?? throw new JsonException("A clock must be a string.")));

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ClockText.Format(value));

    private static TimeOnly Parse(string text) =>
        ClockText.TryParse(text, out var typed) ? typed
            : TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out var stored) ? stored
            : throw new JsonException($"'{text}' is not a clock.");

    private static TimeOnly ToTheMinute(TimeOnly time) => new(time.Hour, time.Minute);
}
