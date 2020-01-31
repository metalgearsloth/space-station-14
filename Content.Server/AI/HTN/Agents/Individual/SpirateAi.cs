using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Primitive.Combat;
using Content.Server.AI.HTN.Tasks.Primitive.Combat.Melee;
using Content.Server.AI.HTN.Tasks.Primitive.Inventory;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Robust.Server.AI;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.Agents.Individual
{
    [AiLogicProcessor("Spirate")]
    public class SpirateAi : AiAgent
    {
        protected override float PlanCooldown => 0.5f;

        // Phrases

        public override void HandleTaskOutcome(PrimitiveTask task, Outcome outcome)
        {
            base.HandleTaskOutcome(task, outcome);

            var random = IoCManager.Resolve<IRobustRandom>();

            switch (task)
            {
                case PickupItem _:
                    switch (outcome)
                    {
                        case Outcome.Success:
                            Bark("Come to pappa");
                            break;
                    }

                    break;
                case SwingMeleeWeapon _:
                    switch (outcome)
                    {
                        case Outcome.Success:
                            Bark("Yarrr!");
                            break;
                    }

                    break;
            }
        }
    }
}
