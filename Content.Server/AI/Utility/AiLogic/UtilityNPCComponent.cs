#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Content.Server.AI.Operators;
using Content.Server.AI.Utility.Actions;
using Content.Server.AI.WorldState;
using Content.Server.AI.WorldState.States.Utility;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.EntitySystems.AI;
using Content.Server.GameObjects.EntitySystems.AI.LoadBalancer;
using Content.Server.GameObjects.EntitySystems.JobQueues;
using Content.Shared.GameObjects.Components.Damage;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Server.AI.Utility.AiLogic
{
    public class UtilityNPCComponent : NPCComponent
    {
        // TODO: Look at having ParallelOperators (probably no more than that as then you'd have a full-blown BT)
        // Also RepeatOperators (e.g. if we're following an entity keep repeating MoveToEntity)
        private AiActionSystem _planner = default!;
        public Blackboard Blackboard => _blackboard;
        private Blackboard _blackboard = default!;

        /// <summary>
        /// The sum of all BehaviorSets gives us what actions the AI can take
        /// </summary>
        public Dictionary<string, List<IAiUtility>> BehaviorSets { get; } = new Dictionary<string, List<IAiUtility>>();
        private readonly List<IAiUtility> _availableActions = new List<IAiUtility>();

        /// <summary>
        /// The currently running action; most importantly are the operators.
        /// </summary>
        public UtilityAction? CurrentAction { get; private set; }

        public (Type Type, Queue<AiOperator> ActionOperators)? ActualCurrentAction { get; private set; }

        /// <summary>
        /// How frequently we can re-plan. If an AI's in combat you could decrease the cooldown,
        /// or if there's no players nearby increase it.
        /// </summary>
        public float PlanCooldown { get; } = 0.5f;
        private float _planCooldownRemaining;

        /// <summary>
        /// If we've requested a plan then wait patiently for the action
        /// </summary>
        private AiActionRequestJob? _actionRequest;

        private CancellationTokenSource? _actionCancellation;

        /// <summary>
        /// If we can't do anything then stop thinking; should probably use ActionBlocker instead
        /// </summary>
        private bool _isDead = false;

        /// <summary>
        ///     Adds a behavior set to this entity.
        /// </summary>
        /// <param name="behaviorSet"></param>
        /// <param name="sort"></param>
        public void AddBehaviorSet(string behaviorSet, bool sort = true)
        {
            var aiSystem = EntitySystem.Get<NPCSystem>();

            if (BehaviorSets.TryAdd(behaviorSet, aiSystem.GetBehaviorActions(behaviorSet)) && sort)
            {
                SortActions();
            }

            if (BehaviorSets.Count == 1 && !aiSystem.IsAwake(this))
            {
                Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new SleepAiMessage(this, false));
            }
        }

        public void RemoveBehaviorSet(string behaviorSet)
        {
            if (BehaviorSets.ContainsKey(behaviorSet))
            {
                BehaviorSets.Remove(behaviorSet);
                SortActions();
            }

            if (BehaviorSets.Count == 0)
            {
                Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new SleepAiMessage(this, true));
            }
        }

        /// <summary>
        ///     Whenever the behavior sets are changed we'll re-sort the actions by bonus
        /// </summary>
        protected void SortActions()
        {
            _availableActions.Clear();
            foreach (var actions in BehaviorSets.Values)
            {
                foreach (var action in actions)
                {
                    var found = false;

                    for (var i = 0; i < _availableActions.Count; i++)
                    {
                        if (_availableActions[i].Bonus < action.Bonus)
                        {
                            _availableActions.Insert(i, action);
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        _availableActions.Add(action);
                    }
                }
            }

            _availableActions.Reverse();
        }

        public override void Initialize()
        {
            base.Initialize();
            _planCooldownRemaining = PlanCooldown;
            // TODO: Probably make blackboard a component
            _blackboard = new Blackboard(Owner);
            _planner = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AiActionSystem>();
            if (Owner.TryGetComponent(out IDamageableComponent? damageableComponent))
            {
                damageableComponent.HealthChangedEvent += DeathHandle;
            }
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataReadWriteFunction("behaviorSets", new List<string>(),
                sets =>
                {
                    foreach (var behaviorSet in sets)
                    {
                        AddBehaviorSet(behaviorSet, false);
                    }
                    SortActions();
                },
                () => BehaviorSets.Keys.ToList());
        }

        public override void OnRemove()
        {
            base.OnRemove();
            // TODO: If DamageableComponent removed still need to unsubscribe?
            // TODO: smug bb
            if (Owner.TryGetComponent(out IDamageableComponent? damageableComponent))
            {
                damageableComponent.HealthChangedEvent -= DeathHandle;
            }

            var currentOp = ActualCurrentAction?.ActionOperators.Peek();
            currentOp?.Shutdown(Outcome.Failed);
        }

        private void DeathHandle(HealthChangedEventArgs eventArgs)
        {
            var oldDeadState = _isDead;
            _isDead = eventArgs.Damageable.CurrentState == DamageState.Dead || eventArgs.Damageable.CurrentState == DamageState.Critical;

            if (oldDeadState != _isDead)
            {
                switch (_isDead)
                {
                    case true:
                        Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new SleepAiMessage(this, true));
                        break;
                    case false:
                        Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new SleepAiMessage(this, false));
                        break;
                }
            }
        }

        private void ReceivedAction()
        {
            switch (_actionRequest?.Exception)
            {
                case null:
                    break;
                default:
                    Logger.FatalS("ai", _actionRequest.Exception.ToString());
                    return;
            }
            var action = _actionRequest?.Result;
            _actionRequest = null;
            // Actions with lower scores should be implicitly dumped by GetAction
            // If we're not allowed to replace the action with an action of the same type then dump.
            if (action == null || !action.CanOverride && ActualCurrentAction?.Type == action.GetType())
            {
                return;
            }

            var currentOp = ActualCurrentAction?.ActionOperators.Peek();
            if (currentOp != null && currentOp.HasStartup)
            {
                currentOp.Shutdown(Outcome.Failed);
            }

            ActualCurrentAction = (action.GetType(), action.GetOperators(_blackboard));
        }

        public override void Update(float frameTime)
        {
            // If we asked for a new action we don't want to dump the existing one.
            if (_actionRequest != null)
            {
                if (_actionRequest.Status != JobStatus.Finished)
                {
                    return;
                }

                ReceivedAction();
                // Do something next tick
                return;
            }

            _planCooldownRemaining -= frameTime;

            // Might find a better action while we're doing one already
            if (_planCooldownRemaining <= 0.0f)
            {
                _planCooldownRemaining = PlanCooldown;
                _actionCancellation = new CancellationTokenSource();
                _actionRequest = _planner.RequestAction(new AiActionRequest(Owner.Uid, _blackboard, _availableActions), _actionCancellation);

                return;
            }

            // When we spawn in we won't get an action for a bit
            if (CurrentAction == null)
            {
                return;
            }

            var outcome = CurrentAction.Execute(frameTime);

            switch (outcome)
            {
                case Outcome.Success:
                    if (CurrentAction.ActionOperators.Count == 0)
                    {
                        CurrentAction.Shutdown();
                        CurrentAction = null;
                        // Nothing to compare new action to
                        _blackboard.GetState<LastUtilityScoreState>().SetValue(0.0f);
                    }
                    break;
                case Outcome.Continuing:
                    break;
                case Outcome.Failed:
                    CurrentAction.Shutdown();
                    CurrentAction = null;
                    _blackboard.GetState<LastUtilityScoreState>().SetValue(0.0f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
