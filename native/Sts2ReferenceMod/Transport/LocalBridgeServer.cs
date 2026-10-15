using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sts2Emulator.Trace;
using Sts2ReferenceMod.Decision;

namespace Sts2ReferenceMod.Transport;

/// <summary>
/// Optional loopback-only authenticated protocol for future native control.
/// Transport threads never call native game APIs. The game thread must call
/// TryTakeAuthorizedAction and dispatch the returned command itself.
/// </summary>
public sealed class LocalBridgeServer : IAsyncDisposable
{
    private const int MaxActionBytes = 32 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpListener _server = new();
    private readonly DecisionCoordinator _controller;
    private readonly EventJournal _journal;
    private readonly byte[] _expectedToken;
    private readonly bool _spectatorOnly;
    private readonly NativeAgentBoundaryFeed? _agentFeed;
    private Task? _loop;

    public LocalBridgeServer(
        DecisionCoordinator controller, EventJournal journal, string bearerToken,
        int port = 39221, bool spectatorOnly = false,
        NativeAgentBoundaryFeed? agentFeed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearerToken);
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        _controller = controller;
        _journal = journal;
        _spectatorOnly = spectatorOnly;
        _agentFeed = agentFeed;
        if (spectatorOnly && agentFeed is null)
            throw new ArgumentException("Spectator transport requires a public feed.");
        _expectedToken = Encoding.UTF8.GetBytes("Bearer " + bearerToken);
        _server.Prefixes.Add("http://127.0.0.1:" + port + "/");
    }

    /// <summary>
    /// The native game uses spectator-only mode. No transport path can submit
    /// player actions, request pause/step, or serve privileged event journals.
    /// </summary>
    public LocalBridgeServer(NativeAgentBoundaryFeed feed, string bearerToken,
        int port = 39221)
        : this(new DecisionCoordinator(), new EventJournal(), bearerToken,
            port, spectatorOnly: true, agentFeed: feed) { }

    public void Start()
    {
        if (_loop is not null) throw new InvalidOperationException("Server already started.");
        _server.Start();
        _loop = Task.Run(ServeAsync);
    }

    private async Task ServeAsync()
    {
        while (_server.IsListening)
        {
            HttpListenerContext context;
            try { context = await _server.GetContextAsync().ConfigureAwait(false); }
            catch (HttpListenerException) when (!_server.IsListening) { break; }
            catch (ObjectDisposedException) { break; }
            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var supplied = Encoding.UTF8.GetBytes(context.Request.Headers["Authorization"] ?? "");
            if (supplied.Length != _expectedToken.Length ||
                !CryptographicOperations.FixedTimeEquals(supplied, _expectedToken))
            {
                await SendAsync(context, 401, new { error = "UNAUTHORIZED" }).ConfigureAwait(false);
                return;
            }

            var method = context.Request.HttpMethod;
            var path = context.Request.Url?.AbsolutePath;
            if (_spectatorOnly)
            {
                if (method == "GET" && path == "/v1/agent/status")
                    await SendAsync(context, 200, _agentFeed!.Status()).ConfigureAwait(false);
                else if (method == "GET" && path == "/v1/agent/boundary")
                    await SendAsync(context, 200, new
                    {
                        status = _agentFeed!.Status(),
                        boundary = _agentFeed.Current()
                    }).ConfigureAwait(false);
                else
                    await SendAsync(context, 403, new
                    {
                        error = "NATIVE_SPECTATOR_READ_ONLY",
                        supported_endpoints = new[]
                        {
                            "GET /v1/agent/status", "GET /v1/agent/boundary"
                        }
                    }).ConfigureAwait(false);
                return;
            }
            if (method == "GET" && path == "/v1/status")
            {
                await SendAsync(context, 200,
                    new { controller = _controller.Snapshot(), last_event_seq = _journal.LastSequence })
                    .ConfigureAwait(false);
            }
            else if (method == "GET" && path == "/v1/events")
            {
                long.TryParse(context.Request.QueryString["after_seq"], out long sequence);
                if (sequence < 0) sequence = 0;
                await SendAsync(context, 200, new { events = _journal.ReadAfter(sequence, 512),
                    last_event_seq = _journal.LastSequence }).ConfigureAwait(false);
            }
            else if (method == "POST" && path is "/v1/pause" or "/v1/resume" or "/v1/step" or "/v1/stop")
            {
                try
                {
                    switch (path)
                    {
                        case "/v1/pause": _controller.Pause(); break;
                        case "/v1/resume": _controller.Resume(); break;
                        case "/v1/step": _controller.Step(); break;
                        case "/v1/stop": _controller.Stop(); break;
                    }
                    await SendAsync(context, 200, _controller.Snapshot()).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    await SendAsync(context, 409, new { error = ex.Message }).ConfigureAwait(false);
                }
            }
            else if (method == "POST" && path == "/v1/actions")
            {
                var body = await ReadBoundedAsync(context.Request.InputStream).ConfigureAwait(false);
                if (body is null)
                {
                    await SendAsync(context, 413, new { error = "BODY_TOO_LARGE" }).ConfigureAwait(false);
                    return;
                }
                ActionSubmission? submission;
                try { submission = JsonSerializer.Deserialize<ActionSubmission>(body, JsonOptions); }
                catch (JsonException)
                {
                    await SendAsync(context, 400, new { error = "BAD_ACTION_JSON" }).ConfigureAwait(false);
                    return;
                }
                if (submission is null)
                {
                    await SendAsync(context, 400, new { error = "MISSING_ACTION" }).ConfigureAwait(false);
                    return;
                }
                var receipt = _controller.Submit(submission);
                await SendAsync(context, receipt.Accepted ? 200 : 409, receipt).ConfigureAwait(false);
            }
            else await SendAsync(context, 404, new { error = "UNKNOWN_ENDPOINT" }).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A disconnected client must never unwind into the game thread.
            try { context.Response.Abort(); } catch (ObjectDisposedException) { }
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(Stream input)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int n;
        while ((n = await input.ReadAsync(chunk).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + n > MaxActionBytes) return null;
            await buffer.WriteAsync(chunk.AsMemory(0, n)).ConfigureAwait(false);
        }
        return buffer.ToArray();
    }

    private static async Task SendAsync(HttpListenerContext context, int code, object value)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        context.Response.StatusCode = code;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
        context.Response.Close();
    }

    public async ValueTask DisposeAsync()
    {
        _server.Stop();
        _server.Close();
        if (_loop is not null) await _loop.ConfigureAwait(false);
    }
}
