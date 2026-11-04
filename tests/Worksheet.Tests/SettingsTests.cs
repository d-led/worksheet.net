// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>
/// The knobs and where their values come from: what a person chose, else the environment, else the
/// built-in value — and a file that can be read back after a restart.
/// </summary>
public sealed class SettingsTests : IDisposable
{
    private readonly TempDirectory _temp = new("worksheet-settings");

    /// <summary>The built-in values, as the application hands them in: the port from MCP, the rest here.</summary>
    private static readonly AppSettings Defaults = new(McpPort: 5317, SnapshotEvery: 500);

    public void Dispose() => _temp.Dispose();

    private SettingsResolver Resolved(params (string Name, string Value)[] environment) =>
        new(_temp.Path, variable => environment.FirstOrDefault(pair => pair.Name == variable).Value, Defaults);

    [Fact]
    public void With_nothing_said_the_built_in_values_are_in_force()
    {
        var resolved = Resolved().Resolve();

        Assert.Equal(Defaults, resolved.Values);
        Assert.Equal(SettingSource.Default, resolved.McpPortSource);
        Assert.Equal(SettingSource.Default, resolved.SnapshotSource);
    }

    [Fact]
    public void The_environment_sets_a_knob_and_says_so()
    {
        var resolved = Resolved((SettingsResolver.McpPortVariable, "6000")).Resolve();

        Assert.Equal(6000, resolved.Values.McpPort);
        Assert.Equal(SettingSource.Environment, resolved.McpPortSource);
        Assert.Equal(Defaults.SnapshotEvery, resolved.Values.SnapshotEvery);
    }

    [Fact]
    public void A_choice_in_the_settings_view_beats_the_environment()
    {
        Resolved().Store(new AppSettings(McpPort: 7000, SnapshotEvery: 100));

        var resolved = Resolved((SettingsResolver.McpPortVariable, "6000")).Resolve();

        Assert.Equal(7000, resolved.Values.McpPort);
        Assert.Equal(SettingSource.Stored, resolved.McpPortSource);
        Assert.Equal(100, resolved.Values.SnapshotEvery);
    }

    [Fact]
    public void A_choice_survives_a_restart()
    {
        Resolved().Store(new AppSettings(McpPort: 7000, SnapshotEvery: 100));

        Assert.Equal(7000, Resolved().Resolve().Values.McpPort);
    }

    [Fact]
    public void A_mistyped_environment_value_is_ignored()
    {
        var resolved = Resolved((SettingsResolver.McpPortVariable, "port seven thousand")).Resolve();

        Assert.Equal(Defaults.McpPort, resolved.Values.McpPort);
        Assert.Equal(SettingSource.Default, resolved.McpPortSource);
    }

    [Fact]
    public void A_port_outside_the_possible_range_is_ignored_wherever_it_came_from()
    {
        Assert.Equal(
            Defaults.McpPort,
            Resolved((SettingsResolver.McpPortVariable, "70000")).Resolve().Values.McpPort);

        File.WriteAllText(Path.Combine(_temp.Path, "settings.json"), """{"mcpPort": 70000}""");

        Assert.Equal(Defaults.McpPort, Resolved().Resolve().Values.McpPort);
    }

    [Fact]
    public void A_file_someone_edited_into_nonsense_is_ignored()
    {
        File.WriteAllText(Path.Combine(_temp.Path, "settings.json"), "{ not really json");

        Assert.Equal(Defaults, Resolved().Resolve().Values);
    }

    [Fact]
    public void Zero_is_a_choice_and_means_off()
    {
        Resolved().Store(new AppSettings(McpPort: 0, SnapshotEvery: 0));

        var resolved = Resolved((SettingsResolver.McpPortVariable, "6000")).Resolve();

        Assert.Equal(0, resolved.Values.McpPort);
        Assert.Equal(0, resolved.Values.SnapshotEvery);
    }

    [Fact]
    public void Forgetting_the_choice_lets_the_environment_decide_again()
    {
        var resolver = Resolved((SettingsResolver.McpPortVariable, "6000"));
        resolver.Store(new AppSettings(McpPort: 7000, SnapshotEvery: 100));

        resolver.Forget();

        var resolved = resolver.Resolve();

        Assert.Equal(6000, resolved.Values.McpPort);
        Assert.Equal(SettingSource.Environment, resolved.McpPortSource);
        Assert.Equal(Defaults.SnapshotEvery, resolved.Values.SnapshotEvery);
    }

    [Fact]
    public void A_value_out_of_range_is_not_written_rather_than_forgotten()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Resolved().Store(new AppSettings(McpPort: 70_000, SnapshotEvery: 500)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Resolved().Store(new AppSettings(McpPort: 5317, SnapshotEvery: -1)));
    }

    [Fact]
    public void The_close_notice_has_not_been_shown_before_the_first_close()
    {
        Assert.False(Resolved().HasShownCloseNotice);
    }

    [Fact]
    public void The_close_notice_is_remembered_across_a_restart()
    {
        Resolved().RememberCloseNotice();

        Assert.True(Resolved().HasShownCloseNotice);
    }

    [Fact]
    public void Choosing_a_port_does_not_make_the_close_notice_return()
    {
        Resolved().RememberCloseNotice();
        Resolved().Store(new AppSettings(McpPort: 7000, SnapshotEvery: 100));

        Assert.True(Resolved().HasShownCloseNotice);
    }
}
