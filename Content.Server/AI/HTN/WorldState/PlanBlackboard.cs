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

        public void ApplyEffects(PrimitiveTask task)
        {
            foreach (var effect in task.ProceduralEffects)
            {
                RunningState.UpdateState(effect);
            }
        }

        public void Save(Stack<IAiTask> tasksToProcess, Queue<PrimitiveTask> finalPlan, int method, CompoundTask owningTask)
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
            public Queue<PrimitiveTask> FinalPlan { get; }
            public int ChosenMethodIndex { get; }
            public CompoundTask OwningCompoundTask { get; }
            public AiWorldState WorldState { get; }

            public Decomposition(
                Stack<IAiTask> tasksToProcess,
                Queue<PrimitiveTask> finalPlan,
                int chosenMethodIndex,
                CompoundTask owningCompoundTask,
                AiWorldState worldState)
            {
                TasksToProcess = tasksToProcess;
                FinalPlan = finalPlan;
                ChosenMethodIndex = chosenMethodIndex;
                OwningCompoundTask = owningCompoundTask;
                WorldState = worldState;
            }
        }
    }
}
