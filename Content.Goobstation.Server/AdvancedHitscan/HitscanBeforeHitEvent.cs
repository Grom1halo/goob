using Content.Shared.Damage;
using Robust.Shared.Map;
using Robust.Server.GameObjects;

namespace Content.Goobstation.Server.AdvancedHitscan;

/// <summary>
/// Raised on the gun entity before hitscan damage is applied.
/// Allows systems to modify damage based on distance, atmosphere, etc.
/// </summary>
[ByRefEvent]
public record struct HitscanBeforeHitEvent(
    EntityUid Gun,
    EntityUid? User,
    EntityUid Target,
    DamageSpecifier Damage,
    float Distance,
    MapCoordinates FromPosition
)
{
    /// <summary>
    /// Set to true to completely cancel the damage.
    /// </summary>
    public bool Cancelled = false;
}
