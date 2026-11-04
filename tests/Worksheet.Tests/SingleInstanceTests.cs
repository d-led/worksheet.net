// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Desktop;

namespace Worksheet.Tests;

/// <summary>A second copy of the application must not fight the first over the store.</summary>
public sealed class SingleInstanceTests
{
    /// <summary>A short name per test: a pipe's path carries the temporary folder, and macOS caps it.</summary>
    private static string NewName() => $"W{Guid.NewGuid():N}"[..12];

    [Fact]
    public void The_first_copy_takes_the_instance()
    {
        using var instance = SingleInstance.TryAcquire(NewName());

        Assert.NotNull(instance);
    }

    [Fact]
    public void A_second_copy_is_refused_while_the_first_is_running()
    {
        var name = NewName();
        using var first = SingleInstance.TryAcquire(name);

        var second = SingleInstance.TryAcquire(name);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void Quitting_lets_the_next_copy_start()
    {
        var name = NewName();
        var first = SingleInstance.TryAcquire(name);
        Assert.NotNull(first);

        first.Dispose();
        using var second = SingleInstance.TryAcquire(name);

        Assert.NotNull(second);
    }

    [Fact]
    public void Different_instances_do_not_block_each_other()
    {
        using var first = SingleInstance.TryAcquire(NewName());
        using var second = SingleInstance.TryAcquire(NewName());

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [Fact]
    public void A_second_copy_asks_the_running_one_for_its_window()
    {
        var name = NewName();
        using var first = SingleInstance.TryAcquire(name);
        using var heard = new ManualResetEventSlim();

        Assert.NotNull(first);
        first.Summoned += (_, _) => heard.Set();

        SingleInstance.Summon(name);

        Assert.True(heard.Wait(TimeSpan.FromSeconds(10)), "the running copy heard the second");
    }

    [Fact]
    public void Summoning_a_window_that_is_not_there_is_not_an_error()
    {
        Assert.Null(Record.Exception(() => SingleInstance.Summon(NewName())));
    }
}
