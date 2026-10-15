using System.Text.Json;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;

namespace Sts2ReferenceMod.Transport;

/// <summary>
/// Main-thread publisher / off-thread reader of a strictly whitelisted
/// player-visible subset of one native combat decision. No original native
/// state, RNG, hidden draw order or privileged state hash crosses this API.
/// An absent boundary is an explicit state, never a cached action menu.
/// </summary>
public sealed class NativeAgentBoundaryFeed
{
    public const string SchemaId = "sts2-native-agent-spectator-v1";
    public const string ObservationSchemaId = "sts2-native-combat-visible-partial-v1";
    private readonly object _sync = new();
    private string? _sessionId;
    private string _phase = "WAITING_FOR_COMBAT";
    private long _lastDecisionId;
    private NativeAgentVisibleBoundary? _current;

    public void OpenCombat(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_sync)
        {
            _sessionId = sessionId;
            _lastDecisionId = 0;
            _current = null;
            _phase = "AWAITING_PLAYER_BOUNDARY";
        }
    }

    public void Withhold(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_sync)
        {
            _current = null;
            if (_sessionId is not null) _phase = reason;
        }
    }

    public void EndCombat(string reason)
    {
        lock (_sync)
        {
            _current = null;
            _phase = "COMBAT_COMPLETE";
        }
    }

    public void Publish(NativeDecisionRecord decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Phase != "Combat"
            || decision.DecisionId <= 0 || decision.ObservationRevision <= 0)
            throw new ArgumentException("Requires a real stable native combat decision.");
        // Construct only listed visible fields: never copy the whole native
        // State, state hash or source ObservationHash. This is a partial
        // public projection until native intent/relic/potion APIs are audited.
        JsonElement publicState = ProjectVisibleState(decision.State);
        var menu = decision.LegalActions.Select(ProjectLegalAction).ToArray();
        JsonElement publicMenu = JsonSerializer.SerializeToElement(menu);
        string hash = CanonicalJson.Sha256(JsonSerializer.SerializeToElement(new
        {
            observation = publicState,
            legal_actions = publicMenu
        }));
        lock (_sync)
        {
            if (_sessionId is null)
                throw new InvalidOperationException("No native combat session.");
            if (decision.DecisionId < _lastDecisionId)
                throw new InvalidOperationException("Stale native decision.");
            if (decision.DecisionId == _lastDecisionId && _current is not null
                && (_current.PublicHash != hash ||
                    _current.Revision != decision.ObservationRevision))
                throw new InvalidOperationException("Conflicting native decision revision.");
            _lastDecisionId = decision.DecisionId;
            _current = new NativeAgentVisibleBoundary(
                SchemaId, ObservationSchemaId, _sessionId,
                decision.DecisionId, decision.ObservationRevision, hash,
                publicState, publicMenu, "PARTIAL_CARD_PLAY_AND_END_TURN_ONLY",
                "NOT_TESTED", "HUMAN_ONLY");
            _phase = "PLAYER_BOUNDARY";
        }
    }

    /// <summary>One atomic readiness + boundary publication snapshot.</summary>
    public NativeAgentFeedRead Read()
    {
        lock (_sync)
            return new NativeAgentFeedRead(
                new NativeAgentFeedStatus(SchemaId, _phase,
                    _sessionId is not null, _current is not null,
                    _current?.DecisionId, "HUMAN_ONLY", false,
                    "PARTIAL_NATIVE_COMBAT_OBSERVATION", "NOT_TESTED"),
                _current);
    }

    public NativeAgentFeedStatus Status() => Read().Status;

    public NativeAgentVisibleBoundary? Current() => Read().Boundary;

    private static JsonElement ProjectVisibleState(JsonElement state)
    {
        var player = state.GetProperty("player");
        var combat = state.GetProperty("combat");
        static object[] Powers(JsonElement source) => source
            .EnumerateArray().Select(x => (object)new
            {
                model_id = x.GetProperty("model_id").GetString(),
                amount = x.GetProperty("amount").GetInt32()
            }).ToArray();
        static object[] Cards(JsonElement source) => source
            .EnumerateArray().Select(x => (object)new
            {
                instance_id = x.GetProperty("instance_id").GetInt64(),
                model_id = x.GetProperty("model_id").GetString(),
                upgrades = x.GetProperty("upgrades").GetInt32(),
                cost = x.GetProperty("cost").GetInt32(),
                target_type = x.GetProperty("target_type").GetString()
            }).ToArray();

        // No draw-pile content, seed, RNG state, original hidden hash,
        // unsupported_continuation, internal event history or memory address.
        // Enemy intent and all omitted information have a visible marker.
        return JsonSerializer.SerializeToElement(new
        {
            player = new
            {
                hp = player.GetProperty("hp").GetInt32(),
                max_hp = player.GetProperty("max_hp").GetInt32(),
                block = player.GetProperty("block").GetInt32(),
                gold = player.GetProperty("gold").GetInt32(),
                powers = Powers(player.GetProperty("powers"))
            },
            combat = new
            {
                energy = combat.GetProperty("energy").GetInt32(),
                max_energy = combat.GetProperty("max_energy").GetInt32(),
                turn = combat.GetProperty("turn").GetInt32(),
                creatures = combat.GetProperty("creatures")
                    .EnumerateArray().Select(x => new
                    {
                        id = x.GetProperty("id").GetString(),
                        model_id = x.GetProperty("model_id").GetString(),
                        hp = x.GetProperty("hp").GetInt32(),
                        max_hp = x.GetProperty("max_hp").GetInt32(),
                        block = x.GetProperty("block").GetInt32(),
                        alive = x.GetProperty("alive").GetBoolean(),
                        powers = Powers(x.GetProperty("powers"))
                    }).ToArray(),
                hand = Cards(combat.GetProperty("hand")),
                draw_pile_count = combat.GetProperty("draw_pile").GetArrayLength(),
                discard_pile = Cards(combat.GetProperty("discard_pile")),
                exhaust_pile = Cards(combat.GetProperty("exhaust_pile"))
            },
            unobserved_public_fields = new[]
            {
                "enemy_intents", "relics", "potions", "map_and_rewards",
                "nested_native_player_choices"
            },
            fidelity = "NATIVE_PLAYER_VISIBLE_SLICE_ONLY"
        });
    }

    private static object ProjectLegalAction(GameAction action)
    {
        if (action.Kind == "end_turn")
            return new { kind = "end_turn", payload = (object)new { } };
        if (action.Kind == "play_card")
        {
            var payload = action.Payload;
            return new
            {
                kind = "play_card",
                payload = (object)new
                {
                    card_instance_id = payload.GetProperty("card_instance_id").GetInt64(),
                    target_id = payload.GetProperty("target_id").ValueKind == JsonValueKind.Null
                        ? null : payload.GetProperty("target_id").GetString()
                }
            };
        }
        throw new InvalidOperationException("Unverified native agent action kind: "
            + action.Kind);
    }
}

public sealed record NativeAgentFeedStatus(
    string SchemaId, string Phase, bool CombatSessionPresent,
    bool PlayerBoundaryAvailable, long? DecisionId, string ControlMode,
    bool ActionSubmissionSupported, string ObservationCoverage,
    string NativeGameParity);

public sealed record NativeAgentVisibleBoundary(
    string SchemaId, string ObservationSchemaId, string SessionId,
    long DecisionId, long Revision, string PublicHash,
    JsonElement Observation, JsonElement LegalActions, string LegalMenuCoverage,
    string NativeGameParity, string ControlMode);

public sealed record NativeAgentFeedRead(
    NativeAgentFeedStatus Status, NativeAgentVisibleBoundary? Boundary);
