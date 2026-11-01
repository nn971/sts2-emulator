using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using Sts2ReferenceMod.Trace;
using Sts2ReferenceMod.Transport;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeLiveEventHubTests
{
    private const string PublicToken = "public-credential-for-native-test-971";
    private const string FidelityToken = "private-credential-for-native-test-971";

    private static string NewJournalPrefix() => Path.Combine(
        Path.GetTempPath(), "sts2-native-live-test-" + Guid.NewGuid().ToString("N"),
        "capture");

    private static NativeAgentVisibleBoundary Boundary() => new(
        NativeAgentBoundaryFeed.SchemaId,
        NativeAgentBoundaryFeed.ObservationSchemaId,
        "synthetic-combat", 1, 1, "public-only-hash",
        JsonSerializer.SerializeToElement(new { hp = 50, draw_pile_count = 5 }),
        JsonSerializer.SerializeToElement(new[] { new { kind = "end_turn" } }),
        "PARTIAL_CARD_PLAY_AND_END_TURN_ONLY", "NOT_TESTED", "HUMAN_ONLY");

    [Fact]
    public async Task PublicAndFidelityEventsHaveIndependentCursorsAndJournalFiles()
    {
        var prefix = NewJournalPrefix();
        await using (var hub = new NativeLiveEventHub(prefix))
        {
            Assert.True(hub.OpenSession("synthetic-combat"));
            Assert.True(hub.PublishPublicBoundary(Boundary()));
            Assert.True(hub.PublishFidelityRecord(new NativeActionEnvelopeRecord(
                "executor_start", 7,
                Data: JsonSerializer.SerializeToElement(new
                {
                    rng_secret = "PRIVILEGED_RANDOM_WORD_971",
                    hidden_card = "HIDDEN_CARD_971"
                }))));
            Assert.True(hub.PublishPublicStatus("ACTION_RESOLVING"));

            var publicEvents = hub.ReadAfter(NativeLiveEventHub.PublicChannel, 0);
            var privateEvents = hub.ReadAfter(NativeLiveEventHub.FidelityChannel, 0);
            Assert.Equal(new long[] { 1, 2, 3 },
                publicEvents.Select(e => e.Sequence).ToArray());
            Assert.Equal(new long[] { 1, 2 },
                privateEvents.Select(e => e.Sequence).ToArray());
            Assert.Equal("decision", publicEvents[1].Kind);
            Assert.Equal("executor_start", privateEvents[1].Kind);
            Assert.Equal(7, privateEvents[1].SourceOrdinal);
            Assert.Equal(64, publicEvents[1].ContentFingerprint.Length);
            Assert.Equal("synthetic-combat", publicEvents[1].RunId);
            Assert.All(publicEvents, e => Assert.Equal(1, e.SessionEpoch));
            Assert.Equal(2, hub.ReadAfter(NativeLiveEventHub.PublicChannel, 1).Count);
            Assert.Empty(hub.ReadAfter(NativeLiveEventHub.FidelityChannel, 2));
            string publicJson = string.Join("\n", publicEvents.Select(
                Sts2Emulator.Trace.TraceJsonl.SerializeLine));
            Assert.DoesNotContain("PRIVILEGED_RANDOM_WORD_971", publicJson);
            Assert.DoesNotContain("HIDDEN_CARD_971", publicJson);
            Assert.Throws<InvalidOperationException>(() =>
                hub.ReadAfter(NativeLiveEventHub.PublicChannel, 999));
        }
        var onDiskPublic = await File.ReadAllTextAsync(prefix + ".public.jsonl");
        var onDiskPrivate = await File.ReadAllTextAsync(prefix + ".fidelity.jsonl");
        Assert.Contains("decision", onDiskPublic);
        Assert.DoesNotContain("PRIVILEGED_RANDOM_WORD_971", onDiskPublic);
        Assert.Contains("PRIVILEGED_RANDOM_WORD_971", onDiskPrivate);
        Directory.Delete(Path.GetDirectoryName(prefix)!, recursive: true);
    }

    [Fact]
    public async Task CapacityFailureIsExplicitAndNoEventIsSilentlyEvicted()
    {
        var prefix = NewJournalPrefix();
        await using (var hub = new NativeLiveEventHub(prefix, capacity: 2))
        {
            Assert.True(hub.OpenSession("synthetic-combat"));
            Assert.True(hub.PublishPublicStatus("WAITING"));
            Assert.False(hub.PublishPublicStatus("OVERFLOW"));
            Assert.Equal("LIVE_EVENT_JOURNAL_CAPACITY_EXCEEDED", hub.Failure);
            Assert.Throws<InvalidOperationException>(() =>
                hub.ReadAfter(NativeLiveEventHub.PublicChannel, 0));
            Assert.Equal(2, hub.LastSequence(NativeLiveEventHub.PublicChannel));
        }
        Directory.Delete(Path.GetDirectoryName(prefix)!, recursive: true);
    }

    [Fact]
    public async Task StreamSeparatesCredentialsAndSupportsLastEventIdReplay()
    {
        int port;
        using (var holder = new TcpListener(IPAddress.Loopback, 0))
        {
            holder.Start();
            port = ((IPEndPoint)holder.LocalEndpoint).Port;
        }
        var prefix = NewJournalPrefix();
        await using (var hub = new NativeLiveEventHub(prefix))
        {
            Assert.True(hub.OpenSession("synthetic-combat"));
            Assert.True(hub.PublishPublicBoundary(Boundary()));
            Assert.True(hub.PublishFidelityRecord(new NativeActionEnvelopeRecord(
                "executor_start", 12,
                Data: JsonSerializer.SerializeToElement(new
                {
                    private_marker = "ONLY_FOR_FIDELITY_971"
                }))));
            var feed = new NativeAgentBoundaryFeed();
            await using (var server = new LocalBridgeServer(feed, PublicToken, port,
                liveEvents: hub, fidelityToken: FidelityToken))
            {
                server.Start();
                using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
                var root = "http://127.0.0.1:" + port;
                var unauthorized = await http.GetAsync(root + "/v1/public/stream");
                Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", PublicToken);
                var denied = await http.GetAsync(root + "/v1/fidelity/stream");
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

                using var publicRequest = new HttpRequestMessage(HttpMethod.Get,
                    root + "/v1/public/stream");
                publicRequest.Headers.TryAddWithoutValidation("Last-Event-ID", "1");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var response = await http.SendAsync(publicRequest,
                    HttpCompletionOption.ResponseHeadersRead, cts.Token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new StreamReader(stream);
                Assert.Equal("id: 2", await reader.ReadLineAsync(cts.Token));
                Assert.Equal("event: native", await reader.ReadLineAsync(cts.Token));
                var data = await reader.ReadLineAsync(cts.Token);
                Assert.StartsWith("data: ", data);
                Assert.DoesNotContain("ONLY_FOR_FIDELITY_971", data);

                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", FidelityToken);
                using var privateRequest = new HttpRequestMessage(HttpMethod.Get,
                    root + "/v1/fidelity/stream");
                privateRequest.Headers.TryAddWithoutValidation("Last-Event-ID", "1");
                using var privateResponse = await http.SendAsync(privateRequest,
                    HttpCompletionOption.ResponseHeadersRead, cts.Token);
                Assert.Equal(HttpStatusCode.OK, privateResponse.StatusCode);
                await using var privateStream = await privateResponse.Content
                    .ReadAsStreamAsync(cts.Token);
                using var privateReader = new StreamReader(privateStream);
                Assert.Equal("id: 2", await privateReader.ReadLineAsync(cts.Token));
                Assert.Equal("event: native", await privateReader.ReadLineAsync(cts.Token));
                Assert.Contains("ONLY_FOR_FIDELITY_971",
                    await privateReader.ReadLineAsync(cts.Token));
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", PublicToken);
                var post = await http.PostAsync(root + "/v1/step", null, cts.Token);
                Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
            }
        }
        Directory.Delete(Path.GetDirectoryName(prefix)!, recursive: true);
    }
}
