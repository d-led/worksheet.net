// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// What the window needs from the shell to put itself aside honestly: whether the person has
/// already been told that closing leaves the application running, and the way to tell them — a
/// window that vanishes without a word reads as a window that was closed for good.
/// </summary>
public sealed record ClosingProcedure(Func<bool> Noticed, Action<Window> Notice);
