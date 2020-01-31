using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Selector.Combat
{
    public class KillNearestPlayer : SelectorTask
    {
        private IEntity _nearestPlayer;
        public KillNearestPlayer(IEntity owner) : base(owner)
        {
        }

        public override string Name => "KillNearestPlayer";

        public override bool PreconditionsMet(AiWorldState context)
        {
            var found = false;

            foreach (var entity in context.GetEnumerableStateValue<NearbyPlayers, IEntity>())
            {
                _nearestPlayer = entity;
                if (!entity.TryGetComponent(out DamageableComponent damageableComponent) || damageableComponent.IsDead()) continue;
                found = true;
                break;
            }

            return found;
        }

        public override void SetupMethods(AiWorldState context)
        {
            Methods = new List<IAiTask>
            {
                new MeleeCombat(Owner, _nearestPlayer),
            };
        }
    }
}
