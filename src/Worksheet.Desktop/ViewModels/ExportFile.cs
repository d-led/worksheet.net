// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Desktop.ViewModels;

/// <summary>One export, ready to be saved: the name it takes and the zip a save dialog writes.</summary>
public sealed record ExportFile(string FileName, byte[] Bytes);
