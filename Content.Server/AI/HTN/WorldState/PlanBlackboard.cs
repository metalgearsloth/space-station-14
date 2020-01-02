using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.Tasks.Primitive;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState
{
    public class PlanBlackboard
    {

        public AiWorldState RunningState { get; set; }

        public Stack<Decomposition> DecompositionHistory => _decompositionHistory;
        private Stack<Decomposition> _decompositionHistory = new Stack<Decomposition>();

        public PlanBlackboard(AiWorldState worldState)
        {
            // TODO: Check this
            RunningState = worldState;
        }

        public void ApplyEffects(ConcreteTask task)
        {
            foreach (var effect in task.ProceduralEffects)
            {
                RunningState.UpdateState(effect);
            }
        }

        public void Save(Stack<IAiTask> tasksToProcess, Queue<ConcreteTask> finalPlan, int method, SelectorTask owningTask)
        {
            _decompositionHistory.Push(new Decomposition(tasksToProcess, finalPlan, method, owningTask, RunningState));
        }

        public void Reset()
        {
            RunningState = _decompositionHistory.Peek().WorldState;
        }

        public struct Decomposition
        {
            public Stack<IAiTask> TasksToProcess { get; }
            public Queue<ConcreteTask> FinalPlan { get; }
            public int ChosenMethodIndex { get; }
            public SelectorTask OwningSelectorTask { get; }
            public AiWorldState WorldState { get; }

            public Decomposition(
                Stack<IAiTask> tasksToProcess,
                Queue<ConcreteTask> finalPlan,
                int chosenMethodIndex,
                SelectorTask owningSelectorTask,
                AiWorldState worldState)
            {
                TasksToProcess = tasksToProcess;
                FinalPlan = finalPlan;
                ChosenMethodIndex = chosenMethodIndex;
                OwningSelectorTask = owningSelectorTask;
                WorldState = worldState;
            }
        }
    }
}
