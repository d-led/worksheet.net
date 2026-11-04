// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Worksheet.Storage;

/// <summary>Where a setting's value came from, so the settings view can say it rather than guess.</summary>
public enum SettingSource
{
    /// <summary>Nobody chose it: the built-in value is in force.</summary>
    Default,

    /// <summary>The environment supplies it, which is how a portable copy or a script sets it.</summary>
    Environment,

    /// <summary>Someone chose it in the settings view, and it is remembered.</summary>
    Stored,
}

/// <summary>Every knob this application has, as numbers. Names, ranges and defaults live with the resolver.</summary>
public sealed record AppSettings(int McpPort, int SnapshotEvery);

/// <summary>The knobs in force, each with where it came from.</summary>
public sealed record ResolvedSettings(AppSettings Values, SettingSource McpPortSource, SettingSource SnapshotSource);

/// <summary>
/// Resolves the knobs and remembers the ones a person chose. Precedence is the settings view first,
/// the environment second, the built-in default last: an environment variable is how a script or a
/// portable copy says something once, and a person who went to the settings view meant it.
/// The file sits beside the journal — a preference is not domain state and must not be undoable.
/// </summary>
public sealed class SettingsResolver(string directory, Func<string, string?> environment, AppSettings defaults)
{
    public const string McpPortVariable = "WORKSHEET_MCP_PORT";

    public const string SnapshotVariable = "WORKSHEET_SNAPSHOT_EVERY";

    private const string FileName = "settings.json";

    private readonly Func<string, string?> _environment = environment ?? throw new ArgumentNullException(nameof(environment));

    private readonly AppSettings _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));

    private string FilePath => Path.Combine(directory, FileName);

    /// <summary>What is in force when nothing was chosen and the environment says nothing.</summary>
    public AppSettings Defaults => _defaults;

    public ResolvedSettings Resolve()
    {
        var stored = Read();

        var port = Choose(stored?.McpPort, McpPortVariable, _defaults.McpPort, Port, out var portSource);
        var every = Choose(stored?.SnapshotEvery, SnapshotVariable, _defaults.SnapshotEvery, Every, out var everySource);

        return new ResolvedSettings(new AppSettings(port, every), portSource, everySource);
    }

    /// <summary>
    /// Why these values cannot be remembered, in the words a person reads, or nothing when they
    /// can. The settings view asks this before it writes, so a refusal sounds the same everywhere.
    /// </summary>
    public static string? WhyNot(AppSettings values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (Port(values.McpPort.ToString(CultureInfo.InvariantCulture)) is null)
        {
            return "The MCP port is 0 to 65535; zero turns the server off.";
        }

        return Every(values.SnapshotEvery.ToString(CultureInfo.InvariantCulture)) is null
            ? "The snapshot interval is 0 to 100000 changes; zero keeps the journal alone."
            : null;
    }

    /// <summary>
    /// Remembers a choice made in the settings view. A value out of range is refused here rather
    /// than written as nothing: a knob that quietly forgets what it was given is worse than a refusal.
    /// </summary>
    public void Store(AppSettings values)
    {
        if (WhyNot(values) is { } problem)
        {
            throw new ArgumentOutOfRangeException(nameof(values), problem);
        }

        Directory.CreateDirectory(directory);

        File.WriteAllText(
            FilePath,
            JsonSerializer.Serialize(new StoredSettings
            {
                McpPort = values.McpPort,
                SnapshotEvery = values.SnapshotEvery,
                CloseNoticeShown = HasShownCloseNotice,
            }));
    }

    /// <summary>
    /// Whether the application has already told this person that closing the window leaves it
    /// running. Kept beside the knobs rather than among them: it is something the application
    /// remembers about the person, not something the person chose.
    /// </summary>
    public bool HasShownCloseNotice => Read()?.CloseNoticeShown ?? false;

    /// <summary>Remembers that the notice has been given, leaving every knob as it was.</summary>
    public void RememberCloseNotice()
    {
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            FilePath,
            JsonSerializer.Serialize((Read() ?? new StoredSettings()) with { CloseNoticeShown = true }));
    }

    /// <summary>
    /// The value in force for one knob, and where it came from. A stored value is read back through
    /// the same check as an environment variable: a hand-edited file is not trusted either.
    /// </summary>
    private int Choose(int? stored, string variable, int fallback, Func<string?, int?> read, out SettingSource source)
    {
        if (read(stored?.ToString(CultureInfo.InvariantCulture)) is { } chosen)
        {
            source = SettingSource.Stored;
            return chosen;
        }

        if (read(_environment(variable)) is { } fromEnvironment)
        {
            source = SettingSource.Environment;
            return fromEnvironment;
        }

        source = SettingSource.Default;
        return fallback;
    }

    /// <summary>
    /// Forgets what was chosen here, so the environment and the built-in value decide again. Writing
    /// the default value down instead would keep the environment out for good, which is not what
    /// reverting means.
    /// </summary>
    public void Forget()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    private StoredSettings? Read()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(FilePath))
                : null;
        }
        catch (JsonException)
        {
            // A file someone edited by hand is a choice to start over, not a reason to fail to start.
            return null;
        }
    }

    private static int? Port(string? text) => Number(text, port => port is >= 0 and <= 65535);

    private static int? Every(string? text) => Number(text, every => every is >= 0 and <= 100_000);

    private static int? Number(string? text, Func<int, bool> acceptable) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && acceptable(value)
            ? value
            : null;

    private sealed record StoredSettings
    {
        [JsonPropertyName("mcpPort")]
        public int? McpPort { get; init; }

        [JsonPropertyName("snapshotEvery")]
        public int? SnapshotEvery { get; init; }

        [JsonPropertyName("closeNoticeShown")]
        public bool? CloseNoticeShown { get; init; }
    }
}
