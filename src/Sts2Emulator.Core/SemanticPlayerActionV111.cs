using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// A restricted real player-action segment between controlled Nature
/// boundaries. The semantic provider is NOT reinitialized. Supported actions
/// must finish at a player boundary with a valid provider-owned deck; all
/// unsupported chance and card effects fail before a child is published.
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
    // Source-action admission remains explicit. Any known draw-pile changes
    // must pass through SemanticDrawPileAdapterV111 and preserve the Nature
    // boundary; stochastic and interactive effects use their own continuations.
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
            "proto.silent.echoing_slash",
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
            "proto.silent.outbreak",
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
            "proto.silent.storm_of_steel",
            "proto.colorless.dramatic_entrance",
            "proto.colorless.bolas",
            "proto.colorless.calamity",
            "proto.colorless.nostalgia",
            "proto.colorless.ultimate_strike",
            "proto.colorless.ultimate_defend",
            "proto.colorless.shockwave",
            "proto.colorless.mind_blast",
            "proto.colorless.hand_of_greed",
            "proto.colorless.panache",
            "proto.colorless.the_bomb",
            "proto.colorless.mayhem",
            "proto.colorless.automation",
            "proto.colorless.panic_button",
            "proto.colorless.dark_shackles",
            "proto.colorless.prowess",
            "proto.colorless.equilibrium",
            "proto.colorless.production",
            "proto.colorless.prolong",
            "proto.colorless.rend",
            "proto.colorless.salvo",
            "proto.colorless.gold_axe",
            "proto.colorless.fisticuffs",
            "proto.colorless.fasten",
            "proto.colorless.prep_time",
            "proto.colorless.eternal_armor",
            "proto.colorless.omnislice",
            "proto.colorless.rolling_boulder",
            "proto.colorless.the_gambit",
            "proto.colorless.entropy",
            "proto.colorless.stratagem",
            "proto.colorless.thrumming_hatchet"
        };

    internal static bool SupportsCard(string cardId) =>
        SupportedCards.Contains(cardId)
        && PrototypeContent.Card(cardId) is
            { MechanicsImplemented: true, MultiplayerOnly: false };

    internal static bool IsSupportedNostalgiaDrawReturn(
        RunState source, RunState result, GameAction action)
    {
        if (action.Kind != "play_card"
            || source.World?.Combat is not { } before
            || result.Phase != RunPhase.Combat
            || result.World?.Combat is not { } after)
            return false;

        var payload = action.ReadPayload<PlayCardPayload>();
        var card = before.Cards.SingleOrDefault(item =>
            item.InstanceId == payload.CardInstanceId);
        if (card is null
            || !before.Hand.Contains(card.InstanceId)
            || PrototypeContent.Card(card.CardId).Type is not
                (PrototypeCardType.Attack or PrototypeCardType.Skill))
            return false;

        var playedCardCount = after.CounterState.AttacksPlayedThisTurn
            + after.CounterState.SkillsPlayedThisTurn;
        if (!after.PlayerPowers.Any(power => power.Stacks > playedCardCount
                && PrototypeContent.Power(power.PowerId)
                    .PlayedAttacksAndSkillsReturnToDraw))
            return false;

        var expectedZones = before.Mechanics.CardZones.WithZoneChanges(
            new Dictionary<PrototypeCardZone, long[]>
            {
                [PrototypeCardZone.DrawPile] = before.DrawPile
                    .Append(card.InstanceId).ToArray(),
                [PrototypeCardZone.Hand] = before.Hand
                    .Where(id => id != card.InstanceId).ToArray()
            });
        return CanonicalJson.Sha256(expectedZones)
                == CanonicalJson.Sha256(after.Mechanics.CardZones)
            && after.DrawPile.SequenceEqual(
                before.DrawPile.Append(card.InstanceId));
    }

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
        if (action.Kind != "play_card")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                "operation=" + action.Kind);
        var payload = action.Payload.Deserialize<PlayCardPayload>()
            ?? throw new InvalidDataException("Invalid play-card payload.");
        var combat = source.World!.Combat!;
        var card = combat.Cards.SingleOrDefault(c =>
            c.InstanceId == payload.CardInstanceId);
        if (card is null || !combat.Hand.Contains(payload.CardInstanceId) ||
            !SupportsCard(card.CardId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                "operation=card_effect_requires_semantic_audit");

        RunState run;
        using (ControlledChanceGuardV111.Enter())
            run = environment.Step(source.Fork(), actionId).State;
        // The allowlisted action may still activate an unmodeled relic,
        // power, draw hook or enemy mechanic. Any supported deck change must
        // be the exact Nostalgia source return committed by the shared adapter.
        var afterCombat = run.World?.Combat;
        if (!PrototypeGameEngine
                .IsAtCombatPlayerDecisionBoundary(run) || afterCombat is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                "operation=action_crossed_chance_or_turn_boundary");
        if ((!combat.DrawPile.SequenceEqual(afterCombat.DrawPile) ||
             CanonicalJson.Sha256(combat.Stochastic.SemanticDeck) !=
                CanonicalJson.Sha256(afterCombat.Stochastic.SemanticDeck)) &&
            !IsSupportedNostalgiaDrawReturn(source, run, action))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                "operation=unmodeled_draw_pile_mutation");
        ValidateBoundary(run);

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
        ValidateBoundary(state.Run);
        var sourceAction = string.IsNullOrWhiteSpace(state.ActionId)
            ? null
            : new PrototypeAiEnvironment().ResolvePlayerAction(
                state.Origin, state.ActionId).Action;
        if (state.ActionKind != "play_card" || sourceAction is null ||
            ((!state.Origin.World!.Combat!.DrawPile.SequenceEqual(
                  state.Run.World!.Combat!.DrawPile) ||
              CanonicalJson.Sha256(state.Origin.World.Combat.Stochastic.SemanticDeck) !=
                  CanonicalJson.Sha256(state.Run.World.Combat.Stochastic.SemanticDeck)) &&
             !IsSupportedNostalgiaDrawReturn(
                 state.Origin, state.Run, sourceAction)))
            throw new InvalidDataException(
                "Semantic player action changed provider-owned hidden deck outside a supported source return.");
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
