// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using SukiUI;
using Worksheet.UiTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Worksheet.UiTests;

/// <summary>
/// The application the UI tests drive: the real theme and the real token styles, but none of the
/// real start-up — no store, no tray, no single-instance guard. Tests build the one window they
/// need instead of running the application.
/// </summary>
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new SukiTheme());

        // The same token file the application uses, so the tests see what a user sees.
        Styles.Add(new StyleInclude(new Uri("avares://Worksheet.Desktop/"))
        {
            Source = new Uri("avares://Worksheet.Desktop/Styles/Tokens.axaml"),
        });
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
