using System.Collections.Generic;
using Content.Server.AI.HTN.Agents.Individual;
using Robust.Shared.Log;

namespace Content.Server.AI.HTN.Agents.Group
    {/// <summary>
     /// This is a controller for multiple agents and will co-ordinate the group
     /// </summary>
    public abstract class GroupAiManager
     {
         /// <summary>
         /// The Ai processors currently being managed by this manager
         /// </summary>
         public IReadOnlyCollection<AiAgent> Agents => _agents;

         protected readonly List<AiAgent> _agents = new List<AiAgent>();
         public abstract void Setup();

         /// <summary>
         /// Ai manager no longer controls the agent
         /// </summary>
         /// <param name="agent"></param>
         public void ReleaseAgent(AiAgent agent)
         {
             // TODO: Look at setting a cooldown on this so it doesn't immediately grab it back
             if (!_agents.Contains(agent))
             {
                 Logger.WarningS("ai", $"Agent {agent} not managed by this manager");
                 return;
             }
             _agents.Remove(agent);
             agent.AiManager = null;
             agent.PlanStatus -= HandlePlanOutcome;
         }

         protected virtual void SetupAgent(AiAgent agent)
         {
             agent.AiManager = this;
             agent.PlanStatus += HandlePlanOutcome;
         }

         /// <summary>
         /// Generally used to handle where we're unable to plan or need to abort a running plan
         /// </summary>
         protected abstract void HandlePlanOutcome(PlanUpdate update);

         /// <summary>
         /// When the Ai System announces an AI with no manager this will try and grab it for use.
         /// </summary>
         /// <param name="agent"></param>
         /// <returns></returns>
         protected abstract bool TryTakeAgent(AiAgent agent);
     }
}
