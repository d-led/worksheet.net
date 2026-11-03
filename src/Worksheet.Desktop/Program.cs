// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;

namespace Worksheet.Desktop;

internal static class Program
{
    /// <summary>How Avalonia's macOS backend names the frame timer it cannot create.</summary>
    private const string FrameTimerMarker = "RenderTimer";

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains(FrameTimerMarker, StringComparison.Ordinal))
        {
            // macOS times frames with a display link, and refuses to make one while the screen is
            // asleep. That happens before any window exists, so a stack trace says nothing useful.
            Console.Error.WriteLine(
                "Worksheet could not open a window: the display is asleep or unavailable."
                + Environment.NewLine
                + "Wake the screen and start it again.");

            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
