using Content.Client.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.GameObjects.Components.Weapons.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeapon))]
    public class ClientRevolver : SharedRevolverBarrelComponent
    {
        // TODO: Need a way to make this common
        public override bool Firing
        {
            get => _firing;
            set
            {
                if (_firing == value)
                {
                    return;
                }

                _firing = value;
                NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
                SendNetworkMessage(new RangedFiringMessage(Firing));
            }
        }
        
        private bool _firing;

        public override Angle? FireAngle
        {
            get => _fireAngle;
            set
            {
                if (_fireAngle == value)
                {
                    return;
                }

                _fireAngle = value;
                SendNetworkMessage(new RangedAngleMessage(_fireAngle));
            }
        }
        private Angle? _fireAngle;

        public override void MuzzleFlash()
        {
            // TODO:
            return;
        }

        protected override bool TryTakeBullet()
        {
            if (!base.TryTakeBullet())
            {
                return false;
            }
            
            if (AmmoSlots[CurrentSlot] == null && UnspawnedCount > 0)
            {
                var entity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.MapPosition);
                AmmoSlots[CurrentSlot] = entity;
                AmmoContainer.Insert(entity);
            }

            if (AmmoSlots[CurrentSlot]?.GetComponent<SharedAmmoComponent>().CanFire() == true)
            {
                Cycle();
                return true;
            }

            return false;
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            CameraRecoilComponent recoilComponent = null;
            
            if (Shooter()?.TryGetComponent(out recoilComponent) == true)
            {
                recoilComponent.Kick(-direction.ToVec() * 0.15f);
            }
        }
    }
}