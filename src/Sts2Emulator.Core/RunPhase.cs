namespace Sts2Emulator.Core;

public enum RunPhase
{
    Unknown = 0,
    RunStart,
    MapChoice,
    Combat,
    CardReward,
    Reward,
    Shop,
    Event,
    Rest,
    ActTransition,
    Terminal
}
