using Robust.Server.AI;

namespace Content.Server.AI.HTN.Agents.Individual
{
    [AiLogicProcessor("Civilian")]
    public class CivilianAi : AiAgent
    {
        protected override float PlanCooldown => 1.0f;
    }
}
