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
        private float PlanCooldown { get; } = 0.2f;
        private float _planCooldownRemaining;
        private Stack<IAiTask> _rootTasks = new Stack<IAiTask>();

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

            if (!TryActiveTask(out var activeTask) && RunPlan.HasValue) return;

            if (_planCooldownRemaining <= 0)
            {
                _planCooldownRemaining = PlanCooldown;
                RunPlan = _planner.GetPlan(State, ActiveRootTask());
                return;
            }

            // Oh noes
            if (activeTask.Execute(frameTime) == Outcome.Failed)
            {
                _planCooldownRemaining = 0;
                return;
            }

            if (_planCooldownRemaining <= 0)
            {
                _planCooldownRemaining = PlanCooldown;
                // TODO: Plan
            }
        }
    }
}
