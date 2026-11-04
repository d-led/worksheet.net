// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Net;
using System.Text;

namespace Worksheet.Mcp;

/// <summary>
/// The smallest transport that carries a body: loopback only, one endpoint, POST in and JSON out.
/// A wrong URL is answered with the sentence naming the right one, because whoever is connecting
/// has a configuration file open in the other window.
/// </summary>
public sealed class McpHttpHost : IDisposable
{
    public const string Endpoint = "/mcp";

    private readonly McpServer _server;
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _stopping = new();

    private McpHttpHost(McpServer server, HttpListener listener, int port)
    {
        _server = server;
        _listener = listener;
        Port = port;
        _ = Task.Run(() => Listen(_stopping.Token));
    }

    public int Port { get; }

    /// <summary>
    /// Starts listening on the loopback. A port another program already holds is a sentence for
    /// the window, not an exception out of the application.
    /// </summary>
    public static McpHttpHost? TryStart(McpServer server, int port, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(server);

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        try
        {
            listener.Start();
        }
        catch (HttpListenerException error)
        {
            listener.Close();
            problem = $"Port {port} is taken, so the MCP server is not running: {error.Message}";
            return null;
        }

        problem = null;

        return new McpHttpHost(server, listener, port);
    }

    /// <summary>
    /// What one exchange is answered with, decided without a socket. Everything but a POST to the
    /// endpoint is refused with the sentence that says what to do instead.
    /// </summary>
    public static (int Status, string? Body) Exchange(McpServer server, string method, string path, string? body)
    {
        if (!string.Equals(path.Split('?')[0], Endpoint, StringComparison.Ordinal))
        {
            return (404, $"The MCP endpoint is {Endpoint}.");
        }

        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return (405, $"The MCP endpoint takes POST, not {method}.");
        }

        return server.Answer(body) is { } answer ? (200, answer) : (202, null);
    }

    private async Task Listen(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => Answer(context), CancellationToken.None);
        }
    }

    private void Answer(HttpListenerContext context)
    {
        try
        {
            var body = Read(context.Request);

            var (status, answer) = Exchange(
                _server,
                context.Request.HttpMethod,
                context.Request.Url?.AbsolutePath ?? "/",
                body);

            context.Response.StatusCode = status;

            if (answer is not null)
            {
                Write(context.Response, answer);
            }
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static string Read(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private static void Write(HttpListenerResponse response, string answer)
    {
        var bytes = Encoding.UTF8.GetBytes(answer);

        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes);
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _listener.Close();
        _stopping.Dispose();
    }
}
