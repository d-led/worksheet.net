// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Desktop.ViewModels;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>
/// The settings view: it starts from what is in force, says where that came from, refuses a value
/// that is not one, and answers what the application should apply when it saves.
/// </summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private static readonly AppSettings Defaults = new(McpPort: 5317, SnapshotEvery: 500);

    private readonly TempDirectory _temp = new("worksheet-settings-view");

    public void Dispose() => _temp.Dispose();

    private SettingsResolver Resolver(params (string Name, string Value)[] environment) =>
        new(_temp.Path, variable => environment.FirstOrDefault(pair => pair.Name == variable).Value, Defaults);

    private SettingsViewModel View(SettingsResolver resolver) => new(resolver, _temp.Path);

    [Fact]
    public void The_view_starts_from_what_is_in_force_and_says_where_it_came_from()
    {
        var view = View(Resolver((SettingsResolver.McpPortVariable, "6000")));

        Assert.Equal("6000", view.PortText);
        Assert.Equal($"from {SettingsResolver.McpPortVariable}", view.PortSource);
        Assert.Equal("built-in", view.SnapshotSource);
    }

    [Fact]
    public void A_field_that_is_not_a_number_is_refused_with_a_sentence()
    {
        var view = View(Resolver());
        view.PortText = "sixty";

        Assert.Null(view.Save());
        Assert.Equal("Both fields take a whole number.", view.Message);
        Assert.True(view.MessageIsProblem);
    }

    [Fact]
    public void A_port_that_cannot_be_a_port_is_refused_with_its_range()
    {
        var view = View(Resolver());
        view.PortText = "70000";

        Assert.Null(view.Save());
        Assert.Equal("The MCP port is 0 to 65535; zero turns the server off.", view.Message);
    }

    [Fact]
    public void Saving_remembers_the_choice_and_answers_what_to_apply()
    {
        var resolver = Resolver((SettingsResolver.McpPortVariable, "6000"));
        var view = View(resolver);
        view.PortText = "7000";

        var applied = view.Save();

        Assert.Equal(new AppSettings(McpPort: 7000, SnapshotEvery: 500), applied);
        Assert.Equal("chosen here", view.PortSource);
        Assert.Equal("Saved.", view.Message);
        Assert.Equal(7000, resolver.Resolve().Values.McpPort);
    }

    [Fact]
    public void Reverting_forgets_the_choice_and_shows_what_decides_now()
    {
        var resolver = Resolver((SettingsResolver.McpPortVariable, "6000"));
        var view = View(resolver);
        view.PortText = "7000";
        view.Save();

        view.Revert();

        Assert.Equal("6000", view.PortText);
        Assert.Equal($"from {SettingsResolver.McpPortVariable}", view.PortSource);
        Assert.False(view.MessageIsProblem);
    }
}
