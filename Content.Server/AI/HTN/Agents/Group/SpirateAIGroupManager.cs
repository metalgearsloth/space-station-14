using System;
using System.Reflection;
using Content.Server.AI.HTN.Agents.Individual;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.Tasks.Selector.Combat;
using Content.Server.AI.HTN.Tasks.Selector.Nutrition;
using Content.Server.AI.HTN.Tasks.Sequence.Combat;
using Content.Server.GameObjects.Components.Nutrition;
using Content.Server.GameObjects.EntitySystems;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.Agents.Group
{
    public sealed class SpirateAiGroupManager : GroupAiManager
    {
        // TODO: Group blackboard

        /// <inheritdoc />
        public override void Setup()
        {
            var systemManager = IoCManager.Resolve<IEntitySystemManager>();
            var aiSystem = systemManager.GetEntitySystem<AiSystem>();
            aiSystem.RequestAiManager += agent => { TryTakeAgent(agent); };
        }

        /// <inheritdoc />
        protected override bool TryTakeAgent(AiAgent agent)
        {
            if (agent.AiManager != null) return false;
            var attribute = agent.GetType().GetCustomAttribute<AiLogicProcessorAttribute>();
            if (attribute.SerializeName != "Spirate") return false;
            _agents.Add(agent);
            SetupAgent(agent);
            return true;
        }

        /// <inheritdoc />
        protected override void HandlePlanOutcome(PlanUpdate update)
        {
            var taskType = update.Task.GetType();

            switch (update.Outcome)
            {
                case PlanOutcome.PlanningFailed:
                {
                    switch (update.Task)
                    {
                        // You think darkness is your ally?
                        case KillNearestPlayer _:
                            break;
                        default:
                            update.Agent.DeprioritiseRootTask(taskType);
                            break;
                    }

                    break;
                }

                case PlanOutcome.PlanAborted:
                    switch (update.Task)
                    {
                        case KillNearestPlayer _:
                            break;
                        default:
                            update.Agent.DeprioritiseRootTask(taskType);
                            break;
                    }

                    break;
                case PlanOutcome.Continuing:
                    break;
                case PlanOutcome.Success:
                    switch (update.Task)
                    {
                        case KillNearestPlayer _:
                            break;
                        default:
                            update.Agent.DeprioritiseRootTask(taskType);
                            break;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <inheritdoc />
        protected override void SetupAgent(AiAgent agent)
        {
            base.SetupAgent(agent);
            // Listeners
            if (agent.SelfEntity.TryGetComponent(out HungerComponent hungerComponent))
            {
                HandleHunger(agent, hungerComponent.CurrentHungerThreshold);
                hungerComponent.ThresholdChange += threshold => HandleHunger(agent, threshold);
            }

            if (agent.SelfEntity.TryGetComponent(out ThirstComponent thirstComponent))
            {
                HandleThirst(agent, thirstComponent.CurrentThirstThreshold);
                thirstComponent.ThresholdChange += threshold => HandleThirst(agent, threshold);
            }

            agent.AddRootTask(new KillNearestPlayer(agent.SelfEntity));

            agent.AddRootTask(new PickupNearestMeleeWeapon(agent.SelfEntity));

            agent.AddRootTask(new WanderStation(agent.SelfEntity));

            /*
            if (agent.SelfEntity.HasComponent<InventoryComponent>())
            {
                agent.AddRootTask(AiAgent.RootTaskPriority.Normal, new EquipUniform(agent.SelfEntity));
            }
            */
        }

        private void HandleHunger(AiAgent agent, HungerThreshold threshold)
        {
            switch (threshold)
            {
                case HungerThreshold.Overfed:
                    agent.RemoveRootTask(typeof(EatFood));
                    break;
                case HungerThreshold.Okay:
                    agent.RemoveRootTask(typeof(EatFood));
                    break;
                case HungerThreshold.Peckish:
                    agent.AddRootTask(new EatFood(agent.SelfEntity));
                    break;
                case HungerThreshold.Starving:
                    agent.AddRootTask(new EatFood(agent.SelfEntity));
                    break;
                case HungerThreshold.Dead:
                    agent.RemoveRootTask(typeof(EatFood));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(threshold), threshold, null);
            }
        }

        private void HandleThirst(AiAgent agent, ThirstThreshold threshold)
        {
            switch (threshold)
            {
                case ThirstThreshold.OverHydrated:
                    agent.RemoveRootTask(typeof(DrinkDrink));
                    break;
                case ThirstThreshold.Okay:
                    agent.RemoveRootTask(typeof(DrinkDrink));
                    break;
                case ThirstThreshold.Thirsty:
                    agent.AddRootTask(new DrinkDrink(agent.SelfEntity));
                    break;
                case ThirstThreshold.Parched:
                    agent.AddRootTask(new DrinkDrink(agent.SelfEntity));
                    break;
                case ThirstThreshold.Dead:
                    agent.RemoveRootTask(typeof(DrinkDrink));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(threshold), threshold, null);
            }
        }
    }
}
