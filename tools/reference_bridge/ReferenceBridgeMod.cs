using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace Sts2ReferenceBridge;

[ModInitializer(nameof(Initialize))]
public partial class ReferenceBridgeMod : Node
{
    public static void Initialize()
    {
        PassiveReferenceRecorder.Initialize();
    }
}
