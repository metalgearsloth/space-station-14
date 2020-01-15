using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Planner;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Sequence;
using Content.Server.AI.HTN.WorldState;
using Content.Shared.GameObjects.Components.AI;
using Logger = Robust.Shared.Log.Logger;

namespace Content.Server.AI.HTN
{
    public class HtnPlanner : IPlanner
    {
        // TODO: Look at using individual tasks to generate plans for each agent given they don't affect state.

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

        private const double PlanTimeout = 0.16;
        public event Action<AiPlanMessage> FoundPlan;

        /// <summary>
        /// Tries to decompose the root task into a series of primitive tasks to do.
        /// </summary>
        /// <param name="worldState"></param>
        /// <param name="rootTask">The final outcome we're trying to achieve</param>
        /// <returns></returns>
        public Queue<PrimitiveTask> GetPlan(AiWorldState worldState, IAiTask rootTask)
        {
            // Debugging
            var startTime = DateTime.Now;
            var planningTime = 0.0;
            var methodTraversalRecord = new Queue<int>();

            // Setup
            var blackboard = new PlanBlackboard(worldState);

            // TODO: For states use Value and GetValue; Value is the transient Value which can be reset and GetValue is the true value

            var tasksToProcess = new Stack<IAiTask>();
            tasksToProcess.Push(rootTask);
            var finalPlan = new Queue<PrimitiveTask>();
            var reset = false;
            var methodIndex = 0;

            // Decomposition logger
            // TODO: This still needs tweaking

            while (tasksToProcess.Count > 0 || reset)
            {
                if (planningTime >= PlanTimeout)
                {
                    Logger.WarningS("ai", $"Planning timed out for {rootTask}");
                    break;
                }

                planningTime = (DateTime.Now - startTime).TotalSeconds;

                IAiTask currentTask;
                if (reset)
                {
                    if (blackboard.DecompositionHistory.Count == 0) break;
                    var decomp = blackboard.DecompositionHistory.Pop();
                    methodIndex = decomp.ChosenMethodIndex + 1;
                    tasksToProcess = decomp.TasksToProcess;
                    finalPlan = decomp.FinalPlan;
                    currentTask = decomp.OwningSelectorTask;
                    if (methodTraversalRecord.Count > 0)
                    {
                        methodTraversalRecord.Dequeue();
                    }
                    reset = false;
                }
                else
                {
                    currentTask = tasksToProcess.Pop();
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

                        compound.SetupMethods(blackboard.RunningState);
                        var indexOffset = -1;

                        foreach (var method in compound.Methods.GetRange(methodIndex, compound.Methods.Count - methodIndex))
                        {
                            indexOffset += 1;
                            if (!method.PreconditionsMet(blackboard.RunningState)) continue;
                            foundMethod = method;
                            break;
                        }

                        if (foundMethod == null)
                        {
                            reset = true;
                            break;
                        }

                        // Save where we got up to so if this method doesn't pan out we can try the next one.
                        // TODO: Copy world state
                        blackboard.Save(
                            new Stack<IAiTask>(finalPlan),
                            new Queue<PrimitiveTask>(finalPlan),
                            methodIndex + indexOffset,
                            compound);
                        tasksToProcess.Push(foundMethod);
                        methodTraversalRecord.Enqueue(methodIndex);
                        break;
                    case SequenceTask sequence:
                        if (sequence.PreconditionsMet(blackboard.RunningState))
                        {
                            sequence.SetupSubTasks(blackboard.RunningState);
                            foreach (var task in sequence.SubTasks)
                            {
                                tasksToProcess.Push(task);
                            }
                        }
                        else
                        {
                            reset = true;
                        }

                        break;
                    case PrimitiveTask primitive:
                        if (primitive.PreconditionsMet(blackboard.RunningState))
                        {
                            primitive.ProceduralEffects(blackboard.RunningState);
                            finalPlan.Enqueue(primitive);
                        }
                        else
                        {
                            // Restore to last decomposed
                            reset = true;
                        }

                        break;
                    case null: // This should only happen if the root task couldn't select anything
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            if (finalPlan.Count == 0)
            {
                return null;
            }

            foreach (var task in finalPlan)
            {
                task.SetupOperator();
            }

            planningTime = (DateTime.Now - startTime).TotalSeconds;
            var plan = new AiPlanMessage(rootTask.ToString()); //, finalPlan, methodTraversalRecord, planningTime); TODO
            FoundPlan?.Invoke(plan);

            return finalPlan;
        }
    }
}
