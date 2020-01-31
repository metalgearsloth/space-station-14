using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.WorldState;
using Content.Shared.GameObjects.Components.AI;
using Robust.Shared.GameObjects;

namespace Content.Server.AI.HTN.Planner
{
    public interface IPlanner
    {
        event Action<AiPlanMessage> FoundPlan;
        Queue<PrimitiveTask> GetPlan(EntityUid entityUid, AiWorldState worldState, IAiTask rootTask);
    }
}
