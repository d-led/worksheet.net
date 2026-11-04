// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>
/// The weekly copies: one per week that closed, four kept, named so that a week can be found by its
/// name and a restore is putting the file back.
/// </summary>
public sealed class WeeklyBackupsTests : IDisposable
{
    /// <summary>A Thursday, so the week that closed is the one before it.</summary>
    private static readonly DateTimeOffset Thursday = new(2026, 11, 5, 9, 0, 0, TimeSpan.FromHours(1));

    private readonly TempDirectory _temp = new("worksheet-backups");

    private DateTimeOffset _now = Thursday;

    public void Dispose() => _temp.Dispose();

    private WeeklyBackups Backups =>
        new(_temp.Path, target => File.WriteAllText(target, "a copy"), () => _now);

    private string[] Copied => [.. Directory.GetFiles(_temp.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];

    [Fact]
    public void The_week_that_closed_is_copied_beside_the_journal()
    {
        var written = Backups.Take();

        Assert.Equal("ledger-2026-W44.db", Path.GetFileName(written));
        Assert.Equal(["ledger-2026-W44.db"], Copied);
    }

    [Fact]
    public void A_week_is_copied_once()
    {
        var backups = Backups;

        Assert.NotNull(backups.Take());
        Assert.Null(backups.Take());

        Assert.Single(Copied);
    }

    [Fact]
    public void On_a_monday_morning_the_week_that_just_ended_is_the_one_copied()
    {
        _now = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal("ledger-2026-W44.db", Path.GetFileName(Backups.Take()));
    }

    [Fact]
    public void A_sunday_still_belongs_to_the_week_that_is_running()
    {
        _now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal("ledger-2026-W43.db", Path.GetFileName(Backups.Take()));
    }

    [Fact]
    public void Only_the_last_four_weeks_stay_on_disk()
    {
        var backups = Backups;

        for (var week = 0; week < 5; week++)
        {
            _now = Thursday.AddDays(7 * week);
            backups.Take();
        }

        Assert.Equal(
            ["ledger-2026-W45.db", "ledger-2026-W46.db", "ledger-2026-W47.db", "ledger-2026-W48.db"],
            Copied);
    }

    [Fact]
    public void A_copy_that_fails_leaves_the_older_ones_alone()
    {
        var broken = new WeeklyBackups(_temp.Path, _ => throw new IOException("no room"), () => _now);

        Assert.Throws<IOException>(() => broken.Take());
        Assert.Empty(Copied);
    }
}
