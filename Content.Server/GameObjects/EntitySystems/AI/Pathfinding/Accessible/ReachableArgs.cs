using System.Collections.Generic;
using Content.Server.GameObjects.Components.Access;
using Content.Server.GameObjects.Components.Movement;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.GameObjects.EntitySystems.AI.Pathfinding.Accessible
{
    public sealed class ReachableArgs
    {
        public float VisionRadius { get; }
        public ICollection<string> Access { get; }
        public int CollisionMask { get; }

        public ReachableArgs(float visionRadius, ICollection<string> access, int collisionMask)
        {
            VisionRadius = visionRadius;
            Access = access;
            CollisionMask = collisionMask;
        }

        public static ReachableArgs GetArgs(IEntity entity)
        {
            var collisionMask = 0;
            if (entity.TryGetComponent(out CollidableComponent collidableComponent))
            {
                collisionMask = collidableComponent.CollisionMask;
            }

            var access = AccessReader.FindAccessTags(entity);
            var visionRadius = entity.GetComponent<AiControllerComponent>().VisionRadius;
            
            return new ReachableArgs(visionRadius, access, collisionMask);
        }
    }
}