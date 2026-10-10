namespace Sts2Emulator.Core;

/// <summary>
/// Native v0.111.0 normal encounter formations that reuse existing
/// source-backed monsters. Deliberately gated from later-act routing.
/// Reference: Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class PrototypeNativeAdditionalNormals
{
    public const string TunnelerNormalId =
        "proto.native.hive.encounter.tunneler_normal";
    public const string ConstructMenagerieNormalId =
        "proto.native.glory.encounter.construct_menagerie_normal";

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        // TunnelerNormal.GenerateMonsters: a Chomper that screams first,
        // then a Tunneler. The "screech-first" slot is the same initial
        // phase already source-backed for ChompersNormal.
        new(TunnelerNormalId, PrototypeRoomType.Combat,
            [PrototypeNativeHiveNormals.ChomperId,
                "proto.native.hive.tunneler"],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new(PrototypeNativeHiveNormals.ChomperId, 0,
                    "screech-first"),
                new("proto.native.hive.tunneler", 1)
            ]),
        // ConstructMenagerieNormal.GenerateMonsters: one PunchConstruct
        // followed by two independently acting CubexConstructs.
        new(ConstructMenagerieNormalId, PrototypeRoomType.Combat,
            ["proto.enemy.punch_construct",
                "proto.enemy.cubex_construct",
                "proto.enemy.cubex_construct"],
            MinAct: 3, MaxAct: 3, Weight: 0,
            Formation:
            [
                new("proto.enemy.punch_construct", 0),
                new("proto.enemy.cubex_construct", 1),
                new("proto.enemy.cubex_construct", 2)
            ])
    ];
}
