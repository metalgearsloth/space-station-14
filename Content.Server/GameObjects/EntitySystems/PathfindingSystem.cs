using Content.Server.GameObjects.Components.Interactable.Tools;
using Content.Server.Pathfinding;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;

namespace Content.Server.GameObjects.EntitySystems
{
    public class PathfindingSystem : EntitySystem
    {
        public override void Initialize()
        {
            EntityQuery = new TypeEntityQuery(typeof(AStarPathfinder));
        }

        public override void Update(float frameTime)
        {
            foreach (var entity in RelevantEntities)
            {
                var comp = entity.GetComponent<AStarPathfinder>();
                comp.OnUpdate(frameTime);
            }
        }
    }
}
