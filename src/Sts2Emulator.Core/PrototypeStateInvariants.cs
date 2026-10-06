namespace Sts2Emulator.Core;

public static class PrototypeStateInvariants
{
    public static void Validate(RunState state)
    {
        if (state.Phase == RunPhase.RunStart)
        {
            if (state.World is not null)
            {
                throw new InvalidOperationException("RunStart state must not have initialized world state.");
            }

            return;
        }

        var world = state.World
            ?? throw new InvalidOperationException("Initialized prototype run is missing world state.");

        if (!StringComparer.Ordinal.Equals(world.RulesetId, PrototypeContent.RulesetId))
        {
            throw new InvalidOperationException($"Unexpected prototype ruleset '{world.RulesetId}'.");
        }

        if (state.Player.MaxHp <= 0 || state.Player.Hp < 0 || state.Player.Hp > state.Player.MaxHp)
        {
            throw new InvalidOperationException(
                $"Invalid player HP {state.Player.Hp}/{state.Player.MaxHp}.");
        }

        if (state.Player.Gold < 0)
        {
            throw new InvalidOperationException($"Player gold is negative: {state.Player.Gold}.");
        }

        if (world.Act < 1 || world.Act > PrototypeContent.Rules.Acts)
        {
            throw new InvalidOperationException($"Invalid act {world.Act}.");
        }

        if (world.Floor < 0 || world.Floor > PrototypeContent.Rules.FloorsPerAct)
        {
            throw new InvalidOperationException($"Invalid floor {world.Floor}.");
        }

        if (state.Player.PotionSlots.Length != PrototypeContent.Rules.PotionSlots)
        {
            throw new InvalidOperationException("Potion slot count differs from the active ruleset.");
        }

        var deckIds = state.Player.Deck.Select(card => card.InstanceId).ToArray();
        if (deckIds.Length != deckIds.Distinct().Count())
        {
            throw new InvalidOperationException("Persistent deck contains duplicate card instance IDs.");
        }

        if (world.NextCardInstanceId <= deckIds.DefaultIfEmpty(0).Max())
        {
            throw new InvalidOperationException("Next card instance ID does not exceed all live card IDs.");
        }

        ValidatePhaseState(state, world);

        if (world.Combat is not null)
        {
            ValidateCombat(state.Player, world.Combat);
        }

        if (state.Phase == RunPhase.Terminal)
        {
            if (world.TerminalOutcome is not ("victory" or "defeat"))
            {
                throw new InvalidOperationException("Terminal state has no recognized outcome.");
            }

            if (world.TerminalOutcome == "defeat" && state.Player.Hp != 0)
            {
                throw new InvalidOperationException("Defeat state must have zero HP.");
            }
        }
        else if (world.TerminalOutcome is not null)
        {
            throw new InvalidOperationException("Non-terminal state carries a terminal outcome.");
        }
    }

    private static void ValidatePhaseState(RunState state, RunWorldState world)
    {
        switch (state.Phase)
        {
            case RunPhase.MapChoice:
                if (world.Map.Options.Length == 0)
                {
                    throw new InvalidOperationException("MapChoice phase has no map options.");
                }
                break;

            case RunPhase.Combat:
                if (world.Combat is null)
                {
                    throw new InvalidOperationException("Combat phase has no combat state.");
                }
                break;

            case RunPhase.Reward:
                if (world.Reward is null)
                {
                    throw new InvalidOperationException("Reward phase has no reward state.");
                }
                break;

            case RunPhase.Shop:
                if (world.Shop is null)
                {
                    throw new InvalidOperationException("Shop phase has no shop state.");
                }
                break;

            case RunPhase.Event:
                if (world.Event is null)
                {
                    throw new InvalidOperationException("Event phase has no event state.");
                }
                break;

            case RunPhase.Rest:
            case RunPhase.ActTransition:
                break;

            case RunPhase.Terminal:
                break;

            default:
                throw new InvalidOperationException($"Unexpected initialized prototype phase {state.Phase}.");
        }
    }

    private static void ValidateCombat(PlayerState player, CombatState combat)
    {
        if (combat.Turn <= 0)
        {
            throw new InvalidOperationException($"Combat turn must be positive, got {combat.Turn}.");
        }

        if (combat.Energy < 0 || combat.PlayerBlock < 0)
        {
            throw new InvalidOperationException("Combat energy/block cannot be negative.");
        }

        if (combat.Enemies.Any(enemy => enemy.Hp < 0 || enemy.Block < 0))
        {
            throw new InvalidOperationException("Enemy HP/block cannot be negative.");
        }

        var known = combat.Cards.Select(card => card.InstanceId).ToHashSet();
        if (known.Count != combat.Cards.Length)
        {
            throw new InvalidOperationException("Combat card instance IDs are not unique.");
        }

        if (combat.NextCardInstanceId <= known.DefaultIfEmpty(0).Max())
        {
            throw new InvalidOperationException("Next combat card instance ID is not fresh.");
        }

        var persistentIds = player.Deck.Select(card => card.InstanceId).ToHashSet();
        var persistentCombatCards = combat.Cards
            .Where(card => card.PersistentCardInstanceId is not null)
            .ToArray();

        if (persistentCombatCards.Length != persistentIds.Count
            || persistentCombatCards.Any(card =>
                card.PersistentCardInstanceId is null
                || !persistentIds.Contains(card.PersistentCardInstanceId.Value)))
        {
            throw new InvalidOperationException(
                "Persistent deck cards are not represented exactly once in combat.");
        }

        if (combat.Cards.Any(card => card.IsTemporary != (card.PersistentCardInstanceId is null)))
        {
            throw new InvalidOperationException("Combat temporary-card provenance is inconsistent.");
        }

        var zones = combat.Hand
            .Concat(combat.DrawPile)
            .Concat(combat.DiscardPile)
            .Concat(combat.ExhaustPile)
            .ToArray();

        var suspendedSource = combat.PendingChoice?.SourceCardInstanceId;
        var represented = suspendedSource is null
            ? zones
            : zones.Append(suspendedSource.Value).ToArray();

        if (represented.Any(cardId => !known.Contains(cardId)))
        {
            throw new InvalidOperationException("Combat zone contains a card outside the persistent deck.");
        }

        if (represented.Length != represented.Distinct().Count())
        {
            throw new InvalidOperationException("A card instance occurs in multiple combat zones/continuations.");
        }

        if (represented.Length != known.Count)
        {
            throw new InvalidOperationException("Persistent deck is not fully represented by combat state.");
        }

        if (combat.PendingChoice is not null)
        {
            var pending = combat.PendingChoice;
            var sourceZone = pending.Selection.SourceZone switch
            {
                PrototypeCardZone.Hand => combat.Hand,
                PrototypeCardZone.DrawPile => combat.DrawPile,
                PrototypeCardZone.DiscardPile => combat.DiscardPile,
                PrototypeCardZone.ExhaustPile => combat.ExhaustPile,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (pending.CandidateCardInstanceIds.Length
                != pending.CandidateCardInstanceIds.Distinct().Count())
            {
                throw new InvalidOperationException("Pending choice contains duplicate candidates.");
            }

            if (pending.CandidateCardInstanceIds.Any(cardId => !sourceZone.Contains(cardId)))
            {
                throw new InvalidOperationException("Pending choice candidate left its declared source zone.");
            }

            if (pending.Selection.MinSelections < 0
                || pending.Selection.MaxSelections < pending.Selection.MinSelections
                || pending.Selection.MaxSelections > pending.CandidateCardInstanceIds.Length)
            {
                throw new InvalidOperationException("Pending choice has an invalid selection range.");
            }
        }
    }
}
