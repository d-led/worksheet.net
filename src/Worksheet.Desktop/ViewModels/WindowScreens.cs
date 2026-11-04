// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// The screens the window can open, which the shell owns. The tray menu offers them and so does the
/// window's own heading: a menu-bar icon is not a way into anything on a platform without one, and
/// on any platform a menu bar is not where a person looks first.
/// </summary>
/// <remarks>
/// A heading item carries the name of the screen it opens, because a flyout is outside the window's
/// data context and so cannot bind to these actions.
/// </remarks>
public sealed record WindowScreens(Action Settings, Action Manage, Action Connect, Action Audit, Action DataFolder)
{
    private readonly Dictionary<string, Action> _byName = new()
    {
        ["OpenSettings"] = Settings,
        ["OpenManage"] = Manage,
        ["OpenConnect"] = Connect,
        ["OpenAudit"] = Audit,
        ["OpenDataFolder"] = DataFolder,
    };

    /// <summary>Opens the screen a heading item names, and fails loudly if none answers to it.</summary>
    public void Open(string name) => _byName[name]();
}
