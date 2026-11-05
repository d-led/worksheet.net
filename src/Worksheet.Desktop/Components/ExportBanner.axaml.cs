// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;

namespace Worksheet.Desktop.Components;

/// <summary>
/// Where the last export went: the file, its folder, and the one thing worth doing about it. It
/// holds no behaviour of its own — the day says what was exported and the window says how a file is
/// shown — so the template can read as the sentence it puts on screen.
/// </summary>
public partial class ExportBanner : UserControl
{
    public ExportBanner() => InitializeComponent();
}
