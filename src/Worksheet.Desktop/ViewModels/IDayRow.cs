// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// A line of the day's table: a block that was worked, or a hole between two of them. The two are
/// different things to read and to edit, so they stay different types; what they share is only the
/// place they take in the list.
/// </summary>
public interface IDayRow;
