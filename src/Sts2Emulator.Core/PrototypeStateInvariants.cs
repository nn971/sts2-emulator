namespace Sts2Emulator.Core;

public static class PrototypeStateInvariants
{
    public static void Validate(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
    }
}
