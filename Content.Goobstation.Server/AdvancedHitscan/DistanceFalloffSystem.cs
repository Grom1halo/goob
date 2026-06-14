using System.Numerics;
using Content.Goobstation.Shared.AdvancedHitscan;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;


namespace Content.Goobstation.Server.AdvancedHitscan;

public sealed class DistanceFalloffSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DistanceDamageFalloffComponent, HitscanBeforeHitEvent>(OnBeforeHit);
    }

    private void OnBeforeHit(EntityUid uid, DistanceDamageFalloffComponent comp, ref HitscanBeforeHitEvent args)
    {
        if (args.Cancelled)
            return;

        var distance = args.Distance;
        var fromPos = args.FromPosition;

        // Check atmosphere along the ray path.
        var targetPos = _transform.GetMapCoordinates(args.Target);

        if (fromPos.MapId != targetPos.MapId)
            return;

        // Sample tiles along the beam.
        var atmosResult = AnalyzeAtmosphere(fromPos, targetPos);

        // In vacuum, ignore distance falloff entirely.
        if (comp.VacuumIgnoresFalloff && atmosResult.IsVacuum)
            return;

        // Calculate distance falloff multiplier.
        float distanceMultiplier = 1f;

        if (distance > comp.OptimalRange)
        {
            var falloffRange = comp.MaxRange - comp.OptimalRange;
            if (falloffRange > 0f)
            {
                var t = Math.Clamp((distance - comp.OptimalRange) / falloffRange, 0f, 1f);
                distanceMultiplier = MathHelper.Lerp(1f, comp.MinDamageMultiplier, t);
            }
            else
            {
                distanceMultiplier = comp.MinDamageMultiplier;
            }
        }

        // Apply atmospheric reduction (fog/steam cuts damage further).
        float atmosMultiplier = 1f;
        if (atmosResult.FoggyTiles > 0)
        {
            atmosMultiplier = MathF.Pow(comp.AtmosDamageReduction, atmosResult.FoggyTiles);
        }

        var totalMultiplier = distanceMultiplier * atmosMultiplier;

        if (totalMultiplier >= 1f)
            return;

        // Scale all damage values.
        args.Damage = ScaleDamage(args.Damage, totalMultiplier);
    }

    private AtmosAnalysis AnalyzeAtmosphere(MapCoordinates from, MapCoordinates to)
    {
        var result = new AtmosAnalysis();

        if (from.MapId != to.MapId)
            return result;

        // Get the grid we're on.
        if (!_mapManager.TryFindGridAt(from, out var gridUid, out var grid))
        {
            // No grid = space = vacuum.
            result.IsVacuum = true;
            return result;
        }

        var direction = to.Position - from.Position;
        var totalDist = direction.Length();

        if (totalDist < 0.1f)
            return result;

        var dir = direction / totalDist;

        // Sample every tile along the beam (step size = 1 tile).
        int tilesChecked = 0;
        bool allVacuum = true;

        for (float d = 0f; d <= totalDist; d += 1f)
        {
            var samplePos = from.Position + dir * d;
            var tilePos = grid.WorldToTile(samplePos);

            var mixture = _atmos.GetTileMixture((gridUid, null), null, tilePos);

            tilesChecked++;

            if (mixture == null || mixture.TotalMoles < 0.1f)
            {
                // Vacuum tile.
                continue;
            }

            allVacuum = false;

            // Check for water vapor / dense fog.
            var waterVapor = mixture.GetMoles(Gas.WaterVapor);
            if (waterVapor > 0.5f)
            {
                result.FoggyTiles++;
            }
        }

        if (tilesChecked > 0 && allVacuum)
            result.IsVacuum = true;

        return result;
    }

    private static DamageSpecifier ScaleDamage(DamageSpecifier original, float multiplier)
    {
        var scaled = new DamageSpecifier(original);
        foreach (var (type, value) in original.DamageDict)
        {
            scaled.DamageDict[type] = value * multiplier;
        }
        return scaled;
    }

    private struct AtmosAnalysis
    {
        public bool IsVacuum;
        public int FoggyTiles;
    }
}
//Get-ChildItem -Recurse -Filter *.yml -Path "Resources\Prototypes\Entities\Objects\Weapons\Guns" | ForEach-Object { Write-Host "=== $($_.FullName) ==="; Get-Content $_.FullName }
