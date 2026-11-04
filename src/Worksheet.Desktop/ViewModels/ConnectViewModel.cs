// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Mcp;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// What the connect window shows: whether the server is listening, and the two snippets a client
/// needs. It holds no state of its own — the answer is fixed when the window opens.
/// </summary>
public sealed class ConnectViewModel(McpSettings settings, string status, bool isProblem)
{
    public string Status => status;

    public bool IsProblem => isProblem;

    public string VsCodeConfig => ConnectInfo.VsCode(settings);

    public string OtherConfig => ConnectInfo.OtherClients(settings);
}
