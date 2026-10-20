namespace Sts2Emulator.Core;

/// <summary>
/// Shared v0.111.0 events used by the Silent regional route.
/// </summary>
public static class PrototypeNativeSharedEvents
{
    public const string ThisOrThatId = "proto.native.event.this_or_that";
    public const string ThisOrThatNativeRngEventId = "THIS_OR_THAT";
    public const string ClumsyCardId = NativeGloryAncientsV111.ClumsyCardId;

    public static PrototypeEventDefinition ThisOrThatDefinition =>
        PrototypeDefinitionCopiesV111.CloneEventDefinition(
            ThisOrThatDefinitionValue);

    private static readonly PrototypeEventDefinition
        ThisOrThatDefinitionValue =
        new(
            ThisOrThatId,
            "This or That",
            [
                new(
                    "plain",
                    "Lose 6 HP and gain 41–68 gold",
                    [
                        new(PrototypeRunEffectKind.LoseHp, 6),
                        new(PrototypeRunEffectKind.GainGold)
                    ]),
                new(
                    "ornate",
                    "Gain a relic and add Clumsy",
                    [
                        new(PrototypeRunEffectKind.GainRandomRelic),
                        new(PrototypeRunEffectKind.AddCardAfterDeckChoices,
                            CardId: ClumsyCardId)
                    ])
            ],
            MinAct: 1,
            MaxAct: 3);

    public static bool IsSupported(string eventId) =>
        StringComparer.Ordinal.Equals(eventId, ThisOrThatId);
}
