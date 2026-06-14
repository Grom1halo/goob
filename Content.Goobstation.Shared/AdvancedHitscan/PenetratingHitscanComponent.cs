using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Goobstation.Shared.AdvancedHitscan;

/// <summary>
/// When attached to a gun, its hitscan beam penetrates through walls and entities,
/// dealing reduced damage with each penetration.
/// Blocked only by entities/tags in BlockedByTags.
/// Damage is filtered to only Radiation and Cellular types.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PenetratingHitscanComponent : Component
{
    /// <summary>
    /// Damage multiplier lost per penetrated wall.
    /// 0.25 means each wall reduces remaining damage by 25%.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float PenetrationFalloff = 0.25f;

    /// <summary>
    /// Tags that completely stop the beam.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public List<string> BlockedByTags = new() { "Plasteel", "RadiationShield" };

    /// <summary>
    /// Maximum number of entities/walls the beam can penetrate.
    /// Safety limit to prevent infinite loops.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public int MaxPenetrations = 5;

    /// <summary>
    /// Only these damage types are applied. All others are stripped.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public List<string> AllowedDamageTypes = new() { "Radiation", "Cellular" };
}
