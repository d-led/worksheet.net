// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Mcp;

/// <summary>
/// Where the MCP server listens, and how the rest of the application talks about it. The port is
/// resolved where the other settings are; a port of zero is how the server is turned off.
/// </summary>
public sealed record McpSettings(int Port)
{
    public const int DefaultPort = 5317;

    public bool Enabled => Port > 0;

    public string Url => $"http://127.0.0.1:{Port}{McpHttpHost.Endpoint}";
}
