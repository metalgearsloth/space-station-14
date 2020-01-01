using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Clothing
{
    public class EquipUniform : CompoundTask
    {
        public EquipUniform(IEntity owner) : base(owner)
        {
        }

        public override string Name => "EquipUniform";

        public override bool PreconditionsMet(AiWorldState context)
        {
            return Owner.HasComponent<InventoryComponent>();
        }

        public override void SetupMethods(AiWorldState context)
        {
            Methods = new List<IAiTask>
            {
                new EquipBackpack(Owner),
                new EquipBelt(Owner),
                new EquipGloves(Owner),
                new EquipHead(Owner),
                new EquipInnerClothing(Owner),
                new EquipOuterClothing(Owner),
                new EquipShoes(Owner),
            };
        }
    }
}
