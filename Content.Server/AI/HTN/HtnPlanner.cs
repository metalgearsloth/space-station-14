using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.WorldState;

namespace Content.Server.AI.HTN
{
    public class HtnPlanner
    {
        // Reading material on how HTN works:
        // http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter12_Exploring_HTN_Planners_through_Example.pdf
        // TODO: Other ones I saw

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
            var finalPlan = new Queue<PrimitiveTask>();
            var depth = -1; // TODO: This in decomposition logger
            var reset = false;

            blackboard.Save(tasksToProcess, finalPlan, 0, null);

            if (rootTask.GetType().IsSubclassOf(typeof(CompoundTask)))
            {
                var compoundRoot = (CompoundTask) rootTask;
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
                    currentTask = blackboard.DecompositionHistory.Peek().OwningCompoundTask;
                    methodTraversalRecord.Dequeue();
                    reset = false;
                }
                else
                {
                    currentTask = tasksToProcess.Pop();
                    methodIndex = 0;
                }

                switch (currentTask)
                {
                    case CompoundTask compound:

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

                        if (foundMethod.GetType().IsSubclassOf(typeof(CompoundTask)))
                        {
                            var foundCompound = (CompoundTask) foundMethod;
                            foundCompound.SetupMethods(blackboard.RunningState);
                        }

                        // TODO: Preconditions are being double-checked (once in method and another again)
                        // TODO: Don't even need the primitive case do we?
                        // Need to save: Current world state, that's it I think?
                        foreach (var subTask in foundMethod.Methods)
                        {
                            tasksToProcess.Push(subTask);
                        }

                        methodTraversalRecord.Enqueue(methodIndex);
                        blackboard.Save(tasksToProcess, finalPlan, methodIndex, compound);
                        break;
                    case PrimitiveTask primitive:
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
        public Queue<PrimitiveTask> PrimitiveTasks;
        public IEnumerable<int> MTR;
        public double PlanTime;

        public HtnPlan(IAiTask rootTask, Queue<PrimitiveTask> primitiveTasks, IEnumerable<int> mtr, double planTime)
        {
            RootTask = rootTask;
            PrimitiveTasks = primitiveTasks;
            MTR = mtr;
            PlanTime = planTime;
        }
    }
}
