using Content.Server.AI.Goals;
using Robust.Server.AI;

namespace Content.Server.AI.Processors
{
    [AiLogicProcessor("NPC")]
    public class NpcProcessor : AiLogicProcessor
    {
        private GoapAgent _agent = new GoapAgent();

        public override void Setup()
        {
            base.Setup();
            _agent.Setup(SelfEntity);
            _agent.Goals.Add(new SatisfyHungerGoal(), 10);
        }

        public override void Update(float frameTime)
        {
            _agent.Update(frameTime);
        }
    }
}
