#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged
{
    public abstract class SharedSpeedLoaderComponent : Component, IInteractUsing, IUse, IAfterInteract
    {
        public override string Name => "SpeedLoader";
        public override uint? NetID => ContentNetIDs.SPEED_LOADER;

        protected BallisticCaliber Caliber { get; set; }
        public ushort Capacity { get; set; }
        
        /*
        private Container _ammoContainer;
        private Stack<IEntity> _spawnedAmmo;
        */
        
        protected int UnspawnedCount;

        public abstract int ShotsLeft { get; }

        protected string? FillPrototype;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction("caliber", BallisticCaliber.Unspecified, value => Caliber = value, () => Caliber);
            serializer.DataReadWriteFunction("capacity", (ushort) 6, value => Capacity = value, () => Capacity);
            serializer.DataReadWriteFunction("fillPrototype", null, value => FillPrototype = value, () => FillPrototype);
        }

        public override void Initialize()
        {
            base.Initialize();

            if (FillPrototype != null)
            {
                UnspawnedCount += Capacity;
            }
            else
            {
                UnspawnedCount = 0;
            }
        }

        public bool TryInsertAmmo(IEntity user, IEntity entity)
        {
            if (!entity.TryGetComponent(out AmmoComponent ammoComponent))
            {
                return false;
            }

            if (ammoComponent.Caliber != Caliber)
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }

            if (AmmoLeft >= Capacity)
            {
                Owner.PopupMessage(user, Loc.GetString("No room"));
                return false;
            }

            _spawnedAmmo.Push(entity);
            _ammoContainer.Insert(entity);
            UpdateAppearance();
            return true;

        }

        private bool UseEntity(IEntity user)
        {
            if (!user.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            var ammo = TakeAmmo();
            if (ammo == null)
            {
                return false;
            }

            var itemComponent = ammo.GetComponent<ItemComponent>();
            if (!handsComponent.CanPutInHand(itemComponent))
            {
                ServerRangedBarrelComponent.EjectCasing(ammo);
            }
            else
            {
                handsComponent.PutInHand(itemComponent);
            }

            UpdateAppearance();
            return true;
        }

        void IAfterInteract.AfterInteract(AfterInteractEventArgs eventArgs)
        {
            // TODO:
        }

        async Task<bool> IInteractUsing.InteractUsing(InteractUsingEventArgs eventArgs)
        {
            return TryInsertAmmo(eventArgs.User, eventArgs.Using);
        }

        bool IUse.UseEntity(UseEntityEventArgs eventArgs)
        {
            return UseEntity(eventArgs.User);
        }
    }
    
    [Serializable, NetSerializable]
    public sealed class SpeedLoaderComponentState : ComponentState
    {
        public ushort Capacity { get; }
        
        public Stack<bool> Ammo { get; }
        
        public SpeedLoaderComponentState(ushort capacity, Stack<bool> ammo) : base(ContentNetIDs.SPEED_LOADER)
        {
            Capacity = capacity;
            Ammo = ammo;
        }
    }
}