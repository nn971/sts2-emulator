using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// A restricted real player-action segment between controlled Nature
/// boundaries. The semantic provider is NOT reinitialized. Only actions
/// with audited, draw-pile-neutral semantics are allowed in this version;
/// all unsupported card/relic effects fail before a child is published.
/// </summary>
public sealed record SemanticPlayerActionV111(
    RunState Origin, RunState Run, string ActionId, string ActionKind)
{
    public const string SchemaId = "sts2-v111-semantic-player-action-v1";
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public bool AtPlayerBoundary =>
        PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary(Run);

    public SemanticPlayerActionV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork()
    };
}

public static class SemanticPlayerActionEngineV111
{
    // This first gate has verified source card effects with no primitive
    // draw-pile mutation. The full action/effect state machine will replace
    // this allowlist as semantic continuations become available.
    private static readonly HashSet<string> SupportedCards =
        new(StringComparer.Ordinal)
        {
            "proto.silent.strike",
            "proto.silent.defend",
            "proto.silent.deflect",
            "proto.silent.neutralize",
            "proto.silent.slice",
            "proto.silent.poisoned_stab",
            "proto.silent.deadly_poison",
            "proto.silent.sucker_punch",
            "proto.silent.dagger_spray",
            "proto.silent.dash",
            "proto.silent.cloak_and_dagger",
            "proto.silent.blade_dance",
            "proto.silent.piercing_wail",
            "proto.silent.backstab",
            "proto.silent.leg_sweep",
            "proto.silent.skewer",
            "proto.silent.footwork",
            "proto.silent.accuracy",
            "proto.silent.phantom_blades",
            "proto.silent.tracking",
            "proto.silent.infinite_blades",
            "proto.silent.suppress",
            "proto.silent.anticipate",
            "proto.silent.snakebite",
            "proto.silent.assassinate",
            "proto.silent.catalyst",
            "proto.silent.expose",
            "proto.silent.malaise",
            "proto.silent.crippling_cloud",
            "proto.silent.blur",
            "proto.silent.dodge_and_roll",
            "proto.silent.sidestep",
            "proto.silent.pounce",
            "proto.silent.finisher",
            "proto.silent.flechettes",
            "proto.silent.bubble_bubble",
            "proto.silent.strangle",
            "proto.silent.corrosive_wave",
            "proto.silent.leading_strike",
            "proto.silent.noxious_fumes",
            "proto.silent.speedster",
            "proto.silent.serpent_form",
            "proto.silent.envenom",
            "proto.silent.afterimage",
            "proto.silent.untouchable",
            "proto.silent.flick_flack",
            "proto.silent.abrasive",
            "proto.silent.haze",
            "proto.silent.memento_mori",
            "proto.silent.murder",
            "proto.silent.precise_cut",
            "proto.silent.pinpoint",
            "proto.silent.grand_finale",
            "proto.silent.die_die_die",
            "proto.silent.master_planner",
            "proto.silent.burst",
            "proto.silent.bullet_time",
            "proto.silent.accelerant",
            "proto.silent.blade_of_ink",
            "proto.silent.fan_of_knives",
            "proto.silent.up_my_sleeve",
            "proto.silent.predator",
            "proto.silent.shiv",
            "proto.silent.mirage",
            "proto.silent.tactician",
            "proto.silent.tools_of_the_trade",
            "proto.silent.shadowmeld",
            "proto.silent.the_hunt",
            "proto.silent.well_laid_plans",
            "proto.silent.wraith_form",
            "proto.silent.storm_of_steel"
        };

    // These eight existing source-implemented potion effects are
    // draw-pile-neutral and require no nested player or Nature choice.
    // Keep the one supported-action gate here, rather than in H15.
    private static readonly HashSet<string> SupportedPotions =
        new(StringComparer.Ordinal)
        {
            "proto.potion.block",
            "proto.potion.dexterity",
            "proto.potion.energy",
            "proto.potion.strength",
            "proto.potion.explosive",
            "proto.potion.fire",
            "proto.potion.poison",
            "proto.potion.weak"
        };

    internal static bool SupportsPotion(string potionId) =>
        SupportedPotions.Contains(potionId);

    public static string[] SupportedPotionIds =>
        SupportedPotions.Order(StringComparer.Ordinal).ToArray();

    internal static bool SupportsCard(string cardId) =>
        SupportedCards.Contains(cardId)
        && PrototypeContent.Card(cardId) is
            { MechanicsImplemented: true, MultiplayerOnly: false };

    public static string[] SupportedCardIds =>
        SupportedCards.Where(SupportsCard)
            .Order(StringComparer.Ordinal).ToArray();

    public static SemanticPlayerActionV111 Step(
        RunState source, string actionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ValidateBoundary(source);
        var environment = new PrototypeAiEnvironment();
        var action = environment.Observe(source).LegalActions
            .SingleOrDefault(a => a.ActionId == actionId) ??
            throw new InvalidOperationException(
                "Illegal or ambiguous semantic player action.");
        var combat = source.World!.Combat!;
        if (action.Kind == "play_card")
        {
            var payload = action.Payload.Deserialize<PlayCardPayload>()
                ?? throw new InvalidDataException("Invalid play-card payload.");
            var card = combat.Cards.SingleOrDefault(c =>
                c.InstanceId == payload.CardInstanceId);
            if (card is null || !combat.Hand.Contains(payload.CardInstanceId) ||
                !SupportsCard(card.CardId))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=card_effect_requires_semantic_audit");
        }
        else if (action.Kind == "use_potion")
        {
            var payload = action.Payload.Deserialize<UsePotionPayload>()
                ?? throw new InvalidDataException("Invalid use-potion payload.");
            var slots = source.Player.PotionSlots;
            var potionId = payload.Slot >= 0 && payload.Slot < slots.Length
                ? slots[payload.Slot]?.PotionId : null;
            if (potionId is null || !SupportsPotion(potionId))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=potion_effect_requires_semantic_audit");
        }
        else
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                "operation=" + action.Kind);

        RunState run;
        using (ControlledChanceGuardV111.Enter())
        // Source victory initializes post-combat rewards. Delegate only the
        // reward sites already explicitly allowlisted by the chance guard.
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            run = environment.Step(source.Fork(), actionId).State;
        // A real killing card may exit active combat before publishing
        // another player choice. Its semantic deck is consumed on combat
        // exit, so require the source's genuine reward/defeat boundary.
        if (PrototypeGameEngine.IsAtCombatRewardOrTerminalBoundary(run))
            ValidateCombatExit(run);
        else
        {
            var afterCombat = run.World?.Combat;
            if (!PrototypeGameEngine
                    .IsAtCombatPlayerDecisionBoundary(run) || afterCombat is null)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=action_crossed_chance_or_turn_boundary");
            if (!combat.DrawPile.SequenceEqual(afterCombat.DrawPile) ||
                CanonicalJson.Sha256(combat.SemanticDeck) !=
                    CanonicalJson.Sha256(afterCombat.SemanticDeck))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=unmodeled_draw_pile_mutation");
            ValidateBoundary(run);
        }

        var result = new SemanticPlayerActionV111(
            source.Fork(), run, actionId, action.Kind);
        Validate(result);
        return result;
    }

    public static string Save(SemanticPlayerActionV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    public static SemanticPlayerActionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var saved = JsonSerializer.Deserialize<SemanticPlayerActionV111>(json)
            ?? throw new InvalidDataException("Null semantic player action.");
        Validate(saved);
        var reproduced = Step(saved.Origin, saved.ActionId);
        if (CanonicalJson.Sha256(reproduced) != CanonicalJson.Sha256(saved))
            throw new InvalidDataException(
                "Semantic player action differs from full source replay.");
        return reproduced;
    }

    public static void Validate(SemanticPlayerActionV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateBoundary(state.Origin);
        if (state.ActionKind is not ("play_card" or "use_potion") ||
            string.IsNullOrWhiteSpace(state.ActionId))
            throw new InvalidDataException("Invalid semantic player action.");
        if (PrototypeGameEngine.IsAtCombatRewardOrTerminalBoundary(state.Run))
        {
            ValidateCombatExit(state.Run);
            return;
        }
        ValidateBoundary(state.Run);
        if (!state.Origin.World!.Combat!.DrawPile.SequenceEqual(
                state.Run.World!.Combat!.DrawPile) ||
            CanonicalJson.Sha256(state.Origin.World.Combat.SemanticDeck) !=
                CanonicalJson.Sha256(state.Run.World.Combat.SemanticDeck))
            throw new InvalidDataException(
                "Semantic player action changed provider-owned hidden deck.");
    }

    private static void ValidateCombatExit(RunState run)
    {
        if (run.Phase == RunPhase.Reward &&
            run.World is { Combat: null, Reward: not null })
            return;
        if (run.Phase == RunPhase.Terminal &&
            run.World is { Combat: null, Reward: null,
                TerminalOutcome: "defeat" })
            return;
        throw new InvalidDataException(
            "Semantic killing action did not produce a genuine combat exit.");
    }

    public static void ValidateBoundary(RunState run)
    {
        if (!PrototypeGameEngine
                .IsAtCombatPlayerDecisionBoundary(run) ||
            run.World?.Combat is not { SemanticDeck: not null } combat)
            throw new InvalidOperationException(
                "Expected an idle semantic Nature player combat boundary.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Expected an idle semantic Nature player combat boundary.",
            "Player-boundary physical pile diverges from Nature's private deck.");
    }
}
