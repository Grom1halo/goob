using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Goobstation.Shared.AdvancedHitscan;

/// <summary>
/// When attached to a gun entity, hitscan damage scales down
/// based on distance from shooter to target.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DistanceDamageFalloffComponent : Component
{
    /// <summary>
    /// Distance up to which full damage is dealt.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float OptimalRange = 5f;

    /// <summary>
    /// Distance at which damage drops to MinDamageMultiplier.
    /// Beyond this, MinDamageMultiplier is used.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float MaxRange = 15f;

    /// <summary>
    /// Minimum damage multiplier at MaxRange and beyond.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float MinDamageMultiplier = 0.2f;

    /// <summary>
    /// Damage multiplier per tile of smoke/water vapor the beam passes through.
    /// 0.5 means 50% damage reduction per foggy tile.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float AtmosDamageReduction = 0.5f;

    /// <summary>
    /// Minimum moles of WaterVapor on a tile to count as "foggy".
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float WaterVaporThreshold = 0.5f;

    /// <summary>
    /// If true, distance falloff is completely disabled in vacuum.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool VacuumIgnoresFalloff = true;
}
