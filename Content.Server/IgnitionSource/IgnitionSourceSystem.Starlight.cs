using System.Numerics;
using Content.Shared._Starlight.Weapons.Melee.Events;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server.IgnitionSource;

public sealed partial class IgnitionSourceSystem : SharedIgnitionSourceSystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityQuery<IgnitionSourceComponent> _ignitionQuery;
    [Dependency] private EntityQuery<TransformComponent> _transformQuery;

    private float _updateAccumulator;
    private const float UpdateInterval = 0.25f;

    private readonly HashSet<EntityUid> _activeSources = [];
    private readonly List<EntityUid> _sourceSnapshot = [];
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), IgnitionExposure> _tileExposures = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IgnitionSourceComponent, ComponentStartup>(OnSourceStartup);
        SubscribeLocalEvent<IgnitionSourceComponent, ComponentShutdown>(OnSourceShutdown);

        SubscribeLocalEvent<IgnitionSourceComponent, MeleeHitEvent>(OnMeleeHit);
    }

    // Let me swing something flaming to ignite stuff
    private void OnMeleeHit(EntityUid uid, IgnitionSourceComponent component, MeleeHitEvent args)
    {
        if (!args.IsHit || args.Handled || !component.Ignited || !component.IgniteOnMelee ||
            args.Direction is not { } direction || !TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        var originEvent = new GetMeleeOriginEvent();
        RaiseLocalEvent(args.User, originEvent);
        var origin = originEvent.Handled && originEvent.OriginEntity is { } other ? other : args.User;

        if (!_transformQuery.TryComp(origin, out var xform) || xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var position = _transform.GetWorldPosition(xform);
        var originMap = new MapCoordinates(position, xform.MapID);
        var originTile = _transform.GetGridOrMapTilePosition(origin, xform);
        _atmosphere.HotspotExpose(gridUid, originTile, component.Temperature, 50f, uid, true);

        var distance = direction.Length();
        var range = MathF.Min(melee.Range, distance);
        if (range <= 0f)
            return;

        var forward = direction / distance;
        var arcCosine = MathF.Cos((float) melee.Angle.Theta / 2f);
        var tiles = _map.GetTilesIntersecting(gridUid, grid, new Circle(position, range));
        while (tiles.MoveNext())
        {
            var tile = tiles.Current;
            if (tile.GridIndices == originTile)
                continue;

            var targetMap = _map.GridTileToWorld(gridUid, grid, tile.GridIndices);
            var offset = targetMap.Position - position;
            if (Vector2.Dot(forward, Vector2.Normalize(offset)) < arcCosine ||
                !_interaction.InRangeUnobstructed(originMap, targetMap, range,
                    predicate: entity => entity == origin || entity == args.User || entity == uid))
                continue;

            _atmosphere.HotspotExpose(gridUid, tile.GridIndices, component.Temperature, 50f, uid, true);
        }
    }

    private void OnSourceStartup(Entity<IgnitionSourceComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Ignited)
            _activeSources.Add(ent.Owner);
    }

    private void OnSourceShutdown(Entity<IgnitionSourceComponent> ent, ref ComponentShutdown args) => _activeSources.Remove(ent.Owner);

    protected override void OnIgnitionStateChanged(Entity<IgnitionSourceComponent> ent)
    {
        if (ent.Comp.Ignited)
            _activeSources.Add(ent.Owner);
        else
            _activeSources.Remove(ent.Owner);
    }
    private readonly record struct IgnitionExposure(float Temperature, EntityUid Source);
}
