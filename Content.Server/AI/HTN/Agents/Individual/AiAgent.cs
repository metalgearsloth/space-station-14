using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Agents.Group;
using Content.Server.AI.HTN.Planner;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Server.Interfaces.Chat;
using Robust.Server.AI;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.Agents.Individual
{
    /// <summary>
    /// This is the generic behavior handler for an AI.
    /// It will use the planner to choose what primitive tasks to do based on what is the current highest priority task.
    /// </summary>
    public class AiAgent : AiLogicProcessor
    {
        public GroupAiManager AiManager { get; set; }

        public AiWorldState State { get; private set; }
        private Queue<PrimitiveTask>? RunPlan { get; set; }
        protected virtual float PlanCooldown => 0.5f;
        private float _planCooldownRemaining;

        protected virtual float InteractionCooldown => 0.4f;
        private float _interactionCooldownRemaining;

        protected virtual float MeleeAttackCooldown => 0.4f;
        private float _meleeAttackCooldownRemaining;

        private readonly List<KeyValuePair<RootTaskPriority, List<IAiTask>>> _rootTasks =
            new List<KeyValuePair<RootTaskPriority, List<IAiTask>>>();

        private bool _isDead = false;

        public event Action<PlanUpdate> PlanStatus;

        private IPlanner _planner;

        public AiAgent()
        {
            foreach (RootTaskPriority entry in Enum.GetValues(typeof(RootTaskPriority)))
            {
                _rootTasks.Add(new KeyValuePair<RootTaskPriority, List<IAiTask>>(entry, new List<IAiTask>()));
            }

            _planner = IoCManager.Resolve<IPlanner>();
        }

        private bool TryActiveTask(out PrimitiveTask activeTask)
        {
            activeTask = null;
            if (RunPlan == null || RunPlan?.Count <= 0) return false;
            activeTask = RunPlan.Peek();
            return true;

        }

        private bool TryActiveRootTask(out IAiTask rootTask)
        {
            rootTask = null;

            foreach (var (_, tasks) in _rootTasks)
            {
                if (tasks.Count == 0) continue;
                rootTask = tasks[0];
                return true;
            }

            return false;

        }

        public void Bark(string message)
        {
            var chatManager = IoCManager.Resolve<IChatManager>();
            chatManager.EntitySay(SelfEntity, message);
        }

        /// <summary>
        ///  Will knock the task down the priority list
        /// </summary>
        /// <param name="rootTask"></param>
        public void DeprioritiseRootTask(Type rootTask)
        {
            IAiTask foundTask = null;

            foreach (var (priority, tasks) in _rootTasks)
            {
                // If we're on the last priority then no point continuing anyway as we can't go lower
                if (GetNextPriority(priority) == null && foundTask == null) return;
                if (foundTask != null)
                {
                    tasks.Add(foundTask);
                    return;
                }

                for (var i = 0; i < tasks.Count; i++)
                {
                    var task = tasks[i];
                    if (task.GetType() != rootTask) continue;
                    foundTask = task;
                    tasks.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Gets a lower priority than the specified one if it exists
        /// </summary>
        /// <param name="priority">null if no lower priority</param>
        /// <returns></returns>
        private RootTaskPriority? GetNextPriority(RootTaskPriority priority)
        {
            var intPriority = (int) priority;
            if (!Enum.IsDefined(typeof(RootTaskPriority), intPriority)) return null;
            return (RootTaskPriority) (intPriority + 1);
        }

        public override void Setup()
        {
            base.Setup();
            State = new AiWorldState(SelfEntity);
            SetupListeners();
        }

        protected virtual void SetupListeners()
        {
            if (SelfEntity.TryGetComponent(out DamageableComponent damageableComponent))
            {
                // TODO: Unsubscribe
                damageableComponent.DamageThresholdPassed += (sender, args) =>
                {
                    if (args.DamageThreshold.ThresholdType == ThresholdType.Death)
                    {
                        _isDead = true;
                    }

                    // TODO: If we get healed - double-check what it should be
                    if (args.DamageThreshold.ThresholdType == ThresholdType.None)
                    {
                        _isDead = false;
                    }
                };
            }
            return;
        }

        /// <summary>
        /// Will add the task at the specified priority if it doesn't exist
        /// If the new priority is higher it will bump it up, otherwise it will leave it
        /// </summary>
        /// <param name="rootTaskPriority"></param>
        /// <param name="rootTask"></param>
        public void AddRootTask(RootTaskPriority rootTaskPriority, IAiTask rootTask)
        {
            foreach (var (priority, tasks) in _rootTasks)
            {
                for (var i = 0; i < tasks.Count; i++)
                {
                    // Can't be duplicated tasks so need to check all tasks to see if it already exists
                    if (tasks[i].GetType() != rootTask.GetType()) continue;
                    if ((int) rootTaskPriority >= (int) priority) return;
                    tasks.RemoveAt(i);
                    break;
                }

                if (priority == rootTaskPriority)
                {
                    tasks.Add(rootTask);
                }
            }
        }

        /// <summary>
        ///  Will try and remove the root task if present; won't throw if not found
        /// </summary>
        /// <param name="rootTask"></param>
        public void RemoveRootTask(Type rootTask)
        {
            foreach (var (_, tasks) in _rootTasks)
            {
                for (var i = 0; i < tasks.Count; i++)
                {
                    if (tasks[i].GetType() != rootTask) continue;
                    tasks.RemoveAt(i);
                    return;
                }
            }
        }

        public override void Update(float frameTime)
        {
            // TODO: Check if we ded via event
            if (_isDead)
            {
                return;
            }

            // Cooldown;
            _planCooldownRemaining -= frameTime;
            _interactionCooldownRemaining -= frameTime;
            _meleeAttackCooldownRemaining -= frameTime;

            // If there's no root task then when we do eventually get one it will immediately plan
            if (TryActiveRootTask(out var activeRootTask) && _planCooldownRemaining <= 0)
            {
                _planCooldownRemaining = PlanCooldown;

                // If the root task hasn't changed and we already have a plan (TODO Change this so it may re-plan if the MTR is better)
                if (RunPlan?.Count > 0) return;

                RunPlan = _planner.GetPlan(State, activeRootTask);
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
                    RunPlan?.Dequeue();
                    if (RunPlan?.Count > 0) return;
                    // Plan success
                    PlanStatus?.Invoke(new PlanUpdate(this, activeRootTask, PlanOutcome.Success));
                    return;
                case Outcome.Continuing:
                    return;
                case Outcome.Failed:
                    RunPlan = null;
                    PlanStatus?.Invoke(new PlanUpdate(this, activeRootTask, PlanOutcome.PlanAborted));
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public struct PlanUpdate
        {
            public AiAgent Agent { get; }
            public IAiTask Task { get; set; }
            public PlanOutcome Outcome { get; }

            public PlanUpdate(AiAgent agent, IAiTask task, PlanOutcome outcome)
            {
                Agent = agent;
                Task = task;
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

        public enum RootTaskPriority
        {
            Force,
            High,
            Normal,
            Low,
            VeryLow,
        }
    }
}
