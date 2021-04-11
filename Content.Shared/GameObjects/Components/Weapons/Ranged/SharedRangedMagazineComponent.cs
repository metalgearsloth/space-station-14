#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;
using Component = Robust.Shared.GameObjects.Component;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged
{
    public abstract class SharedRangedMagazineComponent : Component, IInteractUsing, IUse
    {
        public override string Name => "RangedMagazine";

        public override uint? NetID => ContentNetIDs.RANGED_MAGAZINE;

        public abstract int ShotsLeft { get; }

        [ViewVariables]
        [DataField("capacity")]
        public int Capacity { get; private set; } = 20;

        [ViewVariables]
        [DataField("magazineType")]
        public MagazineType MagazineType { get; private set; } = MagazineType.Unspecified;

        [ViewVariables]
        [DataField("caliber")]
        public BallisticCaliber Caliber { get; private set; } = BallisticCaliber.Unspecified;

        // If there's anything already in the magazine
        [ViewVariables]
        [DataField("fillPrototype")]
        public EntityPrototype? FillPrototype { get; private set; }

        // By default the magazine won't spawn the entity until needed so we need to keep track of how many left we can spawn
        // Generally you probably don't want to use this
        protected int UnspawnedCount;

        protected abstract bool TryInsertAmmo(IEntity user, IEntity ammo);

        public abstract bool TryPop([NotNullWhen(true)] out SharedAmmoComponent? ammo);

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
        public Stack<bool> SpawnedAmmo { get; }

        public RangedMagazineComponentState(Stack<bool> spawnedAmmo) : base(ContentNetIDs.RANGED_MAGAZINE)
        {
            SpawnedAmmo = spawnedAmmo;
        }
    }

    [Serializable, NetSerializable]
    public sealed class DumpRangedMagazineComponentMessage : ComponentMessage
    {
        public byte Amount { get; }

        public DumpRangedMagazineComponentMessage(byte amount)
        {
            Amount = amount;
            Directed = true;
        }
    }
}
