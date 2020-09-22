#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using Component = Robust.Shared.GameObjects.Component;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged
{
    public abstract class SharedRangedMagazineComponent : Component, IInteractUsing, IUse
    {
        public override string Name => "RangedMagazine";

        public override uint? NetID => ContentNetIDs.RANGED_MAGAZINE;

        /*
        private Stack<IEntity> _spawnedAmmo = new Stack<IEntity>();
        private Container _ammoContainer;
        

        public int ShotsLeft => _spawnedAmmo.Count + _unspawnedCount;
        */
        
        public abstract int ShotsLeft { get; }
        
        [ViewVariables]
        public ushort Capacity { get; set; }

        [ViewVariables]
        public MagazineType MagazineType { get; private set; }
        
        [ViewVariables]
        public BallisticCaliber Caliber { get; private set; }

        // If there's anything already in the magazine
        [ViewVariables]
        protected string? FillPrototype { get; private set; }
        // By default the magazine won't spawn the entity until needed so we need to keep track of how many left we can spawn
        // Generally you probablt don't want to use this
        protected int UnspawnedCount;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction("capacity", (ushort) 20, value => Capacity = value, () => Capacity);
            
            serializer.DataReadWriteFunction("magazineType", new List<MagazineType>(), magTypes => magTypes.ForEach(magType => MagazineType |= magType),
                () =>
                {
                    var magTypes = new List<MagazineType>();

                    foreach (var magType in magTypes)
                    {
                        MagazineType |= magType;
                    }

                    return magTypes;
                });
            
            serializer.DataReadWriteFunction("caliber", BallisticCaliber.Unspecified, value => Caliber = value, () => Caliber);
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

        protected abstract bool TryInsertAmmo(IEntity user, IEntity ammo);

        async Task<bool> IInteractUsing.InteractUsing(InteractUsingEventArgs eventArgs)
        {
            return TryInsertAmmo(eventArgs.User, eventArgs.Using);
        }

        protected abstract bool Use(IEntity user);

        bool IUse.UseEntity(UseEntityEventArgs eventArgs)
        {
            return Use(eventArgs.User);
        }
    }

    [Serializable, NetSerializable]
    public sealed class RangedMagazineComponentState : ComponentState
    {
        public ushort Capacity { get; }

        public Stack<bool> SpawnedAmmo { get; }
        
        public RangedMagazineComponentState(ushort capacity, Stack<bool> spawnedAmmo) : base(ContentNetIDs.RANGED_MAGAZINE)
        {
            Capacity = capacity;
            SpawnedAmmo = spawnedAmmo;
        }
    }
}