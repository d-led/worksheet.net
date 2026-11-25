// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;

namespace Worksheet.Desktop.Components;

/// <summary>
/// The day's work, project by project, as one chip per project: the pastel its rows carry, its
/// name, and the hours it took. It shows nothing on a day with no work on it.
/// </summary>
public partial class ProjectTotals : UserControl
{
    public ProjectTotals() => InitializeComponent();
}
