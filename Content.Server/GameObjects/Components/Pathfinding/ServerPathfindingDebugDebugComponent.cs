using Content.Shared.GameObjects.Components.Pathfinding;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    [RegisterComponent]
    public sealed class ServerPathfindingDebugDebugComponent : SharedPathfindingDebugComponent
    {
        public override void Initialize()
        {
            base.Initialize();
            var pathfinder = IoCManager.Resolve<IPathfinder>();
            pathfinder.DebugRoute += route =>
            {
                SendNetworkMessage(route);
            };
        }
    }
}
