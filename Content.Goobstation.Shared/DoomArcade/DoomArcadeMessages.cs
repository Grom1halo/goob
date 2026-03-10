using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.DoomArcade;

[Serializable, NetSerializable]
public sealed class DoomArcadeScoreMessage : BoundUserInterfaceMessage
{
    public int Score;

    public DoomArcadeScoreMessage(int score)
    {
        Score = score;
    }
}
