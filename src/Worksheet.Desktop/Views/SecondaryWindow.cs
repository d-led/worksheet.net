// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SukiUI.Controls;

namespace Worksheet.Desktop.Views;

/// <summary>
/// A window that stands beside the day rather than in it — settings, the agent connection, the
/// catalogue. Escape is the way out, the way it is out of a field: a key a control has already
/// taken for itself never closes anything, so a name being typed is put back first and the same key
/// pressed again leaves.
/// </summary>
public class SecondaryWindow : SukiWindow
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.Key != Key.Escape)
        {
            return;
        }

        Close();

        e.Handled = true;
    }
}
