using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.Tasks.Primitive.Combat;
using Content.Server.AI.HTN.Tasks.Sequence.Combat;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Selector.Combat
{
    public class KillNearestPlayer : SelectorTask
    {
        private IEntity _nearestPlayer;
        public KillNearestPlayer(IEntity owner) : base(owner)
        {
        }

        public override string Name => "MeleeAttackNearestPlayer";
        public override bool PreconditionsMet(AiWorldState context)
        {
            foreach (var entity in context.GetEnumerableStateValue<NearbyPlayers, IEntity>())
            {
                _nearestPlayer = entity;
                break;
            }

            return _nearestPlayer != null;
        }

        public override void SetupMethods(AiWorldState context)
        {
            Methods = new List<IAiTask>
            {
                new MeleeAttackTarget(Owner, _nearestPlayer),
                new PickupNearestMeleeWeapon(Owner),
            };
        }
    }
}
