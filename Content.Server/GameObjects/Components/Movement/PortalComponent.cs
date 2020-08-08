#nullable enable
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.GameObjects.Components.Movement
{
    internal class PortalComponent : Component, ICollideBehavior
    {
        public override string Name => "Portal";

        public IEntity? Destination { get; set; }
        
        /// <summary>
        ///     Need a snap-grid so the portal sits on 1 tile and we can put entities onto adjacent tiles
        /// </summary>
        private SnapGridComponent? _snapGridComponent;

        public override void Initialize()
        {
            base.Initialize();
            _snapGridComponent = Owner.EnsureComponent<SnapGridComponent>();
        }

        public void CollideWith(IEntity collidedWith)
        {
            if (!collidedWith.TryGetComponent(out TeleportableComponent teleportable) || 
                Destination == null || Destination.Deleted)
            {
                return;
            }

            Destination.GetComponent<PortalComponent>().Portal(collidedWith);
        }

        private void Portal(IEntity entity)
        {
            // TODO: Check each adjacent tile
            for (var x = -1; x <= 1; x++)
            {
                for 
                    
            }
        }
    }
}