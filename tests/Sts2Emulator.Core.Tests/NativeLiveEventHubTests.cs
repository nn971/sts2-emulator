using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text.Json;
using Sts2Emulator.Core;
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
    public void CanonicalFingerprintMatchesPythonForNativeRngExponentsAndEscapes()
    {
        using var document = JsonDocument.Parse(
            """{"type":"native_rng_scalar_boundary","ordinal":371,"data":{"selected_float":-5.0546736E-05,"label":"<native>&"}}""");
        Assert.Equal(
            "1984f86719ddf531392b0e39b369f29ec5fa55b93d11646177c8526ab409b072",
            CanonicalJson.Sha256(document.RootElement));
    }

    [Fact]
    public void CanonicalFingerprintMatchesPythonForRealNativeHeaderDateOffset()
    {
        // This header-like payload is representative of the first source
        // event after session_started in a real local native fidelity SSE.
        using var document = JsonDocument.Parse(
            """
            {"type":"header","ordinal":1,"data":{"timestamp":"2026-11-02T09:04:06+00:00","build":"assembly=0.1.0.0;mvid=ABC;sha256=0861","label":"<native>& +` \"quote\"","run_id":"synthetic-native-combat"}}
            """);
        Assert.Equal(
            "3c68dabb4333dbab1bd387ac1c20abfe4d8bf2775c10722790f3894a60b582e5",
            CanonicalJson.Sha256(document.RootElement));
    }

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
    [Fact]
    public async Task ProvisionalPostCardDifferenceIsAcceptedAndIrreversible()
    {
        var prefix = NewJournalPrefix();
        await using (var hub = new NativeLiveEventHub(prefix))
        {
            Assert.True(hub.OpenSession("synthetic-combat"));
            Assert.True(hub.PublishFidelityRecord(new NativeActionEnvelopeRecord(
                "action_started", 1)));
            var candidate = new NativeLiveFidelityResult(
                NativeLiveFidelityResult.SchemaId, "synthetic-combat", 1, 2,
                "SOURCE_POST_CARD_ACTION_DIFFERENCE_UNCERTIFIED",
                "First source-stable action difference; settlement unverified",
                1, 0, 1, "$.combat.energy", "SCALAR_DIFFERENCE");
            Assert.True(NativeLiveFidelityResult.IsTerminalFailure(candidate.Status));
            Assert.Null(hub.AcceptFidelityResult(candidate));
            Assert.Equal("$.combat.energy",
                hub.LatestFidelityResult!.FirstDifferencePath);
            Assert.Null(hub.AcceptFidelityResult(candidate)); // reconnect retry
            Assert.Equal("FIDELITY_SESSION_BLOCKED",
                hub.AcceptFidelityResult(candidate with {
                    Status = "PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED",
                    ThroughSequence = 2,
                    FirstDifferencePath = null,
                    FirstDifferenceReason = null
                }));
            Assert.True(hub.OpenSession("next-combat"));
            Assert.Null(hub.LatestFidelityResult);
        }
        Directory.Delete(Path.GetDirectoryName(prefix)!, recursive: true);
    }

    [Fact]
    public async Task FidelityResultRequiresCurrentEpochObservedCursorAndIdempotence()
    {
        var prefix = NewJournalPrefix();
        await using (var hub = new NativeLiveEventHub(prefix))
        {
            Assert.True(hub.OpenSession("synthetic-combat"));
            Assert.True(hub.PublishFidelityRecord(new NativeActionEnvelopeRecord(
                "native_turn_checkpoint", 1)));
            var result = new NativeLiveFidelityResult(
                NativeLiveFidelityResult.SchemaId, "synthetic-combat", 1, 2,
                "PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED",
                "Projected checkpoint matches, settlement unverified", 1, 0, 0);
            Assert.Null(hub.AcceptFidelityResult(result));
            Assert.Equal(result, hub.LatestFidelityResult);
            Assert.Null(hub.AcceptFidelityResult(result)); // reconnect retry
            Assert.Equal("CONFLICTING_FIDELITY_RESULT",
                hub.AcceptFidelityResult(result with { PlayerCheckpointsMatched = 2 }));
            var blocked = result with {
                Status = "BLOCKED_STREAM_INTEGRITY",
                Reason = "Missing native source sequence" };
            Assert.Null(hub.AcceptFidelityResult(blocked));
            Assert.Equal(blocked, hub.LatestFidelityResult);
            Assert.Null(hub.AcceptFidelityResult(blocked));
            Assert.Equal("FIDELITY_SESSION_BLOCKED", hub.AcceptFidelityResult(result));
            Assert.Equal("UNOBSERVED_FIDELITY_CURSOR",
                hub.AcceptFidelityResult(result with { ThroughSequence = 50 }));
            Assert.Equal("STALE_FIDELITY_SESSION",
                hub.AcceptFidelityResult(result with { SessionEpoch = 2 }));
            Assert.Equal("INVALID_FIDELITY_RESULT",
                hub.AcceptFidelityResult(result with { Status = "FULL_PARITY_CERTIFIED" }));
            Assert.True(hub.OpenSession("next-combat"));
            Assert.Null(hub.LatestFidelityResult);
            Assert.Equal("STALE_FIDELITY_SESSION", hub.AcceptFidelityResult(result));
        }
        Directory.Delete(Path.GetDirectoryName(prefix)!, recursive: true);
    }

    [Fact]
    public async Task FidelityFeedbackEndpointUsesPrivateCredentialAndRejectsStaleResults()
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
            var result = new NativeLiveFidelityResult(
                NativeLiveFidelityResult.SchemaId, "synthetic-combat", 1, 1,
                "EMULATOR_BOOTSTRAPPED_UNCERTIFIED",
                "Independent startup completed", 0, 0, 0);
            await using (var server = new LocalBridgeServer(
                new NativeAgentBoundaryFeed(), PublicToken, port,
                liveEvents: hub, fidelityToken: FidelityToken))
            {
                server.Start();
                using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
                var url = "http://127.0.0.1:" + port + "/v1/fidelity/result";
                var statusUrl = "http://127.0.0.1:" + port + "/v1/fidelity/status";
                var options = new JsonSerializerOptions {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", PublicToken);
                var denied = await http.PostAsJsonAsync(url, result, options);
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
                var deniedStatus = await http.GetAsync(statusUrl);
                Assert.Equal(HttpStatusCode.Unauthorized, deniedStatus.StatusCode);
                Assert.Null(hub.LatestFidelityResult);

                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", FidelityToken);
                using (var pendingStatus = await http.GetAsync(statusUrl))
                {
                    Assert.Equal(HttpStatusCode.OK, pendingStatus.StatusCode);
                    using var initial = JsonDocument.Parse(
                        await pendingStatus.Content.ReadAsStringAsync());
                    Assert.Equal(JsonValueKind.Null, initial.RootElement
                        .GetProperty("latest_result").ValueKind);
                    Assert.False(initial.RootElement.GetProperty(
                        "native_settlement_certified").GetBoolean());
                    Assert.False(initial.RootElement.GetProperty(
                        "native_live_lockstep").GetBoolean());
                    Assert.Equal(1, initial.RootElement
                        .GetProperty("last_source_sequence").GetInt64());
                }
                var accepted = await http.PostAsJsonAsync(url, result, options);
                Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
                Assert.Equal(result, hub.LatestFidelityResult);
                using (var acceptedStatus = await http.GetAsync(statusUrl))
                {
                    Assert.Equal(HttpStatusCode.OK, acceptedStatus.StatusCode);
                    using var published = JsonDocument.Parse(
                        await acceptedStatus.Content.ReadAsStringAsync());
                    var received = published.RootElement.GetProperty("latest_result");
                    Assert.Equal("EMULATOR_BOOTSTRAPPED_UNCERTIFIED",
                        received.GetProperty("status").GetString());
                    Assert.Equal(1, received.GetProperty("through_sequence").GetInt64());
                    Assert.Equal(0, received.GetProperty(
                        "player_checkpoints_matched").GetInt32());
                }
                var duplicate = await http.PostAsJsonAsync(url, result, options);
                Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
                var bad = await http.PostAsJsonAsync(url,
                    result with { SessionEpoch = 22 }, options);
                Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
                var illegal = await http.PostAsJsonAsync(url,
                    result with { Status = "FULL_PARITY_CERTIFIED" }, options);
                Assert.Equal(HttpStatusCode.Conflict, illegal.StatusCode);
                Assert.Equal(result, hub.LatestFidelityResult);
            }
        }
        Directory.Delete(Path.GetDirectoryName(prefix)!, recursive: true);
    }

}
