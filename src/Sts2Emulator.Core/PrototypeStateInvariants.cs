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

        var known = player.Deck.Select(card => card.InstanceId).ToHashSet();
        var zones = combat.Hand
            .Concat(combat.DrawPile)
            .Concat(combat.DiscardPile)
            .Concat(combat.ExhaustPile)
            .ToArray();

        if (zones.Any(cardId => !known.Contains(cardId)))
        {
            throw new InvalidOperationException("Combat zone contains a card outside the persistent deck.");
        }

        if (zones.Length != zones.Distinct().Count())
        {
            throw new InvalidOperationException("A card instance occurs in multiple combat zones.");
        }

        if (zones.Length != known.Count)
        {
            throw new InvalidOperationException("Persistent deck is not fully partitioned across combat zones.");
        }
    }
}
