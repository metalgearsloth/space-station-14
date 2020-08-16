#nullable enable
using System;
using System.Collections.Generic;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Physics;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Timers;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Movement
{
    /// <summary>
    ///     Entity goes in, entity goes out. You can't explain that.
    /// </summary>
    [RegisterComponent]
    internal class PortalComponent : Component, ICollideBehavior
    {
        public override string Name => "Portal";

        /// <summary>
        ///     Cooldowns so we're not spamming the shit out of intersecting entities.
        /// </summary>
        private Dictionary<IEntity, TimeSpan> _lastPortal = new Dictionary<IEntity, TimeSpan>();

        private const float PortalCooldown = 0.5f;

        /// <summary>
        ///     Where the portal links to.
        /// </summary>
        public IEntity? Destination { get; private set; }

        private float? _duration;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _duration, "duration", null);
        }

        public override void Initialize()
        {
            base.Initialize();
            Owner.EnsureComponent<SnapGridComponent>();
            Owner.EnsureComponent<CollidableComponent>();
            
            if (_duration != null)
            {
                DebugTools.Assert(_duration > 0.0f);
                
                Timer.Spawn(TimeSpan.FromSeconds(_duration.Value), () =>
                {
                    if (!Owner.Deleted && !Deleted)
                    {
                        Owner.Delete();
                    }
                });
            }
        }

        public static bool CanSpawnOn(IEntity entity)
        {
            var entityPosition = IoCManager.Resolve<IMapManager>()
                .GetGrid(entity.Transform.GridID)
                .GetTileRef(entity.Transform.GridPosition).GridIndices;
            var componentManager = IoCManager.Resolve<IComponentManager>();
            foreach (var portal in componentManager.EntityQuery<PortalComponent>())
            {
                if (portal.Owner.Transform.GridID != entity.Transform.GridID) 
                    continue;

                if (entityPosition != portal.Owner.GetComponent<SnapGridComponent>().Position)
                    continue;

                return false;
            }
            return true;
        }

        public void ConnectTo(IEntity entity)
        {
            DebugTools.Assert(entity.HasComponent<PortalComponent>());
            Destination = entity;
        }

        public void PortalIntersecting()
        {
            var entityManager = IoCManager.Resolve<IEntityManager>();
            foreach (var entityIntersecting in entityManager.GetEntitiesIntersecting(Owner.Transform.MapPosition))
            {
                TryPortal(entityIntersecting);
            }
        }

        public void CollideWith(IEntity collidedWith)
        {
            Destination?.GetComponent<PortalComponent>().TryPortal(collidedWith);
        }

        // TODO: We need a "recentlyportaled" system to track if we're still intersecting, and when we're not stop tracking us
        public bool TryPortal(IEntity entity)
        {
            if (Destination == null || 
                Destination.Deleted)
            {
                Destination = null;
                return false;
            }
            
            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime;
            if (!entity.HasComponent<TeleportableComponent>() || 
                _lastPortal.TryGetValue(entity, out var lastPortal) && (currentTime - lastPortal).TotalSeconds < PortalCooldown ||
                Destination.GetComponent<PortalComponent>()._lastPortal.TryGetValue(entity, out var destLastPortal) && (currentTime - destLastPortal).TotalSeconds < PortalCooldown)
            {
                return false;
            }

            // If we enter portal left side we'll try to exit them right side on the other end.
            var relativeDirection =
                (Owner.Transform.MapPosition.Position - entity.Transform.MapPosition.Position).GetDir();

            var mapManager = IoCManager.Resolve<IMapManager>();

            if (!mapManager.TryFindGridAt(Destination.Transform.MapPosition, out var grid))
                return false;

            if (!EntitySystem.Get<PortalSystem>().CanPortal(entity))
                return false;

            // Essentially if we enter left side of portal try and exit right side of destination.
            // We'll check for any blockers on that tile.
            var destination = Destination.Transform.MapPosition.Position + relativeDirection.ToVec();
            var targetTile = grid.GetTileRef(Destination.Transform.GridPosition.Offset(relativeDirection.ToVec()));
            var targetBox = new Box2(targetTile.X * grid.TileSize, targetTile.Y * grid.TileSize,
                (targetTile.X + 1) * grid.TileSize, (targetTile.Y + 1) * grid.TileSize);

            // Check if there's anything blocking us.
            if (entity.TryGetComponent(out ICollidableComponent collidableComponent))
            {
                var entityManager = IoCManager.Resolve<IEntityManager>();

                foreach (var blocker in entityManager.GetEntitiesIntersecting(Destination.Transform.MapID, targetBox))
                {
                    if (blocker == Destination)
                        continue;
                    
                    if (!blocker.TryGetComponent(out ICollidableComponent targetCollidable))
                        continue;

                    if ((collidableComponent.CollisionLayer & targetCollidable.CollisionMask) != 0)
                        return false;
                }
            }

            _lastPortal[entity] = currentTime;
            Destination.GetComponent<PortalComponent>()._lastPortal[entity] = currentTime;
            Portal(entity, destination);
            EntitySystem.Get<PortalSystem>().Portalled(entity, Destination);
            return true;
        }

        private void Portal(IEntity entity, Vector2 worldPosition)
        {
            DebugTools.AssertNotNull(Destination);
            if (Destination.Transform.Parent != null)
            {
                entity.Transform.AttachParent(Destination.Transform.Parent);
            }
            
            entity.Transform.WorldPosition = worldPosition;
        }
    }
}