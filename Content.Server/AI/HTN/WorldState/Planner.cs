using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.Tasks.Primitive;

namespace Content.Server.AI.HTN.WorldState
{
    public class Planner
    {
        /// <summary>
        /// Will try and compe up with a plan to achieve the compound task desired effects
        /// </summary>
        /// <param name="worldState"></param>
        /// <param name="compoundTask">The final outcome we're trying to achieve</param>
        /// <returns></returns>
        public HtnPlan GetPlan(AiWorldState worldState, IAiTask compoundTask)
        {
            // Debugging
            var startTime = DateTime.Now;
            var methodTraversalRecord = new List<int>();

            // Setup
            var blackboard = new WorldState.Blackboard(worldState);

            foreach (var state in worldState.States)
            {
                blackboard.RunningState.UpdateState(state);
            }

            var tasksToProcess = new Stack<IAiTask>();
            tasksToProcess.Push(compoundTask);
            var finalPlan = new Queue<PrimitiveTask>();
            int depth = -1; // TODO: This in decomposition logger
            bool reset = false;

            blackboard.Save(tasksToProcess, finalPlan, 0, null);

            // Decomposition logger

            while (tasksToProcess.Count > 0)
            {
                var currentTask = tasksToProcess.Pop();
                var methodIndex = 0;
                if (reset)
                {
                    blackboard.Reset();
                    tasksToProcess = blackboard.DecompositionHistory.Peek().TasksToProcess;
                    finalPlan = blackboard.DecompositionHistory.Peek().FinalPlan;
                    methodIndex += blackboard.DecompositionHistory.Peek().ChosenMethodIndex;
                    currentTask = blackboard.DecompositionHistory.Peek().OwningCompoundTask;
                }

                switch (currentTask)
                {
                    // TODO: There needs to be some way to track world state while plan running, which means each task can then verify the preconditions are met
                    case CompoundTask compound:
                        depth++;
                        methodTraversalRecord.Add(depth);

                        var methodFound = false;

                        foreach (var method in compound.Methods.GetRange(methodIndex, compound.Methods.Count - methodIndex))
                        {
                            if (!method.PreconditionsMet(blackboard.RunningState))
                            {
                                methodIndex++;
                                continue;
                            }

                            // Need to save: Current world state, that's it I think?
                            foreach (var subTask in method.Methods)
                            {
                                tasksToProcess.Push(subTask);
                            }

                            methodFound = true;
                            methodTraversalRecord.Add(depth + 1);
                            blackboard.Save(tasksToProcess, finalPlan, methodIndex, compound);
                            // Save current tasksToProcess
                            // Save current finalPlan
                            // Save chosen method (as index?)
                            // Save owning compound task -> I would push it to the top of the stack and continue on when restored
                            // Rollback MTR
                            // TODO: Also add TasksToProcess to blackboard
                            break;
                        }

                        if (!methodFound)
                        {
                            reset = true;
                        }

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

            foreach (var task in finalPlan)
            {
                task.SetupOperator();
            }

            var runTime = (DateTime.Now - startTime).TotalSeconds;
            return new HtnPlan(finalPlan, methodTraversalRecord, runTime);
        }
    }

    public struct HtnPlan
    {
        public Queue<PrimitiveTask> PrimitiveTasks;
        public IEnumerable<int> MTR;
        public double PlanTime;

        public HtnPlan(Queue<PrimitiveTask> primitiveTasks, IEnumerable<int> mtr, double planTime)
        {
            PrimitiveTasks = primitiveTasks;
            MTR = mtr;
            PlanTime = planTime;
        }
    }
}
