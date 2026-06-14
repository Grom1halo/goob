using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Server.GameObjects;

namespace Content.Goobstation.Shared.AdvancedHitscan;

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
    public bool Cancelled = false;
}

/// <summary>
/// Raised on the gun entity when it has PenetratingHitscanComponent.
/// Handled by PenetratingHitscanSystem to fire a custom penetrating ray.
/// If Handled is set to true, normal hitscan logic is skipped.
/// </summary>
[ByRefEvent]
public record struct HitscanPenetratingFireEvent(
    EntityUid Gun,
    EntityUid? User,
    MapCoordinates FromMap,
    EntityCoordinates FromCoordinates,
    Vector2 Direction,
    Vector2 ToMap,
    int CollisionMask,
    DamageSpecifier? Damage,
    float MaxLength,
    float FireStacks,
    float StaminaDamage
)
{
    public bool Handled = false;
}
