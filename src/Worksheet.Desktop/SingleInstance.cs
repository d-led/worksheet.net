// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.IO.Pipes;

namespace Worksheet.Desktop;

/// <summary>
/// One running copy. The store is single-writer, so a second copy could only fail on the file lock;
/// it asks this first, and rather than start it asks the copy that is running for its window and
/// leaves. That is what makes starting the application again — a double click on its icon — bring
/// the window back, on any platform.
/// </summary>
/// <remarks>
/// The ask travels over a named pipe rather than a named event: .NET implements a named
/// synchronization primitive only on Windows, while a named pipe is a stream on every platform this
/// application ships for. The mutex stays, because that is what decides which copy is the first.
/// macOS caps the path of a socket at 104 characters and the pipe's path carries the temporary
/// folder, so the instance name stays short.
/// </remarks>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _closed = new();
    private readonly Task _listening;

    private SingleInstance(Mutex mutex, string name)
    {
        _mutex = mutex;
        _listening = Announce(name);
    }

    /// <summary>Raised when another copy asks for the window. On a pool thread, not the UI's.</summary>
    public event EventHandler? Summoned;

    /// <summary>Takes the instance, or nothing when another copy holds it.</summary>
    public static SingleInstance? TryAcquire(string name)
    {
        var mutex = new Mutex(initiallyOwned: true, name, out var isFirst);

        if (!isFirst)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstance(mutex, name);
    }

    /// <summary>Asks the running copy to show itself; nothing happens when no copy is running.</summary>
    public static void Summon(string name)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", Pipe(name), PipeDirection.Out);
            client.Connect(500);
        }
        catch (Exception failure) when (failure is IOException or TimeoutException or ArgumentException)
        {
            // Nobody is listening, or this platform cannot hold the pipe at all: either way there is
            // no window to ask for, and a second copy that cannot ask simply leaves.
        }
    }

    private static string Pipe(string name) => $"{name}.window";

    /// <summary>Starts listening for a second copy's ask. A pipe this platform will not give is no reason to fail to start.</summary>
    private Task Announce(string name)
    {
        try
        {
            return Listen(Pipe(name), Waiting(Pipe(name)));
        }
        catch (Exception failure) when (failure is IOException or ArgumentException)
        {
            return Task.CompletedTask;
        }
    }

    private static NamedPipeServerStream Waiting(string pipe) =>
        new(pipe, PipeDirection.In, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

    private async Task Listen(string pipe, NamedPipeServerStream waiting)
    {
        while (!_closed.IsCancellationRequested)
        {
            try
            {
                await using (waiting)
                {
                    await waiting.WaitForConnectionAsync(_closed.Token);

                    Summoned?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // The other end left without asking again; keep listening.
            }

            waiting = Waiting(pipe);
        }
    }

    public void Dispose()
    {
        _closed.Cancel();
        _listening.Wait(TimeSpan.FromSeconds(5));
        _closed.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
