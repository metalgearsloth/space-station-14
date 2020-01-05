using Content.Server.AI.HTN.Planner;
using Content.Shared.GameObjects.Components.AI;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.GameObjects.Components.AI
{
    [RegisterComponent]
    public class ServerAiDebugComponent : SharedAiDebugComponent
    {
        public override void Initialize()
        {
            base.Initialize();
            var aiPlanner = IoCManager.Resolve<IPlanner>();
            aiPlanner.FoundPlan += plan =>
            {
                SendNetworkMessage(plan);
            };
        }
    }
}
