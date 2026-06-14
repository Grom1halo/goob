using Content.Goobstation.Shared.AdvancedHitscan;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using System;
using Robust.Server.GameObjects;
using Content.Shared.Atmos.Components;

namespace Content.Goobstation.Server.AdvancedHitscan;

public sealed class DistanceDamageFalloffSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DistanceDamageFalloffComponent, HitscanDamageModifyEvent>(OnHitscanDamageModify);
    }

    private void OnHitscanDamageModify(EntityUid uid, DistanceDamageFalloffComponent comp, ref HitscanDamageModifyEvent args)
    {
        if (args.Damage == null)
            return;

        // Use the distance from the ray hit result, passed by GunSystem
        var totalDistance = args.Distance;
        if (totalDistance <= 0)
            return;

        var startPos = _transform.GetMapCoordinates(args.Gun);
        var targetPos = _transform.GetMapCoordinates(args.Target);

        if (startPos.MapId != targetPos.MapId)
            return;

        // Scan atmosphere along the ray path
        var diff = targetPos.Position - startPos.Position;
        var rayLength = diff.Length();

        int vaporTiles = 0;
        bool isVacuum = true;

        if (rayLength > 0)
        {
            var dir = diff.Normalized();
            int steps = (int) MathF.Ceiling(rayLength);

            for (int i = 0; i <= steps; i++)
            {
                var samplePos = startPos.Position + dir * MathF.Min(i, rayLength);
                var mapCoords = new MapCoordinates(samplePos, startPos.MapId);

                if (!_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var mapGrid))
                    continue;

                var mapUid = _mapManager.GetMapEntityId(startPos.MapId);
                var tile = _mapSystem.GetTileRef(gridUid, mapGrid, mapCoords);

                Entity<GridAtmosphereComponent?, GasTileOverlayComponent?> gridEnt = (gridUid, null, null);
                Entity<MapAtmosphereComponent?> mapEnt = (mapUid, null);

                var mix = _atmos.GetTileMixture(gridEnt, mapEnt, tile.GridIndices, true);

                if (mix == null)
                    continue;

                if (mix.TotalMoles > 0.5f)
                {
                    isVacuum = false;

                    // Check for water vapor (steam/smoke)
                    if (mix.GetMoles(Gas.WaterVapor) > 1f)
                    {
                        vaporTiles++;
                    }
                }
            }
        }

        // 1. Distance falloff
        float distanceMultiplier = 1f;

        if (!(comp.VacuumIgnoresFalloff && isVacuum))
        {
            if (totalDistance > comp.OptimalRange)
            {
                if (totalDistance >= comp.MaxRange)
                {
                    distanceMultiplier = comp.MinDamageMultiplier;
                }
                else
                {
                    var rangeDiff = comp.MaxRange - comp.OptimalRange;
                    if (rangeDiff > 0)
                    {
                        var distDiff = totalDistance - comp.OptimalRange;
                        var ratio = distDiff / rangeDiff;
                        distanceMultiplier = 1f - (1f - comp.MinDamageMultiplier) * ratio;
                    }
                }
            }
        }

        // 2. Atmosphere (vapor/smoke) falloff
        float atmosMultiplier = 1f;
        if (vaporTiles > 0)
        {
            atmosMultiplier = MathF.Pow(comp.AtmosDamageReduction, vaporTiles);
        }

        float finalMultiplier = distanceMultiplier * atmosMultiplier;

        if (finalMultiplier < 0.99f)
        {
            args.Damage *= finalMultiplier;
        }
    }
}
