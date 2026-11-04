// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Worksheet.Core;
using Worksheet.Mcp;

namespace Worksheet.Tests;

/// <summary>
/// The transport: what one exchange is answered with, where the port comes from, and that the
/// thing really listens on the loopback and answers a client over HTTP.
/// </summary>
public sealed class McpHostTests
{
    private static McpServer Server() => new(new WorksheetTools(Core.Ledger.Open(new InMemoryJournal())));

    private const string ToolList = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";

    [Fact]
    public void A_post_to_the_endpoint_is_answered_with_the_servers_answer()
    {
        var (status, body) = McpHttpHost.Exchange(Server(), "POST", "/mcp", ToolList);

        Assert.Equal(200, status);
        Assert.Contains("log_entry", body);
    }

    [Fact]
    public void A_query_string_does_not_put_the_endpoint_off()
    {
        var (status, _) = McpHttpHost.Exchange(Server(), "POST", "/mcp?session=1", ToolList);

        Assert.Equal(200, status);
    }

    [Fact]
    public void A_notification_is_answered_with_no_body()
    {
        var (status, body) = McpHttpHost.Exchange(
            Server(),
            "POST",
            "/mcp",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        Assert.Equal(202, status);
        Assert.Null(body);
    }

    [Fact]
    public void A_get_is_refused_with_what_the_endpoint_takes()
    {
        var (status, body) = McpHttpHost.Exchange(Server(), "GET", "/mcp", body: null);

        Assert.Equal(405, status);
        Assert.Equal("The MCP endpoint takes POST, not GET.", body);
    }

    [Fact]
    public void Another_path_is_told_where_the_endpoint_is()
    {
        var (status, body) = McpHttpHost.Exchange(Server(), "POST", "/", ToolList);

        Assert.Equal(404, status);
        Assert.Equal("The MCP endpoint is /mcp.", body);
    }

    [Fact]
    public void Without_a_port_setting_the_default_port_is_used()
    {
        Assert.Equal(McpSettings.DefaultPort, McpSettings.PortFrom(null));
        Assert.Equal(McpSettings.DefaultPort, McpSettings.PortFrom("   "));
    }

    [Fact]
    public void A_port_that_is_not_a_port_falls_back_to_the_default()
    {
        Assert.Equal(McpSettings.DefaultPort, McpSettings.PortFrom("fifty"));
        Assert.Equal(McpSettings.DefaultPort, McpSettings.PortFrom("70000"));
    }

    [Fact]
    public void Zero_turns_the_server_off()
    {
        var settings = new McpSettings(0);

        Assert.False(settings.Enabled);
    }

    [Fact]
    public void The_url_names_the_loopback_and_the_endpoint()
    {
        Assert.Equal("http://127.0.0.1:5317/mcp", new McpSettings(5317).Url);
    }

    [Fact]
    public async Task The_server_answers_a_tool_list_over_http()
    {
        var port = FreePort();
        using var host = McpHttpHost.TryStart(Server(), port, out var problem);
        using var client = new HttpClient();

        var response = await client.PostAsync(
            $"http://127.0.0.1:{port}/mcp",
            new StringContent(ToolList, Encoding.UTF8, "application/json"));

        Assert.Null(problem);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("log_entry", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void A_port_someone_else_holds_is_a_sentence_rather_than_a_crash()
    {
        var port = FreePort();
        using var first = McpHttpHost.TryStart(Server(), port, out _);

        var second = McpHttpHost.TryStart(Server(), port, out var problem);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Contains($"Port {port} is taken", problem);
    }

    /// <summary>A port the machine is not using: taken and released, so the number is known to be free.</summary>
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }
}
