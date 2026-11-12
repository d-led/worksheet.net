// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using Avalonia;
using Worksheet.Core;

namespace Worksheet.Desktop;

internal static class Program
{
    /// <summary>How Avalonia's macOS backend names the frame timer it cannot create.</summary>
    private const string FrameTimerMarker = "RenderTimer";

    [STAThread]
    public static int Main(string[] args)
    {
        // The interface is English, so the process's culture is pinned before anything is drawn:
        // the calendar a person picks a day from is a control of someone else's, and it words its
        // months from the machine unless told otherwise. Dates.Names keeps the orderings in it too.
        CultureInfo.DefaultThreadCurrentCulture = Dates.English;
        CultureInfo.DefaultThreadCurrentUICulture = Dates.English;

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
                "Worksheet.net could not open a window: the display is asleep or unavailable."
                + Environment.NewLine
                + "Wake the screen and start it again.");

            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // No Dock icon: this is a menu-bar application, and a Dock icon it can never hear a click
            // on would only take a place in the Dock and answer nothing. Said here rather than
            // inherited, because Avalonia's default is to show one.
            .With(new MacOSPlatformOptions { ShowInDock = false })
            .LogToTrace();
}
