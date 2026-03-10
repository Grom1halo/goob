using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.DoomArcade;

[RegisterComponent]
public sealed partial class DoomArcadeComponent : Component
{
    [DataField]
    public int HighScore = 0;
}

[Serializable, NetSerializable]
public enum DoomArcadeUiKey : byte
{
    Key
}
