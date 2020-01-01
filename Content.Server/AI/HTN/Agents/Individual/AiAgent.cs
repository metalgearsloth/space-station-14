using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Compound.Combat;
using Content.Server.AI.HTN.Tasks.Compound.Nutrition;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.WorldState;
using Robust.Server.AI;

namespace Content.Server.AI.HTN.Agents.Individual
{
    [AiLogicProcessor("NPC")]
    public class AiAgent : AiLogicProcessor
    {
        public AiWorldState State { get; private set; }
        private HtnPlan? RunPlan { get; set; }
        private float PlanCooldown { get; } = 0.5f;
        private float _planCooldownRemaining;

        private float InteractionCooldown { get; } = 0.5f;
        private float _interactionCooldownRemaining;

        private float MeleeAttackCooldown { get; } = 0.2f;
        private float _meleeAttackCooldownRemaining;

        private readonly Stack<IAiTask> _rootTasks = new Stack<IAiTask>();

        private bool TryActiveTask(out PrimitiveTask activeTask)
        {
            activeTask = null;
            if (!RunPlan.HasValue || RunPlan.Value.PrimitiveTasks.Count <= 0) return false;
            activeTask = RunPlan.Value.PrimitiveTasks.Peek();
            return true;

        }

        private bool TryActiveRootTask(out IAiTask rootTask)
        {
            rootTask = null;
            if (_rootTasks.Count == 0)
            {
                return false;
            }

            rootTask = _rootTasks.Peek();
            return true;

        }

        public override void Setup()
        {
            base.Setup();
            State = new AiWorldState(SelfEntity);
            SetupListeners();
        }

        protected virtual void SetupListeners()
        {
            _rootTasks.Push(new KillNearestPlayer(SelfEntity));
        }

        /// <summary>
        ///  Default plan handler
        /// </summary>
        /// <param name="rootTask"></param>
        /// <param name="planOutcome"></param>
        protected virtual void HandlePlanOutcome(IAiTask rootTask, PlanOutcome planOutcome)
        {
            switch (rootTask)
            {
                case null:
                    return;
                case EatFood _:
                    return;
                case KillNearestPlayer _:
                    return;
                default:
                    if (planOutcome == PlanOutcome.Success)
                    {
                        _rootTasks.Pop();
                    }

                    return;
            }
        }

        public override void Update(float frameTime)
        {
            // Cooldown;
            _planCooldownRemaining -= frameTime;
            _interactionCooldownRemaining -= frameTime;
            _meleeAttackCooldownRemaining -= frameTime;

            if (_planCooldownRemaining <= 0)
            {
                _planCooldownRemaining = PlanCooldown;

                // If no task to do
                if (!TryActiveRootTask(out var activeRootTask)) return;

                // If the root task hasn't changed and we already have a plan (TODO Change this so it may re-plan if the MTR is better)
                if (RunPlan?.PrimitiveTasks.Count > 0 && RunPlan.Value.RootTask == activeRootTask) return;

                RunPlan = HtnPlanner.GetPlan(State, activeRootTask);

                return;
            }

            if (!TryActiveTask(out var activeTask))
            {
                return;
            }

            // So the AI doesn't have godmode
            switch (activeTask.TaskType)
            {
                case PrimitiveTaskType.Default:
                    break;
                case PrimitiveTaskType.Interaction:
                    if (_interactionCooldownRemaining > 0)
                    {
                        return;
                    }
                    break;
                case PrimitiveTaskType.MeleeAttack:
                    if (_meleeAttackCooldownRemaining > 0)
                    {
                        return;
                    }
                    break;
                case PrimitiveTaskType.RangedAttack:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _interactionCooldownRemaining = InteractionCooldown;
            _meleeAttackCooldownRemaining = MeleeAttackCooldown;

            var outcome = activeTask.Execute(frameTime);

            switch (outcome)
            {
                case Outcome.Success:
                    RunPlan?.PrimitiveTasks.Dequeue();
                    if (RunPlan?.PrimitiveTasks.Count > 0) return;
                    // Plan success
                    HandlePlanOutcome(RunPlan?.RootTask, PlanOutcome.Success);
                    RunPlan = null;
                    return;
                case Outcome.Continuing:
                    return;
                case Outcome.Failed:
                    RunPlan = null;
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected enum PlanOutcome
        {
            PlanningFailed,
            PlanAborted,
            Continuing,
            Success
        }
    }
}
