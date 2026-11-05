// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;

namespace Worksheet.Desktop;

/// <summary>
/// Shows a file where it is rather than opening it: an export is a zip, and a file manager that
/// unpacks it on a double click has not shown the person where their file went. The platform is an
/// argument so the three shapes can be read at once, and tested on the machine that runs them.
/// </summary>
internal static class Reveal
{
    public static void Show(string path) =>
        Process.Start(StartInfoFor(path, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()))?.Dispose();

    internal static ProcessStartInfo StartInfoFor(string path, bool windows, bool macOS) =>
        windows ? new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }
        : macOS ? new ProcessStartInfo("open", ["-R", path]) { UseShellExecute = true }
        : new ProcessStartInfo("xdg-open", [Path.GetDirectoryName(path) ?? "."]) { UseShellExecute = true };
}
