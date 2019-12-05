using Content.Server.AI.Goals;
using Content.Server.AI.Preconditions;
using Robust.Server.AI;
using Robust.Shared.Utility;
using Logger = Robust.Shared.Log.Logger;

namespace Content.Server.AI.Processors
{
    [AiLogicProcessor("NPC")]
    public class GoapProcessor : AiLogicProcessor
    {
        private readonly GoapAgent _agent = new GoapAgent();
        public IWorldState WorldState => _agent.WorldState;

        public override void Setup()
        {
            base.Setup();
            _agent.Setup(SelfEntity);
            _agent.WorldState.StateUpdate += UpdateGoals;
        }

        public override void Update(float frameTime)
        {
            _agent.Update(frameTime);
            if (_agent.Goals.Count == 0)
            {
                // Yah goals done!
                return;
            }
        }

        bool HasGoal(string goalName)
        {
            foreach (var (goal, _) in _agent.Goals)
            {
                if (goalName == goal.Name)
                {
                    return true;
                }
            }

            return false;
        }

        protected virtual void UpdateGoals(string state, bool result)
        {
            if (HasGoal(state))
            {
                return;
            }

            string goalAdded = null;
            int goalPriority = 0;
            // Anything that requires positive state
            if (result)
            {
                switch (state)
                {
                    case "Thirsty":
                        goalAdded = "Thirsty";
                        goalPriority = 30;

                        _agent.Goals.Add(new SatisfyThirstGoal(), goalPriority);
                        break;
                    case "Hungry":
                        goalAdded = "Hungry";
                        goalPriority = 20;

                        _agent.Goals.Add(new SatisfyHungerGoal(), goalPriority);
                        break;
                    default:
                        break;
                }
            }
            else
            {

            }

            if (goalAdded == null)
            {
                return;
            }

            Logger.InfoS("ai", $"Added goal {goalAdded} at priority {goalPriority} to {SelfEntity}");
        }
    }
}
