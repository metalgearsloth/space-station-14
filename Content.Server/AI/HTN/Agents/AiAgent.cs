using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.WorldState;
using Robust.Server.AI;

namespace Content.Server.AI.HTN.Agents
{
    [AiLogicProcessor("NPC")]
    public class AiAgent : AiLogicProcessor
    {
        public AiWorldState State { get; private set; }
        private HtnPlan? RunPlan { get; set; }
        private readonly Planner _planner = new Planner();
        private float PlanCooldown { get; } = 0.5f;
        private float _planCooldownRemaining;
        private readonly Stack<IAiTask> _rootTasks = new Stack<IAiTask>();

        private bool TryActiveTask(out PrimitiveTask activeTask)
        {
            activeTask = null;
            if (!RunPlan.HasValue || RunPlan.Value.PrimitiveTasks.Count <= 0) return false;
            activeTask = RunPlan.Value.PrimitiveTasks.Peek();
            return true;

        }

        private IAiTask ActiveRootTask()
        {
            return _rootTasks.Count == 0 ? null : _rootTasks.Peek();
        }

        public override void Setup()
        {
            base.Setup();
            State = new AiWorldState(SelfEntity);
            SetupListeners();
        }

        protected virtual void SetupListeners()
        {
            _rootTasks.Push(new MoveToNearestPlayer(SelfEntity));
        }

        public override void Update(float frameTime)
        {
            _planCooldownRemaining -= frameTime;

            // TODO Refine this e.g. if we can get a better plan. Check the MTR for better plan

            if (_planCooldownRemaining <= 0)
            {
                _planCooldownRemaining = PlanCooldown;
                var activeRootTask = ActiveRootTask();
                if (RunPlan.HasValue && RunPlan.Value.RootTask == activeRootTask) return;

                RunPlan = _planner.GetPlan(State, activeRootTask);

                return;
            }

            if (!TryActiveTask(out var activeTask))
            {
                return;
            }

            var outcome = activeTask.Execute(frameTime);

            switch (outcome)
            {
                case Outcome.Success:
                    RunPlan?.PrimitiveTasks.Dequeue();
                    if (RunPlan?.PrimitiveTasks.Count > 0) return;
                    // Plan success
                    _rootTasks.Pop();
                    RunPlan = null;
                    return;

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
    }
}
