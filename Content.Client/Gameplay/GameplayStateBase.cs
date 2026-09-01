using System.Linq;
using System.Numerics;
using Content.Client.Clickable;
using Content.Client.Sprite;
using Content.Client.UserInterface;
using Content.Client.Viewport;
using Content.Client.ZLevels;
using Content.Shared.CCVar;
using Content.Shared.Input;
using Content.Shared.ZLevels;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using YamlDotNet.Serialization.TypeInspectors;

namespace Content.Client.Gameplay
{
    // OH GOD.
    // Ok actually it's fine.
    // Instantiated dynamically through the StateManager, Dependencies will be resolved.
    [Virtual]
    public partial class GameplayStateBase : State, IEntityEventSubscriber
    {
        [Dependency] private IEyeManager _eyeManager = default!;
        [Dependency] private IInputManager _inputManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IEntitySystemManager _entitySystemManager = default!;
        [Dependency] private IGameTiming _timing = default!;
        [Dependency] protected IUserInterfaceManager UserInterfaceManager = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IViewVariablesManager _vvm = default!;
        [Dependency] private IConsoleHost _conHost = default!;
        [Dependency] private IConfigurationManager _configurationManager = default!;

        private ClickableEntityComparer _comparer = default!;

        private (ViewVariablesPath? path, string[] segments) ResolveVvHoverObject(string path)
        {
            var segments = path.Split('/');
            var uid = RecursivelyFindUiEntity(UserInterfaceManager.CurrentlyHovered);
            var netUid = _entityManager.GetNetEntity(uid);
            return (netUid != null ? new ViewVariablesInstancePath(netUid) : null, segments);
        }

        private EntityUid? RecursivelyFindUiEntity(Control? control)
        {
            if (control == null)
                return null;

            switch (control)
            {
                case IViewportControl vp:
                    if (_inputManager.MouseScreenPosition.IsValid)
                        return GetClickedEntity(vp.PixelToMap(_inputManager.MouseScreenPosition.Position));
                    return null;
                case SpriteView sprite:
                    return sprite.Entity;
                case IEntityControl ui:
                    return ui.UiEntity;
            }

            return RecursivelyFindUiEntity(control.Parent);
        }

        private IEnumerable<string>? ListVVHoverPaths(string[] segments)
        {
            return null;
        }

        protected override void Startup()
        {
            _vvm.RegisterDomain("enthover", ResolveVvHoverObject, ListVVHoverPaths);
            _inputManager.KeyBindStateChanged += OnKeyBindStateChanged;
            _comparer = new ClickableEntityComparer();
            CommandBinds.Builder
                .Bind(ContentKeyFunctions.InspectEntity, new PointerInputCmdHandler(HandleInspect, outsidePrediction: true))
                .Bind(ContentKeyFunctions.InspectServerComponent, new PointerInputCmdHandler(HandleInspectServerComponent, outsidePrediction: true))
                .Bind(ContentKeyFunctions.InspectClientComponent, new PointerInputCmdHandler(HandleInspectClientComponent, outsidePrediction: true))
                .Register<GameplayStateBase>();
        }

        protected override void Shutdown()
        {
            _vvm.UnregisterDomain("enthover");
            _inputManager.KeyBindStateChanged -= OnKeyBindStateChanged;
            CommandBinds.Unregister<GameplayStateBase>();
        }

        private bool HandleInspect(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            _conHost.ExecuteCommand($"vv /c/enthover");
            return true;
        }

        private bool HandleInspectServerComponent(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            var component = _configurationManager.GetCVar(CCVars.DebugQuickInspect);
            if (_entityManager.TryGetNetEntity(uid, out var net))
                _conHost.ExecuteCommand($"vv /entity/{net.Value.Id}/{component}");
            return true;
        }

        private bool HandleInspectClientComponent(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            var component = _configurationManager.GetCVar(CCVars.DebugQuickInspect);
            _conHost.ExecuteCommand($"vv /c/entity/{uid}/{component}");
            return true;
        }

        public EntityUid? GetClickedEntity(MapCoordinates coordinates)
        {
            return GetClickedEntity(coordinates, _eyeManager.CurrentEye);
        }

        public EntityUid? GetClickedEntity(MapCoordinates coordinates, IEye? eye)
            => GetClickedEntity(coordinates, eye, GetDefaultVisibleMaps());

        public EntityUid? GetClickedEntity(
            MapCoordinates coordinates,
            IEye? eye,
            IReadOnlySet<EntityUid>? visibleMaps)
        {
            if (eye == null)
                return null;

            var first = GetClickableEntities(
                coordinates,
                eye,
                excludeFaded: true,
                visibleMaps: visibleMaps).FirstOrDefault();
            return first.IsValid() ? first : null;
        }

        public IEnumerable<EntityUid> GetClickableEntities(EntityCoordinates coordinates, bool excludeFaded = true)
        {
            var transformSystem = _entitySystemManager.GetEntitySystem<SharedTransformSystem>();
            return GetClickableEntities(transformSystem.ToMapCoordinates(coordinates), excludeFaded);
        }

        public IEnumerable<EntityUid> GetClickableEntities(MapCoordinates coordinates, bool excludeFaded = true)
        {
            return GetClickableEntities(coordinates, _eyeManager.CurrentEye, excludeFaded);
        }

        public IEnumerable<EntityUid> GetClickableEntities(MapCoordinates coordinates, IEye? eye, bool excludeFaded = true)
            => GetClickableEntities(coordinates, eye, excludeFaded, GetDefaultVisibleMaps());

        public IEnumerable<EntityUid> GetClickableEntities(
            MapCoordinates coordinates,
            IEye? eye,
            bool excludeFaded,
            IReadOnlySet<EntityUid>? visibleMaps)
        {
            /*
             * TODO:
             * 1. Stuff like MeleeWeaponSystem need an easy way to hook into viewport specific entities / entities under mouse
             * 2. Cleanup the mess around InteractionOutlineSystem + below the keybind click detection.
             */

            if (eye == null)
                return Array.Empty<EntityUid>();

            var mapSystem = _entityManager.System<MapSystem>();
            var viewedMap = mapSystem.GetMapOrInvalid(coordinates.MapId);
            if (viewedMap == EntityUid.Invalid)
                return Array.Empty<EntityUid>();

            var zLevels = _entityManager.System<ZLevelSystem>();
            var layers = GetVisibleLayers(viewedMap, coordinates.MapId, visibleMaps, zLevels);
            if (layers.Count == 0)
                return Array.Empty<EntityUid>();

            var spriteTree = _entityManager.EntitySysManager.GetEntitySystem<SpriteTreeSystem>();
            var transforms = _entityManager.System<TransformSystem>();
            var clickables = _entityManager.System<ClickableSystem>();
            var surfaceProjection = _entityManager.System<ZLevelSurfaceProjectionSystem>();
            var lookup = _entityManager.System<EntityLookupSystem>();
            var foundEntities = new Dictionary<EntityUid, ProjectedClickHit>();
            var surfaceCandidates = new HashSet<Entity<ZLevelTopSurfaceVisualComponent>>();
            Span<Vector2> surfacePolygon = stackalloc Vector2[4];

            foreach (var layer in layers)
            {
                if (!zLevels.TryUnprojectMapLayerPosition(
                        viewedMap,
                        layer.Map,
                        coordinates.Position,
                        out var canonicalPosition))
                {
                    canonicalPosition = coordinates.Position;
                }

                var canonicalBounds = Box2.CenteredAround(canonicalPosition, new Vector2(3f, 3f));
                var clickBounds = transforms.GetRenderCullingBounds(layer.MapId, canonicalBounds);
                var entities = spriteTree.QueryAabb(layer.MapId, clickBounds);

                foreach (var entity in entities)
                {
                    var absoluteZ = transforms.GetRenderWorldPose(entity.Uid, entity.Transform).AbsoluteZ;
                    if (layers.Count > 1 && visibleMaps != null)
                    {
                        if (!transforms.TryGetPresentedViewSample(
                                entity.Uid,
                                viewedMap,
                                visibleMaps,
                                out var presented,
                                entity.Transform))
                        {
                            continue;
                        }

                        absoluteZ = presented.AbsoluteZ;
                    }

                    if (!clickables.CheckClick(
                            (entity.Uid, null, entity.Component, entity.Transform),
                            coordinates.Position,
                            eye,
                            viewedMap,
                            excludeFaded,
                            out var drawDepthClicked,
                            out var renderOrder,
                            out var bottom))
                    {
                        continue;
                    }

                    AddProjectedHit(
                        foundEntities,
                        new ProjectedClickHit(entity.Uid, absoluteZ, drawDepthClicked, renderOrder, bottom));
                }

                surfaceCandidates.Clear();
                lookup.GetEntitiesIntersecting(layer.MapId, canonicalBounds, surfaceCandidates);
                foreach (var surface in surfaceCandidates)
                {
                    if (!_entityManager.HasComponent<ClickableComponent>(surface.Owner) ||
                        !_entityManager.TryGetComponent(surface.Owner, out SpriteComponent? sprite) ||
                        !sprite.Visible ||
                        excludeFaded && _entityManager.HasComponent<FadingSpriteComponent>(surface.Owner))
                    {
                        continue;
                    }

                    if (!surfaceProjection.TryGetProjectedSurface(
                            (surface.Owner, null),
                            viewedMap,
                            surfacePolygon,
                            out var count,
                            out var absoluteHeight) ||
                        !surfaceProjection.TryGetSurfaceLayer(surface.Owner, absoluteHeight, out var surfaceLayer) ||
                        !VisibleMapsOrViewedContains(visibleMaps, viewedMap, surfaceLayer) ||
                        !ZLevelSurfaceProjectionSystem.ContainsPoint(surfacePolygon[..count], coordinates.Position))
                    {
                        continue;
                    }

                    var bottom = float.PositiveInfinity;
                    for (var i = 0; i < count; i++)
                        bottom = MathF.Min(bottom, surfacePolygon[i].Y);

                    AddProjectedHit(
                        foundEntities,
                        new ProjectedClickHit(
                            surface.Owner,
                            absoluteHeight,
                            sprite.DrawDepth,
                            sprite.RenderOrder,
                            bottom));
                }
            }

            if (foundEntities.Count == 0)
                return Array.Empty<EntityUid>();

            var sorted = foundEntities.Values.ToList();
            sorted.Sort(_comparer);

            return sorted.Select(hit => hit.Entity);
        }

        private IReadOnlySet<EntityUid>? GetDefaultVisibleMaps()
        {
            return _eyeManager.MainViewport switch
            {
                ScalingViewport scaling => scaling.VisibleZMaps,
                ViewportContainer viewport => viewport.Viewport?.VisibleZMaps,
                _ => null,
            };
        }

        private List<ProjectedMapLayer> GetVisibleLayers(
            EntityUid viewedMap,
            MapId viewedMapId,
            IReadOnlySet<EntityUid>? visibleMaps,
            ZLevelSystem zLevels)
        {
            var mapQuery = _entityManager.GetEntityQuery<MapComponent>();
            var layers = new List<ProjectedMapLayer>();
            if (!zLevels.TryGetMapData(viewedMap, out var viewed, out _))
            {
                layers.Add(new ProjectedMapLayer(viewedMap, viewedMapId, 0));
                return layers;
            }

            if (visibleMaps == null || visibleMaps.Count == 0)
            {
                layers.Add(new ProjectedMapLayer(viewedMap, viewedMapId, viewed.Depth));
                return layers;
            }

            foreach (var map in visibleMaps)
            {
                if (!mapQuery.TryComp(map, out var mapComponent) ||
                    !zLevels.TryGetMapData(map, out var data, out _) ||
                    data.Network != viewed.Network)
                {
                    continue;
                }

                layers.Add(new ProjectedMapLayer(map, mapComponent.MapId, data.Depth));
            }

            layers.Sort(static (a, b) =>
            {
                var depth = b.Depth.CompareTo(a.Depth);
                return depth != 0 ? depth : b.Map.CompareTo(a.Map);
            });
            return layers;
        }

        private static bool VisibleMapsOrViewedContains(
            IReadOnlySet<EntityUid>? visibleMaps,
            EntityUid viewedMap,
            EntityUid layer)
            => visibleMaps?.Contains(layer) ?? layer == viewedMap;

        private static void AddProjectedHit(
            Dictionary<EntityUid, ProjectedClickHit> hits,
            ProjectedClickHit hit)
        {
            if (!hits.TryGetValue(hit.Entity, out var existing) ||
                hit.AbsoluteZ > existing.AbsoluteZ)
            {
                hits[hit.Entity] = hit;
            }
        }

        private sealed class ClickableEntityComparer : IComparer<ProjectedClickHit>
        {
            public int Compare(ProjectedClickHit x, ProjectedClickHit y)
                => ZLevelProjectedPicking.Compare(x.Key, y.Key);
        }

        private readonly record struct ProjectedClickHit(
            EntityUid Entity,
            float AbsoluteZ,
            int Depth,
            uint RenderOrder,
            float Bottom)
        {
            public ZLevelProjectedPickKey Key => new(Entity, AbsoluteZ, Depth, RenderOrder, Bottom);
        }

        private readonly record struct ProjectedMapLayer(EntityUid Map, MapId MapId, int Depth);

        private EntityCoordinates ResolveProjectedCoordinates(
            MapCoordinates displayed,
            EntityUid? clicked,
            IReadOnlySet<EntityUid>? visibleMaps)
        {
            var mapSystem = _entityManager.System<MapSystem>();
            var transformSystem = _entityManager.System<TransformSystem>();
            var sharedTransforms = _entitySystemManager.GetEntitySystem<SharedTransformSystem>();
            var zLevels = _entityManager.System<ZLevelSystem>();
            var viewedMap = mapSystem.GetMapOrInvalid(displayed.MapId);
            if (viewedMap == EntityUid.Invalid)
                return EntityCoordinates.Invalid;

            if (clicked is { } target &&
                _entityManager.TryGetComponent(target, out TransformComponent? targetXform) &&
                targetXform.MapUid != null)
            {
                var absoluteHeight = transformSystem
                    .GetRenderWorldPoseForLayer(target, viewedMap, targetXform)
                    .AbsoluteZ;

                if (_entityManager.HasComponent<ZLevelTopSurfaceVisualComponent>(target))
                {
                    var surfaceProjection = _entityManager.System<ZLevelSurfaceProjectionSystem>();
                    Span<Vector2> polygon = stackalloc Vector2[4];
                    if (surfaceProjection.TryGetProjectedSurface(
                            (target, null),
                            viewedMap,
                            polygon,
                            out var count,
                            out var surfaceHeight) &&
                        ZLevelSurfaceProjectionSystem.ContainsPoint(polygon[..count], displayed.Position))
                    {
                        absoluteHeight = surfaceHeight;
                    }
                }

                if (zLevels.TryUnprojectAbsolutePosition(
                        viewedMap,
                        displayed.Position,
                        absoluteHeight,
                        out var canonical))
                {
                    return ToEntityCoordinates(
                        new MapCoordinates(canonical, targetXform.MapID),
                        mapSystem,
                        sharedTransforms);
                }
            }

            foreach (var layer in GetVisibleLayers(viewedMap, displayed.MapId, visibleMaps, zLevels))
            {
                if (!zLevels.TryUnprojectMapLayerPosition(
                        viewedMap,
                        layer.Map,
                        displayed.Position,
                        out var canonical))
                {
                    canonical = displayed.Position;
                }

                var mapCoordinates = new MapCoordinates(canonical, layer.MapId);
                if (!mapSystem.TryFindGridAt(mapCoordinates, out var gridUid, out var grid) ||
                    !mapSystem.TryGetTileRef(gridUid, grid, canonical, out var tile) ||
                    tile.Tile.IsEmpty)
                {
                    continue;
                }

                return mapSystem.MapToGrid(gridUid, mapCoordinates);
            }

            return sharedTransforms.ToCoordinates(displayed);
        }

        private static EntityCoordinates ToEntityCoordinates(
            MapCoordinates coordinates,
            MapSystem mapSystem,
            SharedTransformSystem transforms)
        {
            return mapSystem.TryFindGridAt(coordinates, out var gridUid, out _)
                ? mapSystem.MapToGrid(gridUid, coordinates)
                : transforms.ToCoordinates(coordinates);
        }

        /// <summary>
        ///     Converts a state change event from outside the simulation to inside the simulation.
        /// </summary>
        /// <param name="args">Event data values for a bound key state change.</param>
        protected virtual void OnKeyBindStateChanged(ViewportBoundKeyEventArgs args)
        {
            // If there is no InputSystem, then there is nothing to forward to, and nothing to do here.
            if(!_entitySystemManager.TryGetEntitySystem(out InputSystem? inputSys))
                return;

            var kArgs = args.KeyEventArgs;
            var func = kArgs.Function;
            var funcId = _inputManager.NetworkBindMap.KeyFunctionID(func);

            EntityCoordinates coordinates = default;
            EntityUid? entityToClick = null;
            if (args.Viewport is IViewportControl vp && kArgs.PointerLocation.IsValid)
            {
                var mousePosWorld = vp.PixelToMap(kArgs.PointerLocation.Position);
                IReadOnlySet<EntityUid>? visibleMaps = null;

                if (vp is ScalingViewport svp)
                {
                    visibleMaps = svp.VisibleZMaps;
                    entityToClick = GetClickedEntity(mousePosWorld, svp.Eye, visibleMaps);
                }
                else
                {
                    entityToClick = GetClickedEntity(mousePosWorld);
                    visibleMaps = GetDefaultVisibleMaps();
                }

                coordinates = ResolveProjectedCoordinates(mousePosWorld, entityToClick, visibleMaps);
            }
            else
            {
                coordinates = EntityCoordinates.Invalid;
            }

            var message = new ClientFullInputCmdMessage(_timing.CurTick, _timing.TickFraction, funcId)
            {
                State = kArgs.State,
                Coordinates = coordinates,
                ScreenCoordinates = kArgs.PointerLocation,
                Uid = entityToClick ?? default,
            }; // TODO make entityUid nullable

            // client side command handlers will always be sent the local player session.
            var session = _playerManager.LocalSession;
            if (inputSys.HandleInputCommand(session, func, message))
            {
                kArgs.Handle();
            }
        }
    }
}
