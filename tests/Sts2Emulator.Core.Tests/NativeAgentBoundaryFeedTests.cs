using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;
using Sts2ReferenceMod.Transport;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeAgentBoundaryFeedTests
{
    private const string OriginalPrivateHash = "SECRET_INTERNAL_STATE_HASH_971";
    private const string SecretCardId = "NEVER_REVEAL_HIDDEN_CARD_MODEL_971";

    private static NativeDecisionRecord Decision(long id = 1, long revision = 1,
        string extraAction = "")
    {
        string json = """
            {
              "player":{"hp":51,"max_hp":70,"block":4,"gold":100,
                        "powers":[{"model_id":"DEXTERITY","amount":2}]},
              "combat":{"energy":3,"max_energy":3,"turn":2,
                "creatures":[
                  {"id":"creature:0","model_id":"SILENT","hp":51,"max_hp":70,
                   "block":4,"alive":true,"powers":[]},
                  {"id":"creature:1","model_id":"FOGMOG","hp":21,"max_hp":30,
                   "block":0,"alive":true,"powers":[
                        {"model_id":"WEAK","amount":2}]}],
                "hand":[{"instance_id":42,"model_id":"STRIKE_SILENT",
                         "upgrades":0,"cost":1,"target_type":"AnyEnemy"}],
                "draw_pile":[{"instance_id":13,"model_id":"NEVER_REVEAL_HIDDEN_CARD_MODEL_971",
                              "upgrades":0,"cost":3,"target_type":"None"}],
                "discard_pile":[],"exhaust_pile":[]},
              "rng":{"state0":971,"secret":"FORBIDDEN_INTERNAL_RNG_WORD"},
              "unsupported_continuation":["SECRET_INTERNAL_CONTINUATION"],
              "never_expose":"PROTECTED_NATIVE_VALUE"
            }
            """;
        var state = JsonDocument.Parse(json).RootElement.Clone();
        var actions = new List<GameAction>
        {
            new("play_card", JsonSerializer.SerializeToElement(new
            {
                card_instance_id = 42L, target_id = "creature:1",
                server_only = "SECRET_ACTION_FIELD"
            })),
            GameAction.Empty("end_turn")
        };
        if (extraAction.Length > 0)
            actions.Add(GameAction.Empty(extraAction));
        return new NativeDecisionRecord("decision", id, "Combat", state,
            OriginalPrivateHash, actions.ToArray(), "SECRET_NATIVE_OBS_HASH",
            revision, new CaptureCoverage([], [], [], "RNG_UNAUDITED", []), 0);
    }

    [Fact]
    public void OnlyExplicitPublicFieldsReachAgentAndPublicHashHasNoPrivateSource()
    {
        var feed = new NativeAgentBoundaryFeed();
        feed.OpenCombat("synthetic-session");
        feed.Publish(Decision());
        var boundary = Assert.IsType<NativeAgentVisibleBoundary>(feed.Current());
        string json = JsonSerializer.Serialize(boundary);
        Assert.DoesNotContain(SecretCardId, json, StringComparison.Ordinal);
        Assert.DoesNotContain(OriginalPrivateHash, json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_NATIVE_OBS_HASH", json, StringComparison.Ordinal);
        Assert.DoesNotContain("FORBIDDEN_INTERNAL_RNG_WORD", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_INTERNAL_CONTINUATION", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PROTECTED_NATIVE_VALUE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_ACTION_FIELD", json, StringComparison.Ordinal);
        Assert.DoesNotContain("state0", json, StringComparison.Ordinal);
        Assert.Equal(1, boundary.Observation.GetProperty("combat")
            .GetProperty("draw_pile_count").GetInt32());
        Assert.Equal(42, boundary.Observation.GetProperty("combat")
            .GetProperty("hand")[0].GetProperty("instance_id").GetInt64());
        Assert.Equal("creature:1", boundary.LegalActions[0]
            .GetProperty("payload").GetProperty("target_id").GetString());
        Assert.Equal(2, boundary.LegalActions.GetArrayLength());
        Assert.Equal("HUMAN_ONLY", boundary.ControlMode);
        var consistent = feed.Read();
        Assert.True(consistent.Status.PlayerBoundaryAvailable);
        Assert.Equal(consistent.Status.DecisionId, consistent.Boundary!.DecisionId);
        Assert.Equal("NOT_TESTED", boundary.NativeGameParity);
        Assert.Equal("PARTIAL_CARD_PLAY_AND_END_TURN_ONLY", boundary.LegalMenuCoverage);
        Assert.NotEqual(OriginalPrivateHash, boundary.PublicHash);
        Assert.Equal(64, boundary.PublicHash.Length);
    }

    [Fact]
    public void ActionStartAndNestedChoiceInvalidatePreviouslyVisibleDecision()
    {
        var feed = new NativeAgentBoundaryFeed();
        Assert.Equal("WAITING_FOR_COMBAT", feed.Status().Phase);
        feed.OpenCombat("combat-a");
        Assert.Null(feed.Current());
        feed.Publish(Decision());
        Assert.True(feed.Status().PlayerBoundaryAvailable);
        feed.Withhold("ACTION_RESOLVING");
        var withheld = feed.Read();
        Assert.False(withheld.Status.PlayerBoundaryAvailable);
        Assert.Null(withheld.Boundary);
        Assert.Null(feed.Current());
        Assert.False(feed.Status().PlayerBoundaryAvailable);
        feed.Withhold("UNSUPPORTED_NESTED_CHOICE");
        Assert.Null(feed.Current());
        feed.Publish(Decision(2, 2));
        Assert.Equal(2, feed.Current()!.DecisionId);
        feed.EndCombat("Combat ended");
        Assert.Null(feed.Current());
        Assert.Equal("COMBAT_COMPLETE", feed.Status().Phase);
        feed.OpenCombat("combat-b");
        Assert.Null(feed.Current());
        Assert.True(feed.Status().CombatSessionPresent);
        feed.Publish(Decision());
        Assert.Equal("combat-b", feed.Current()!.SessionId);
    }

    [Fact]
    public void UnknownLegalActionFailsClosedWithoutPublishingPartialMenu()
    {
        var feed = new NativeAgentBoundaryFeed();
        feed.OpenCombat("combat-a");
        Assert.Throws<InvalidOperationException>(() =>
            feed.Publish(Decision(extraAction: "choose_reward")));
        Assert.Null(feed.Current());
        feed.Publish(Decision());
        Assert.NotNull(feed.Current());
        Assert.Throws<ArgumentException>(() =>
            feed.Publish(Decision(0)));
        Assert.Throws<InvalidOperationException>(() =>
            feed.Publish(Decision(2, 3, extraAction: "unknown")));
        // Prior decision must be explicitly withheld when native action starts.
        Assert.Equal(1, feed.Current()!.DecisionId);
        feed.Withhold("PROJECTION_FAILED");
        Assert.Null(feed.Current());
    }

    [Fact]
    public async Task AuthenticatedLoopbackSpectatorRejectsEveryControlEndpoint()
    {
        int port;
        using (var holder = new TcpListener(IPAddress.Loopback, 0))
        {
            holder.Start();
            port = ((IPEndPoint)holder.LocalEndpoint).Port;
        }
        var feed = new NativeAgentBoundaryFeed();
        feed.OpenCombat("synthetic-run");
        feed.Publish(Decision());
        await using var server = new LocalBridgeServer(feed,
            "test-token-should-never-appear-in-response-971", port);
        server.Start();
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
        var url = "http://127.0.0.1:" + port;
        var denied = await http.GetAsync(url + "/v1/agent/boundary");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                "test-token-should-never-appear-in-response-971");
        var current = await http.GetAsync(url + "/v1/agent/boundary");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        string currentBody = await current.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretCardId, currentBody, StringComparison.Ordinal);
        Assert.Contains("draw_pile_count", currentBody, StringComparison.Ordinal);
        Assert.Contains("play_card", currentBody, StringComparison.Ordinal);
        var post = await http.PostAsync(url + "/v1/step", null);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        var action = await http.PostAsync(url + "/v1/actions", null);
        Assert.Equal(HttpStatusCode.Forbidden, action.StatusCode);
        var history = await http.GetAsync(url + "/v1/events");
        Assert.Equal(HttpStatusCode.Forbidden, history.StatusCode);
        feed.Withhold("ACTION_RESOLVING");
        var resolved = await http.GetStringAsync(url + "/v1/agent/boundary");
        Assert.Contains("\"boundary\":null", resolved, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_NATIVE_OBS_HASH", resolved, StringComparison.Ordinal);
    }
}
