// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;
using Worksheet.Mcp;

namespace Worksheet.Tests;

/// <summary>
/// What a person pastes into a client. The two shapes exist because the clients disagree on the
/// key, and a snippet under the wrong key fails without saying why.
/// </summary>
public sealed class ConnectInfoTests
{
    private static readonly McpSettings Settings = new(5317);

    private static JsonElement Server(string snippet, string key) =>
        JsonDocument.Parse(snippet).RootElement.GetProperty(key).GetProperty(ConnectInfo.ServerKey);

    [Fact]
    public void The_vscode_snippet_names_an_http_server_at_the_endpoint()
    {
        var server = Server(ConnectInfo.VsCode(Settings), "servers");

        Assert.Equal("http", server.GetProperty("type").GetString());
        Assert.Equal("http://127.0.0.1:5317/mcp", server.GetProperty("url").GetString());
    }

    [Fact]
    public void The_other_snippet_uses_the_key_most_clients_read()
    {
        var server = Server(ConnectInfo.OtherClients(Settings), "mcpServers");

        Assert.Equal("http://127.0.0.1:5317/mcp", server.GetProperty("url").GetString());
    }

    [Fact]
    public void The_snippet_follows_the_port()
    {
        var server = Server(ConnectInfo.VsCode(new McpSettings(6000)), "servers");

        Assert.Equal("http://127.0.0.1:6000/mcp", server.GetProperty("url").GetString());
    }
}
