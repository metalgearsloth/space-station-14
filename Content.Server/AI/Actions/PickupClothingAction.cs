using System.Collections.Generic;
using Content.Server.AI.Processors;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.Actions
{
    public class PickupClothingAction : GoapAction
    {
        public PickupClothingAction(
            EquipmentSlotDefines.Slots slot,
            IDictionary<string, bool> preConditions = null,
            IDictionary<string, bool> effects = null)
        {

            PreConditions.Add("HasHands", true);
            PreConditions.Add("FreeHand", true);

            if (preConditions != null)
            {
                foreach (var precon in preConditions)
                {
                    Effects.Add(precon);
                }
            }

            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    Effects.Add(effect);
                }
            }
        }

        public override float Cost()
        {
            return 5.0f;
        }

        public override bool CheckProceduralPreconditions(IEntity entity)
        {
            // TODO: Test GoapProcessor
            if (!entity.TryGetComponent(out GoapProcessor processor))
            {
                return false;
            }

            foreach (var comp in AIUtils.Visibility.GetNearestEntities(entity.Transform.GridPosition, typeof(ClothingComponent),
                processor.VisionRadius))
            {
                comp.TryGetComponent(out ClothingComponent clothingComponent);
                foreach (var slotFlag in EquipmentSlotDefines.SlotMasks)
                {
                    if ((clothingComponent.SlotFlags & slotFlag.Value) == 0) continue;

                    TargetEntity = comp;
                    return true;
                }
            }

            return false;
        }

        public override bool TryPerformAction(IEntity entity)
        {
            throw new System.NotImplementedException();
        }
    }
}
