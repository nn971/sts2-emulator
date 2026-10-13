namespace Sts2Emulator.Core;

/// <summary>
/// Silent-compatible Brain Leech event flow shared by Overgrowth,
/// Underdocks and Hive. The class-card offer uses the configured player's
/// card pool; the colorless offer uses the configured fully unlocked
/// single-player Colorless reward pool.
/// </summary>
public static class PrototypeNativeBrainLeech
{
    public const string EventId = "proto.native.event.brain_leech";
    public const string ShareKnowledgeChoiceId = "share_knowledge";
    public const string RipChoiceId = "rip";

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Brain Leech",
        [
            new(
                ShareKnowledgeChoiceId,
                "Share Knowledge",
                [new(PrototypeRunEffectKind.OfferFiveClassCards)]),
            new(
                RipChoiceId,
                "Rip the Leech Off",
                [
                    new(PrototypeRunEffectKind.LoseHp, 5),
                    new(PrototypeRunEffectKind.OfferThreeColorlessCards)
                ])
        ],
        MinAct: 1,
        MaxAct: 2);

    public static bool IsForcedClassCardReward(
        EventState? eventState,
        RewardState reward) =>
        eventState?.EventId == EventId
        && eventState.ChosenChoiceId == ShareKnowledgeChoiceId
        && reward.SourceRoom == "Event:" + EventId;
}
