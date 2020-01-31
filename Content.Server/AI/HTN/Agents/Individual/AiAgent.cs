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
        private float _planCooldownRemaining = 0.5f;

        protected virtual float InteractionCooldown => 0.4f;
        private float _interactionCooldownRemaining;

        protected virtual float MeleeAttackCooldown => 0.4f;
        private float _meleeAttackCooldownRemaining;

        protected virtual float BarkCooldown => 2.0f;
        private DateTime _lastBark = DateTime.Now;

        public IAiTask RootTask { get; private set; }
        private readonly List<IAiTask> _rootTasks =
            new List<IAiTask>();

        private bool _isDead = false;

        public event Action<PlanUpdate> PlanStatus;

        private IPlanner _planner;

        public AiAgent()
        {
            _planner = IoCManager.Resolve<IPlanner>();
        }

        public virtual void HandleTaskOutcome(PrimitiveTask task, Outcome outcome) {}

        private bool TryActiveTask(out PrimitiveTask activeTask)
        {
            activeTask = null;
            if (RunPlan == null || RunPlan?.Count <= 0) return false;
            activeTask = RunPlan.Peek();
            return true;

        }

        public void Bark(string message, bool force = false)
        {
            if (!force && (DateTime.Now - _lastBark).TotalSeconds < BarkCooldown) return;
            var chatManager = IoCManager.Resolve<IChatManager>();
            chatManager.EntitySay(SelfEntity, message);
        }

        /// <summary>
        ///  Will knock the task down the priority list
        /// </summary>
        /// <param name="rootTask"></param>
        public void DeprioritiseRootTask(Type rootTask)
        {
            for (var i = 0; i < _rootTasks.Count; i++)
            {
                // no more
                if (i + 1 == _rootTasks.Count) break;
                var task = _rootTasks[i];
                if (task.GetType() != rootTask) continue;
                var nextTask = _rootTasks[i + 1];
                _rootTasks[i] = nextTask;
                _rootTasks[i + 1] = task;
                break;
            }
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
        public void AddRootTask(IAiTask rootTask)
        {
            _rootTasks.Add(rootTask);
        }

        /// <summary>
        ///  Will try and remove the root task if present; won't throw if not found
        /// </summary>
        /// <param name="rootTask"></param>
        public void RemoveRootTask(Type rootTask)
        {
            for (var i = 0; i < _rootTasks.Count; i++)
            {
                var task = _rootTasks[i];
                if (task.GetType() != rootTask) continue;
                _rootTasks.RemoveAt(i);
                break;
            }
        }

        public override void Update(float frameTime)
        {
            if (_isDead)
            {
                return;
            }

            // Cooldown;
            _planCooldownRemaining -= frameTime;
            _interactionCooldownRemaining -= frameTime;
            _meleeAttackCooldownRemaining -= frameTime;

            // If there's no root task then when we do eventually get one it will immediately plan
            if (_rootTasks.Count > 0 && _planCooldownRemaining <= 0)
            {
                _planCooldownRemaining = PlanCooldown;
                // If the root task hasn't changed and we already have a plan (TODO Change this so it may re-plan if the MTR is better)
                if (RunPlan?.Count > 0) return;

                // Keep going until we have something to do - Should we also de-prioritise?
                foreach (var root in _rootTasks)
                {
                    RunPlan = _planner.GetPlan(SelfEntity.Uid, State, root);
                    if (RunPlan == null || RunPlan.Count == 0) continue;
                    RootTask = RunPlan.Peek();
                    return;
                }

                RootTask = null;
                PlanStatus?.Invoke(new PlanUpdate(this, RootTask, PlanOutcome.PlanningFailed));
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
                    HandleTaskOutcome(activeTask, Outcome.Success);
                    RunPlan?.Dequeue();
                    if (RunPlan?.Count > 0) return;
                    // Plan success
                    PlanStatus?.Invoke(new PlanUpdate(this, RootTask, PlanOutcome.Success));
                    return;
                case Outcome.Continuing:
                    // Don't see much point invoking continuing event?
                    return;
                case Outcome.Failed:
                    HandleTaskOutcome(activeTask, Outcome.Failed);
                    RunPlan = null;
                    PlanStatus?.Invoke(new PlanUpdate(this, RootTask, PlanOutcome.PlanAborted));
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
