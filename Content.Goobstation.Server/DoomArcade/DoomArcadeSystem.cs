using Content.Goobstation.Shared.DoomArcade;
using Robust.Server.GameObjects;

namespace Content.Goobstation.Server.DoomArcade;

public sealed class DoomArcadeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoomArcadeComponent, DoomArcadeScoreMessage>(OnScore);
    }

    private void OnScore(EntityUid uid, DoomArcadeComponent component, DoomArcadeScoreMessage args)
    {
        if (args.Score > component.HighScore)
        {
            component.HighScore = args.Score;
        }
    }
}
