// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json.Nodes;

namespace Worksheet.Mcp;

/// <summary>
/// What a person pastes to connect a client, and where it points. Two shapes, because the clients
/// disagree on the key above the server's name: VS Code reads <c>servers</c>, most other clients
/// read <c>mcpServers</c> — and a snippet that is nearly right is worse than none.
/// </summary>
public static class ConnectInfo
{
    public const string ServerKey = "worksheet";

    public static string VsCode(McpSettings settings) => Snippet(settings, "servers");

    public static string OtherClients(McpSettings settings) => Snippet(settings, "mcpServers");

    private static string Snippet(McpSettings settings, string key) =>
        new JsonObject
        {
            [key] = new JsonObject
            {
                [ServerKey] = new JsonObject
                {
                    ["type"] = "http",
                    ["url"] = settings.Url,
                },
            },
        }
        .ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
}
