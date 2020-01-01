using Content.Server.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.Components.Pathfinding;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.IoC;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    [RegisterComponent]
    public class ServerPathfindingDebugComponent : SharedPathfindingComponent
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
