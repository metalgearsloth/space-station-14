using System;
using Content.Client.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

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

                if (_firing)
                {
                    SendNetworkMessage(new StartFiringMessage(FireAngle!.Value));
                }
                else
                {
                    SendNetworkMessage(new StopFiringMessage(ShotCounter));
                    ShotCounter = 0;
                }
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
                if (Firing && _fireAngle != null)
                {
                    SendNetworkMessage(new RangedAngleMessage(_fireAngle));
                }
            }
        }
        private Angle? _fireAngle;

        private bool?[] _bullets;
        
        protected override ushort Capacity => (ushort) _bullets.Length;
        
        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction(
                "capacity",
                6,
                cap => _bullets = new bool?[cap],
                () => _bullets.Length);
        }

        public override void Initialize()
        {
            base.Initialize();
            
            // Mark every bullet as unspent
            if (FillPrototype != null)
            {
                for (var i = 0; i < _bullets.Length; i++)
                {
                    _bullets[i] = true;
                    UnspawnedCount--;
                }
            }
        }

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
                return false;
            
            if (_bullets[CurrentSlot] == true)
            {
                _bullets[CurrentSlot] = false;
                Cycle();
                return true;
            }

            return false;
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            CameraRecoilComponent recoilComponent = null;
            var shooter = Shooter();
            
            if (shooter != null && shooter.TryGetComponent(out recoilComponent))
            {
                recoilComponent.Kick(-direction.ToVec().Normalized * 1.1f);
            }

            for (var i = 0; i < shotCount; i++)
            {
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(shooter, Owner, SoundGunshot);
            }
        }

        protected override ushort EjectAllSlots()
        {
            // TODO: Predict
            ushort count = 0;

            for (var i = 0; i < _bullets.Length; i++)
            {
                var slot = _bullets[i];

                if (slot == null)
                    continue;

                // TODO: Play SOUND
                count++;
                slot = null;
            }

            return count;
        }

        protected override bool TryInsertBullet(IEntity user, SharedAmmoComponent ammoComponent)
        {
            if (!base.TryInsertBullet(user, ammoComponent))
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }
            
            for (var i = _bullets.Length - 1; i >= 0; i--)
            {
                var slot = _bullets[i];
                if (slot == null)
                {
                    CurrentSlot = (byte) i;
                    _bullets[i] = !ammoComponent.Spent;
                    // TODO: CLIENT-SIDE PREDICTED CONTAINERS HERE
                    var extraTime = FireRate > 0 ? TimeSpan.FromSeconds(1 / FireRate) : TimeSpan.FromSeconds(0.3);
                    
                    NextFire = IoCManager.Resolve<IGameTiming>().CurTime + extraTime;
                    return true;
                }
            }
            
            return false;
        }
    }
}