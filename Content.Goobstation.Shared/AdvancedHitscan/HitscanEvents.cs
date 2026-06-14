using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Weapons.Ranged;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Goobstation.Shared.AdvancedHitscan;

/// <summary>
/// Raised on the gun before standard hitscan logic executes.
/// If Handled is set to true, standard hitscan is skipped (used for X-Ray penetration).
/// </summary>
[ByRefEvent]
public record struct BeforeHitscanFiredEvent(
    EntityUid Gun,
    EntityUid? User,
    HitscanPrototype Hitscan,
    MapCoordinates From,
    Vector2 Direction)
{
    public bool Handled = false;
}

/// <summary>
/// Raised on the gun after a hitscan hits a target, but before damage is applied.
/// Allows modifying the damage based on distance, atmosphere, etc.
/// </summary>
[ByRefEvent]
public record struct HitscanDamageModifyEvent(
    EntityUid Gun,
    EntityUid? User,
    EntityUid Target,
    HitscanPrototype Hitscan,
    float Distance,
    DamageSpecifier? Damage)
{
    public DamageSpecifier? Damage = Damage;
}
