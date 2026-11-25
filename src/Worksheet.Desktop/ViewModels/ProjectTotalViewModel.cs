// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Media;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// One project's share of the day as the strip under it reads it: the name, the hours already
/// worded, and the pastel the project's rows carry, so a share is recognised before it is read.
/// </summary>
public sealed record ProjectTotalViewModel(string Project, string Length, IBrush? Tint);
