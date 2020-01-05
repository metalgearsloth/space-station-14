using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Agents.Group;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Concrete.Operators;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.WorldState;
using Robust.Server.AI;

namespace Content.Server.AI.HTN.Agents.Individual
{
    public class AiAgent : AiLogicProcessor
    {
        public GroupAiManager AiManager { get; set; }

        public AiWorldState State { get; private set; }
        private HtnPlan? RunPlan { get; set; }
        private float PlanCooldown { get; } = 0.5f;
        private float _planCooldownRemaining;

        private float InteractionCooldown { get; } = 0.4f;
        private float _interactionCooldownRemaining;

        private float MeleeAttackCooldown { get; } = 0.4f;
        private float _meleeAttackCooldownRemaining;

        protected IReadOnlyCollection<IAiTask> RootTasks => _rootTasks;
        private readonly List<IAiTask> _rootTasks = new List<IAiTask>();

        public event Action<PlanUpdate> PlanStatus;

        private bool TryActiveTask(out ConcreteTask activeTask)
        {
            activeTask = null;
            if (RunPlan == null || RunPlan?.PrimitiveTasks.Count <= 0) return false;
            activeTask = RunPlan.PrimitiveTasks.Peek();
            return true;

        }

        private bool TryActiveRootTask(out IAiTask rootTask)
        {
            rootTask = null;
            foreach (var task in RootTasks)
            {
                rootTask = task;
                return true;
            }
            return false;

        }

        public void Bark(string message)
        {
            
        }

        public void PushRootTaskToBack()
        {
            if (RootTasks.Count <= 1) return;

            IAiTask rootTask = null;

            foreach (var task in RootTasks)
            {
                rootTask = task;
                break;
            }

            _rootTasks.Remove(rootTask);
            AddRootTask(rootTask);
        }

        private IAiTask GetRootTask()
        {
            foreach (var task in RootTasks)
            {
                return task;
            }

            return null;
        }

        public override void Setup()
        {
            base.Setup();
            State = new AiWorldState(SelfEntity);
            SetupListeners();
        }

        protected virtual void SetupListeners()
        {
            return;
        }

        public void AddRootTask(IAiTask rootTask)
        {
            foreach (var task in _rootTasks)
            {
                if (task.GetType() == rootTask.GetType())
                {
                    return;
                }
            }

            _rootTasks.Add(rootTask);
        }

        /// <summary>
        ///  Will try and remove the root task if present; won't throw if not found
        /// </summary>
        /// <param name="rootTask"></param>
        public void RemoveRootTask(Type rootTask)
        {
            IAiTask found = null;
            foreach (var item in RootTasks)
            {
                if (item.GetType() != rootTask) continue;

                found = item;
                break;
            }

            if (found != null)
            {
                _rootTasks.Remove(found);
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
                if (RunPlan?.PrimitiveTasks.Count > 0 && RunPlan.RootTask == activeRootTask) return;

                RunPlan = HtnPlanner.GetPlan(State, activeRootTask);
                if (RunPlan == null)
                {
                    PlanStatus?.Invoke(new PlanUpdate(this, activeRootTask, PlanOutcome.PlanningFailed));
                }

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
                    PlanStatus?.Invoke(new PlanUpdate(this, GetRootTask(), PlanOutcome.Success));
                    return;
                case Outcome.Continuing:
                    return;
                case Outcome.Failed:
                    RunPlan = null;
                    PlanStatus?.Invoke(new PlanUpdate(this, GetRootTask(), PlanOutcome.PlanAborted));
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public struct PlanUpdate
        {
            public AiAgent Agent { get; }
            public IAiTask RootTask { get; }
            public PlanOutcome Outcome { get; }

            public PlanUpdate(AiAgent agent, IAiTask rootTask, PlanOutcome outcome)
            {
                Agent = agent;
                RootTask = rootTask;
                Outcome = outcome;
            }
        }

        public enum PlanOutcome
        {
            PlanningFailed,
            PlanAborted,
            Continuing,
            Success
        }
    }
}
