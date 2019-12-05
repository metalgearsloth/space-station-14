using System;
using System.Collections.Generic;
using Content.Server.AI.Actions;
using Content.Server.AI.Goals;
using Content.Server.AI.Preconditions;
using Content.Server.AI.Routines.Movers;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Log;

namespace Content.Server.AI
{
    public class GoapAgent
    {
        /// <summary>
        /// Goal state and priority for it
        /// </summary>
        public Dictionary<IGoapGoal, int> Goals { get; set; } = new Dictionary<IGoapGoal, int>();

        public IGoapGoal CurrentGoal => _currentGoal;
        private IGoapGoal _currentGoal;

        public event Action<PlanOutcome> PlanChange;

        /// <summary>
        /// The actions the current goal lets us use
        /// </summary>
        public HashSet<GoapAction> AvailableActions => _availableActions;
        private HashSet<GoapAction> _availableActions = new HashSet<GoapAction>();

        /// <summary>
        /// The current plan
        /// </summary>
        public Queue<GoapAction> CurrentActions => _currentActions;
        private Queue<GoapAction> _currentActions = new Queue<GoapAction>();

        public GoapState State => _state;
        private GoapState _state = GoapState.Idle;
        private GoalPlanner _planner = new GoalPlanner();

        private float _planCooldown = 0.0f;

        // Robust specific
        private AiMover _mover;

        public IEntity Owner;
        private AiWorldState _worldState;

        /// <summary>
        /// Gets the next action in the plan if we have one
        /// </summary>
        /// <param name="nextAction"></param>
        /// <returns></returns>
        private bool HasNextAction(out GoapAction nextAction)
        {
            nextAction = CurrentActions.Peek();
            return CurrentActions.Count > 0;
        }

        public void Setup(IEntity owner)
        {
            Owner = owner;
            _mover = new AiMover(Owner);
            _worldState = new AiWorldState(Owner);
            PlanChange += outcomes => { Logger.DebugS("ai", $"{outcomes}"); };
        }

        /// <summary>
        /// Will try and come up with a plan for its current goal
        /// </summary>
        /// <param name="frameTime"></param>
        /// <returns></returns>
        private void Idle(float frameTime)
        {
            // TODO: Use while loop on goals looking for which ones are possible
            // Get highest priority goal
            IGoapGoal highestGoal = null;
            int highestPriority = 0;
            foreach (var goal in Goals)
            {
                if (goal.Value > highestPriority)
                {
                    highestPriority = goal.Value;
                    highestGoal = goal.Key;
                }
            }

            // No goals in life ;-;
            if (highestGoal == null)
            {
                return;
            }

            // Why change course?
            if (_currentGoal == highestGoal && _currentActions.Count > 0)
            {
                if (State == GoapState.Idle)
                {
                    _state = GoapState.PerformAction;
                }
                return;
            }

            _currentGoal = highestGoal;
            _availableActions = highestGoal.Actions;

            // If we've run out of stuff to do in our last plan. TODO: reassess this at some frequency
            // TODO: Should this if be here?
            if (CurrentActions.Count == 0)
            {
                var actions = _planner.Plan(Owner, AvailableActions, _worldState.GetState(), highestGoal.GoalState);
                if (actions == null)
                {
                    _state = GoapState.Idle;
                    return;
                }

                _currentActions = actions;
                _state = GoapState.PerformAction;
                PlanChange?.Invoke(PlanOutcome.PlanFound);
            }

            // Plan failed
            if (CurrentActions == null)
            {
                _state = GoapState.Idle;
                PlanChange?.Invoke(PlanOutcome.PlanFailed);
                // TODO: Have cooldown on failed plans
                return;
            }

        }

        /// <summary>
        /// Moves the agent towards next action's position
        /// </summary>
        /// <param name="action"></param>
        /// <param name="frameTime"></param>
        /// <returns>true if we're not in range</returns>
        private void MoveTo(float frameTime)
        {
            if (!HasNextAction(out var action))
            {
                _state = GoapState.Idle;
                _mover.HaveArrived();
                return;
            }

            if (!action.RequiresInRange || action.InRange(Owner))
            {
                _state = GoapState.PerformAction;
                _mover.HaveArrived();
                return;
            }

            if (action.TargetEntity != null)
            {
                _mover.MoveToEntity(action.TargetEntity, frameTime);
                return;
            }

            _mover.MoveToGrid(action.TargetGrid, frameTime);

            return;
        }

        /// <summary>
        /// Will try and perform the specified action (duh)
        /// </summary>
        /// <param name="frameTime"></param>
        private void PerformAction(float frameTime)
        {
            if (!HasNextAction(out var action))
            {
                // No actions left to do
                _state = GoapState.Idle;
                PlanChange?.Invoke(PlanOutcome.ActionsFinished);
                return;
            }

            // Noice, we did that
            if (action.IsDone)
            {
                _currentActions.Dequeue();
            }

            if (CurrentActions.Count > 0)
            {
                action = CurrentActions.Peek();

                if (action.RequiresInRange && !action.InRange(Owner))
                {
                    _state = GoapState.MoveTo;
                    return;
                }

                if (action.TryPerformAction(Owner))
                {
                    action.IsDone = true;
                    return;
                }

                PlanChange?.Invoke(PlanOutcome.PlanAborted);
                _state = GoapState.Idle;
                _currentActions.Clear();
                return;
            }

            _currentActions.Clear(); // Shouldn't need this clear...
            _state = GoapState.Idle;
            PlanChange?.Invoke(PlanOutcome.ActionsFinished);
        }

        public virtual void Update(float frameTime)
        {
            // BIG TODO: THROTTLE PLANNING
            _planCooldown -= frameTime;

            if (Goals.Count == 0 || (State == GoapState.Idle && _planCooldown > 0))
            {
                return;
            }

            // 1-tick delay between plan and move / action
            switch (State)
            {
                case GoapState.Idle:
                    _planCooldown = 1.0f;
                    Idle(frameTime);
                    return;
                case GoapState.MoveTo:
                    MoveTo(frameTime);
                    return;
                case GoapState.PerformAction:
                    PerformAction(frameTime);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public enum PlanOutcome
    {
        PlanAborted,
        PlanFound,
        PlanFailed,
        ActionsFinished,
    }

    public enum GoapState
    {
        Idle,
        MoveTo,
        PerformAction,
    }
}
