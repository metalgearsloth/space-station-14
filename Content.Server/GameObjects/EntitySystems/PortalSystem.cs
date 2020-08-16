#nullable enable
using System.Collections.Generic;
using Content.Server.GameObjects.Components.GUI;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Physics;
using Robust.Shared.IoC;

namespace Content.Server.GameObjects.EntitySystems
{
    /// <summary>
    ///     Need to track entities that get portalled so they don't get portalled back and forth continuously.
    /// </summary>
    internal sealed class PortalSystem : EntitySystem
    {
        [Dependency] private readonly IPhysicsManager _physicsManager = default!;

        /// <summary>
        ///     Portalled entity, destination portal
        /// </summary>
        private Dictionary<IEntity, IEntity> _portalled = new Dictionary<IEntity, IEntity>();

        public bool CanPortal(IEntity entity) => !_portalled.ContainsKey(entity);

        public void Portalled(IEntity portalled, IEntity portal)
        {
            _portalled[portalled] = portal;
        }

        public override void Update(float frameTime)
        {
            // TODO: Fix dis shit coz it dun werk
            base.Update(frameTime);
            if (_portalled.Count == 0)
                return;
            
            var removals = new List<IEntity>(0);
            
            foreach (var (entity, portal) in _portalled)
            {
                var stillIntersecting = false;
                foreach (var intersecting in EntityManager.GetEntitiesIntersecting(portal))
                {
                    if (intersecting == entity)
                    {
                        stillIntersecting = true;
                        break;
                    }
                }

                if (stillIntersecting)
                    continue;
                
                removals.Add(entity);
            }

            foreach (var entity in removals)
            {
                _portalled.Remove(entity);
            }
        }
    }
}