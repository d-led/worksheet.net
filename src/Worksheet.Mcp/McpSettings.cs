// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;

namespace Worksheet.Mcp;

/// <summary>
/// Where the MCP server listens. The environment decides for now — a settings view with the same
/// knobs is a later step — and a port of zero is how it is turned off.
/// </summary>
public sealed record McpSettings(int Port)
{
    public const int DefaultPort = 5317;

    public const string PortVariable = "WORKSHEET_MCP_PORT";

    public static McpSettings FromEnvironment() =>
        new(PortFrom(Environment.GetEnvironmentVariable(PortVariable)));

    /// <summary>
    /// The port the variable asks for, or the default when it says nothing a port could be. A
    /// mistyped variable must not leave the application without its server, or without a window.
    /// </summary>
    public static int PortFrom(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) && port is >= 0 and <= 65535
            ? port
            : DefaultPort;

    public bool Enabled => Port > 0;

    public string Url => $"http://127.0.0.1:{Port}{McpHttpHost.Endpoint}";
}
