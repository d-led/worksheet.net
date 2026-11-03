// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Worksheet.Core;
using Worksheet.Desktop;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;
using Worksheet.Storage;

// A day of work for the documentation: eight hours over three projects and a forty-five
// minute break, rendered from the real window rather than drawn by hand.
//
//     dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/day.png
//     dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/frozen-day.png sick
//     dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/completion.png working "Client portal ("

var output = args.Length > 0 ? args[0] : "docs/screenshots/day.png";
var kind = args.Length > 1 ? Enum.Parse<DayKind>(args[1], ignoreCase: true) : DayKind.Working;
var typing = args.Length > 2 ? args[2] : null;
var dataDirectory = Path.Combine(Path.GetTempPath(), "worksheet-screenshot", Guid.NewGuid().ToString("N"));

AppBuilder.Configure<App>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia()
    .SetupWithoutStarting();

using var local = LocalLedger.Open(dataDirectory, SnapshotPolicy.Disabled);

// A fixed Monday: the picture must not change with the day it is rendered on, and a weekend
// would freeze the timetable it is meant to show.
var today = new DateOnly(2026, 11, 2);

foreach (var line in TheDay())
{
    var parse = EntryCommandParser.Parse(line, today);

    if (!parse.Succeeded)
    {
        throw new InvalidOperationException($"The example day is not valid: {parse.Error}");
    }

    var composed = new EntryComposer(local.Ledger).Compose(parse.Command!);

    if (!composed.Created)
    {
        throw new InvalidOperationException(string.Join(" ", composed.Problems));
    }
}

if (kind != DayKind.Working)
{
    new DayCalendar(local.Ledger).Declare(today, kind);
}

var day = new DayViewModel(local, today);

if (typing is not null)
{
    day.EntryText = typing;
}

var window = new MainWindow
{
    DataContext = day,
    Width = 940,
    Height = 620,
};

window.Show();
Dispatcher.UIThread.RunJobs();

var frame = window.CaptureRenderedFrame()
    ?? throw new InvalidOperationException("The headless platform rendered no frame.");

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
frame.Save(output, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

Console.WriteLine($"Wrote {output} (total {day.Total})");
Directory.Delete(dataDirectory, recursive: true);

// Eight hours of work across three projects, a 45 minute break, and one hole left in the day.
static IEnumerable<string> TheDay()
{
    yield return "Client portal (login) 08:30-11:00";
    yield return "Client portal (reports) 11:00-12:30";
    yield return "Break 12:30 45m";
    yield return "Mobile app (checkout) 13:30-15:00";
    yield return "Internal (onboarding) 15:00 2h30m";
}
