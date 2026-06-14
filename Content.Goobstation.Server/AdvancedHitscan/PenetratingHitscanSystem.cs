using System.Numerics;
using System.Linq;
using Content.Goobstation.Shared.AdvancedHitscan;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Server.GameObjects;
using Content.Shared.Tag;

namespace Content.Goobstation.Server.AdvancedHitscan;

public sealed class PenetratingHitscanSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PenetratingHitscanComponent, BeforeHitscanFiredEvent>(OnBeforeHitscanFired);
    }

    private void OnBeforeHitscanFired(EntityUid uid, PenetratingHitscanComponent comp, ref BeforeHitscanFiredEvent args)
    {
        args.Handled = true;

        var hitscan = args.Hitscan;
        if (hitscan.Damage == null)
            return;

        var baseDamage = FilterDamage(hitscan.Damage, comp.AllowedDamageTypes);
        if (baseDamage.Empty)
            return;

        var dir = args.Direction.Normalized();
        var ray = new CollisionRay(args.From.Position, dir, hitscan.CollisionMask);

        var allHits = _physics.IntersectRay(args.From.MapId, ray, hitscan.MaxLength, args.User ?? uid, false)
            .ToList();

        float currentMultiplier = 1f;
        int penetrations = 0;
        var hitEntities = new HashSet<EntityUid>();

        foreach (var hit in allHits)
        {
            if (penetrations >= comp.MaxPenetrations)
                break;

            if (currentMultiplier <= 0.01f)
                break;

            var hitEntity = hit.HitEntity;

            if (!hitEntities.Add(hitEntity))
                continue;

            // Check if blocked by special tags (Plasteel, RadiationShield)
            // Check if blocked by special tags (Plasteel, RadiationShield)
            bool blocked = false;
            if (TryComp<TagComponent>(hitEntity, out var tagComp))
            {
                foreach (var tag in comp.BlockedByTags)
                {
                    if (_tags.HasTag(tagComp, tag))
                    {
                        blocked = true;
                        break;
                    }
                }
            }

            if (blocked)
                break;

            // Walls and structures: penetrate through, reduce damage, but don't damage them
            if (IsWallOrStructure(hitEntity))
            {
                currentMultiplier *= (1f - comp.PenetrationFalloff);
                penetrations++;
                continue;
            }

            // Living entities: deal filtered damage
            var scaledDamage = ScaleDamage(baseDamage, currentMultiplier);
            _damageable.TryChangeDamage(hitEntity, scaledDamage, origin: args.User);

            // Entities also reduce beam slightly
            currentMultiplier *= (1f - comp.PenetrationFalloff * 0.5f);
            penetrations++;
        }
    }

    private bool IsWallOrStructure(EntityUid uid)
    {
        // If it doesn't have DamageableComponent, it's a structure (wall, grille, etc.)
        if (!HasComp<DamageableComponent>(uid))
            return true;

        return false;
    }

    private static DamageSpecifier FilterDamage(DamageSpecifier original, List<string> allowedTypes)
    {
        var filtered = new DamageSpecifier();
        foreach (var (type, value) in original.DamageDict)
        {
            if (value > 0 && allowedTypes.Contains(type))
                filtered.DamageDict[type] = value;
        }
        return filtered;
    }

    private static DamageSpecifier ScaleDamage(DamageSpecifier original, float multiplier)
    {
        var scaled = new DamageSpecifier(original);
        foreach (var (type, value) in original.DamageDict)
            scaled.DamageDict[type] = value * multiplier;
        return scaled;
    }
}
