using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Sequence;
using Content.Server.AI.HTN.WorldState;

namespace Content.Server.AI.HTN
{
    public class HtnPlanner
    {
        // Reading material on how HTN works:
        // http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter12_Exploring_HTN_Planners_through_Example.pdf
        // http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter29_Hierarchical_AI_for_Multiplayer_Bots_in_Killzone_3.pdf
        // SHOP2 (this is what Guerilla Games use for their stuff)

        // TODO: Other ones I saw

        // Primitive Tasks -> ConcreteTask
        // Compound Tasks -> Split into SelectorTask (chooses a Branch / Method to use) and SequenceTask (Subtasks).
        // This was to avoid diluting methods and subtasks together for compound tasks into spaghetti.
        // FluidHTN also does it this way which seemed like the sanest choice (though we have nothing else in common with FluidHTN).

        // Games that use HTN Planners:
        // Transformers Fall of Cybertron (Previously they used GOAP in War for Cybertron)
        // Guerilla Games (Horizon: Zero Dawn, Killzone, etc.)

        // Depending where you read there will be slight implementation differences,
        // i.e. are operators and primitive tasks separate things.
        // This implementation is very loosely based on FluidHTN's interpretation of the GameAIPro article

        /// <summary>
        /// Tries to decompose the root task into a series of primitive tasks to do.
        /// </summary>
        /// <param name="worldState"></param>
        /// <param name="rootTask">The final outcome we're trying to achieve</param>
        /// <returns></returns>
        public static HtnPlan GetPlan(AiWorldState worldState, IAiTask rootTask)
        {
            // Debugging
            var startTime = DateTime.Now;
            var methodTraversalRecord = new Queue<int>();

            // Setup
            var blackboard = new PlanBlackboard(worldState);

            foreach (var state in worldState.States)
            {
                blackboard.RunningState.UpdateState(state);
            }

            var tasksToProcess = new Stack<IAiTask>();
            tasksToProcess.Push(rootTask);
            var finalPlan = new Queue<ConcreteTask>();
            var depth = -1; // TODO: This in decomposition logger
            var reset = false;

            blackboard.Save(tasksToProcess, finalPlan, 0, null);

            if (rootTask.GetType().IsSubclassOf(typeof(SelectorTask)))
            {
                var compoundRoot = (SelectorTask) rootTask;
                compoundRoot.SetupMethods(blackboard.RunningState);
            }

            // Decomposition logger
            // TODO: This shit still needs tweaking

            while (tasksToProcess.Count > 0 || reset)
            {
                IAiTask currentTask;
                int methodIndex;
                if (reset)
                {
                    blackboard.Reset();
                    tasksToProcess = blackboard.DecompositionHistory.Peek().TasksToProcess;
                    finalPlan = blackboard.DecompositionHistory.Peek().FinalPlan;
                    methodIndex = blackboard.DecompositionHistory.Peek().ChosenMethodIndex + 1;
                    currentTask = blackboard.DecompositionHistory.Peek().OwningSelectorTask;
                    if (methodTraversalRecord.Count > 0)
                    {
                        methodTraversalRecord.Dequeue();
                    }
                    reset = false;
                }
                else
                {
                    currentTask = tasksToProcess.Pop();
                    methodIndex = 0;
                }

                switch (currentTask)
                {
                    case SelectorTask compound:

                        if (!compound.PreconditionsMet(blackboard.RunningState))
                        {
                            continue;
                        }
                        // TODO: Need to reset and force this???
                        IAiTask foundMethod = null;

                        foreach (var method in compound.Methods.GetRange(methodIndex, compound.Methods.Count - methodIndex))
                        {
                            if (!method.PreconditionsMet(blackboard.RunningState)) continue;
                            foundMethod = method;
                            break;
                        }

                        if (foundMethod == null)
                        {
                            reset = true;
                            break;
                        }

                        tasksToProcess.Push(foundMethod);

                        methodTraversalRecord.Enqueue(methodIndex);
                        blackboard.Save(tasksToProcess, finalPlan, methodIndex, compound);
                        break;
                    case SequenceTask sequence:
                        if (sequence.PreconditionsMet(blackboard.RunningState))
                        {
                            foreach (var task in sequence.SubTasks)
                            {
                                tasksToProcess.Push(task);
                            }
                        }

                        break;
                    case ConcreteTask primitive:
                        // If conditions met
                        if (primitive.PreconditionsMet(blackboard.RunningState))
                        {
                            blackboard.ApplyEffects(primitive);
                            finalPlan.Enqueue(primitive);
                        }
                        else
                        {
                            // Restore to last decomposed
                            reset = true;
                        }

                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            // TODO: Probs don't want to do this
            if (finalPlan.Count == 0)
            {
                return null;
            }

            foreach (var task in finalPlan)
            {
                task.SetupOperator();
            }

            var runTime = (DateTime.Now - startTime).TotalSeconds;
            return new HtnPlan(rootTask, finalPlan, methodTraversalRecord, runTime);
        }
    }

    public class HtnPlan
    {
        public IAiTask RootTask;
        public Queue<ConcreteTask> PrimitiveTasks;
        public IEnumerable<int> MTR;
        public double PlanTime;

        public HtnPlan(IAiTask rootTask, Queue<ConcreteTask> primitiveTasks, IEnumerable<int> mtr, double planTime)
        {
            RootTask = rootTask;
            PrimitiveTasks = primitiveTasks;
            MTR = mtr;
            PlanTime = planTime;
        }
    }
}
