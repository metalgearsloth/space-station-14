using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.AI.HTN.WorldState.States.Combat.Equipped;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Selector.Combat
{
    public sealed class ReloadRangedWeapon : SelectorTask
    {
        public override string Name => "ReloadRangedWeapon";

        public ReloadRangedWeapon(IEntity owner) : base(owner) {}
        public override bool PreconditionsMet(AiWorldState context)
        {
            return context.GetStateValue<EquippedRangedWeapon, IEntity>() != null;
        }

        public override void SetupMethods(AiWorldState context)
        {
            Methods = new List<IAiTask>()
            {
                // Reload ballistic
                // Reload laser
            };
        }
    }
}
