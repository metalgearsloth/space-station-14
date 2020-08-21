using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged
{
    public abstract class SharedRangedWeaponComponent : Component
    {
        // Each RangedWeapon should have a RangedWeapon component +
        // some kind of RangedBarrelComponent (this dictates what ammo is retrieved).
        public override string Name => "RangedWeapon";
        public override uint? NetID => ContentNetIDs.RANGED_WEAPON;
        
        public float TimeSinceLastFire { get; protected set; }
        
        public Func<bool> WeaponCanFireHandler;
        
        public Func<IEntity, bool> UserCanFireHandler;
        
        public Action<IEntity, MapId, Vector2> FireHandler;
    }

    [Serializable, NetSerializable]
    public sealed class RangedWeaponComponentState : ComponentState
    {
        public FireRateSelector FireRateSelector { get; }
        
        public RangedWeaponComponentState(
            FireRateSelector fireRateSelector
            ) : base(ContentNetIDs.RANGED_WEAPON)
        {
            FireRateSelector = fireRateSelector;
        }
    }

    /// <summary>
    ///     A component message raised when the weapon is fired at a position on the map.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class FirePosComponentMessage : ComponentMessage
    {
        public MapId MapId { get; }
        
        public Vector2 Position { get; }

        /// <summary>
        /// Constructs a new instance of <see cref="FirePosComponentMessage"/>.
        /// </summary>
        public FirePosComponentMessage(MapId mapId, Vector2 position)
        {
            MapId = mapId;
            Position = position;
        }
    }
}
