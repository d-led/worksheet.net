// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Worksheet.Desktop.Components;
using Worksheet.Desktop.ViewModels;
using Worksheet.Mcp;

namespace Worksheet.UiTests;

/// <summary>
/// The connect window's panel: whether the server is up, and the two snippets a client needs.
/// It needs no ledger, so it stands alone rather than borrowing the day's harness.
/// </summary>
public sealed class ConnectPanelTests
{
    private static ConnectPanel Opened(string status, bool isProblem)
    {
        var panel = new ConnectPanel
        {
            DataContext = new ConnectViewModel(new McpSettings(5317), status, isProblem),
        };

        var window = new Window { Content = panel, Width = 720, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    [AvaloniaFact]
    public void The_panel_says_where_the_server_listens_and_what_to_paste()
    {
        var panel = Opened("Listening on http://127.0.0.1:5317/mcp.", isProblem: false);

        Assert.Equal("Listening on http://127.0.0.1:5317/mcp.", panel.FindControl<TextBlock>("Status")!.Text);
        Assert.Contains("\"servers\"", panel.FindControl<TextBox>("VsCodeConfig")!.Text);
        Assert.Contains("\"mcpServers\"", panel.FindControl<TextBox>("OtherConfig")!.Text);
    }

    [AvaloniaFact]
    public void Both_snippets_point_at_the_same_endpoint()
    {
        var panel = Opened("Listening.", isProblem: false);

        Assert.Contains("http://127.0.0.1:5317/mcp", panel.FindControl<TextBox>("VsCodeConfig")!.Text);
        Assert.Contains("http://127.0.0.1:5317/mcp", panel.FindControl<TextBox>("OtherConfig")!.Text);
    }

    [AvaloniaFact]
    public void A_server_that_is_not_running_is_said_in_the_panel()
    {
        var panel = Opened("Port 5317 is taken.", isProblem: true);

        var status = panel.FindControl<TextBlock>("Status")!;

        Assert.Equal("Port 5317 is taken.", status.Text);
        Assert.Contains("problem", status.Classes);
    }
}
