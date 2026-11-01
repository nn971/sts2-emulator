using System.Text.Json;

namespace Sts2Emulator.Core;

public enum PrototypeCardEnchantmentKind
{
    Spiral,
    Glam,
    Inky,
    Sown,
    Slither,
    Nimble,
    Steady,
    PerfectFit,
    SoulsPower,
    TezcatarasEmber,
    Goopy,
    Clone,
    Imbued,
    Instinct,
    Swift,
    Sharp,
    Vigorous
}

public sealed record PrototypeCardEnchantment(
    PrototypeCardEnchantmentKind Kind,
    int Amount = 1,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool Disabled = false);

public sealed record CardInstance(
    long InstanceId,
    string CardId,
    int UpgradeLevel,
    JsonElement PersistentState,
    PrototypeCardEnchantment? Enchantment = null);

public sealed record RelicInstance(
    string RelicId,
    JsonElement PersistentState,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool IsWax = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool IsMelted = false);

public sealed record PotionInstance(
    string PotionId,
    JsonElement PersistentState);

public sealed record PlayerState(
    int Hp,
    int MaxHp,
    int Gold,
    CardInstance[] Deck,
    RelicInstance[] Relics,
    PotionInstance?[] PotionSlots,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    CharacterResourceState? Resources = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<RelicInstance> ActiveRelics => Relics.Where(relic => !relic.IsMelted);

    public PlayerState Fork() => this with
    {
        Deck = (CardInstance[])Deck.Clone(),
        Relics = (RelicInstance[])Relics.Clone(),
        PotionSlots = (PotionInstance?[])PotionSlots.Clone(),
        Resources = Resources?.Fork()
    };
}

/// <summary>
/// Canonical run-state envelope. World contains typed whole-run mechanics for implemented
/// rulesets; ExtensionState remains available for versioned reference data that has not
/// yet earned a stable typed representation.
/// </summary>
public sealed record RunState(
    string GameBuild,
    string EmulatorSchema,
    string RunId,
    string RunSeed,
    long DecisionIndex,
    RunPhase Phase,
    PlayerState Player,
    RngBundle Rng,
    JsonElement ExtensionState,
    RunWorldState? World = null,
    int Ascension = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    RunConfiguration? Configuration = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    RngBundle? NativeRunRng = null,
    // The native event owns its local RNG across option/page decisions.
    // Unlike NativeRunRng, this RNG has a content-derived lifetime.
    RngBundle? NativeEventRng = null,
    // Source-owned act map stream, independent of the 15 persistent streams.
    RngBundle? NativeMapRng = null,
    // The source relic-bag phase ends at UpFront counter 229;
    // source room pre-generation is retained separately below.
    NativeUpFrontRelicBagsV111.Result? NativeStartupRelicBags = null,
    // Source-ordered UpFront event/encounter/boss/Ancient pre-generation.
    NativeUpFrontRoomsV111.Result? NativeStartupRooms = null,
    // Diagnostic-only queue cursor; records expected versus actually
    // entered encounters without changing gameplay or named RNG streams.
    NativeStartupRoomQueueTraceV111.Snapshot? NativeStartupQueueTrace = null)
{
    public RunState Fork() => this with
    {
        Player = Player.Fork(),
        Rng = Rng.Fork(),
        NativeRunRng = NativeRunRng?.Fork(),
        NativeEventRng = NativeEventRng?.Fork(),
        NativeMapRng = NativeMapRng?.Fork(),
        NativeStartupRelicBags = NativeStartupRelicBags?.Fork(),
        NativeStartupRooms = NativeStartupRooms?.Fork(),
        NativeStartupQueueTrace = NativeStartupQueueTrace?.Fork(),
        ExtensionState = ExtensionState.Clone(),
        World = World?.Fork(),
        Configuration = Configuration?.Fork()
    };
}
