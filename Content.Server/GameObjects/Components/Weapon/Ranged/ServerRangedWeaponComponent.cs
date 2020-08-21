using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.Components.Weapon.Ranged.Barrels;
using Content.Shared.Damage;
using Content.Shared.GameObjects.Components.Damage;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Players;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    public sealed class ServerRangedWeaponComponent : SharedRangedWeaponComponent
    {
        [ViewVariables(VVAccess.ReadWrite)]
        public bool ClumsyCheck { get; set; }
        [ViewVariables(VVAccess.ReadWrite)]
        public float ClumsyExplodeChance { get; set; }

        public ServerRangedBarrelComponent Barrel => Owner.GetComponent<ServerRangedBarrelComponent>();

        private FireRateSelector FireRateSelector => Barrel?.FireRateSelector ?? FireRateSelector.Safety;
        
        private readonly Queue<FirePosComponentMessage> _queuedMessages = new Queue<FirePosComponentMessage>();
        
        private bool WeaponCanFire()
        {
            return WeaponCanFireHandler == null || WeaponCanFireHandler();
        }

        private bool UserCanFire(IEntity user)
        {
            return (UserCanFireHandler == null || UserCanFireHandler(user)) && ActionBlockerSystem.CanAttack(user);
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(this, p => p.ClumsyCheck, "clumsyCheck", true);
            serializer.DataField(this, p => p.ClumsyExplodeChance, "clumsyExplodeChance", 0.5f);
        }

        /// <inheritdoc />
        public override void HandleNetworkMessage(ComponentMessage message, INetChannel channel, ICommonSession session = null)
        {
            base.HandleNetworkMessage(message, channel, session);

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            switch (message)
            {
                case FirePosComponentMessage msg:
                    var user = session.AttachedEntity;
                    if (user == null)
                    {
                        return;
                    }
                    
                    // TODO: Validate user is holding the thing.

                    _queuedMessages.Enqueue(msg);

                    break;
            }
        }

        public override ComponentState GetComponentState()
        {
            return new RangedWeaponComponentState(FireRateSelector);
        }

        public void Update(float frameTime)
        {
            TimeSinceLastFire += frameTime;
            
            if (_queuedMessages.Count == 0)
            {
                return;
            }

            var message = _queuedMessages.Peek();

            if (TryFire(message))
            {
                _queuedMessages.Dequeue();
            }
        }

        /// <summary>
        ///     
        /// </summary>
        /// <param name="message"></param>
        /// <returns>True if we should dequeue the message</returns>
        private bool TryFire(FirePosComponentMessage message)
        {
            ContainerHelpers.TryGetContainer(Owner, out var container);
            var user = container?.Owner;

            if (user == null || 
                !user.TryGetComponent(out HandsComponent hands) || 
                hands.GetActiveHand?.Owner != Owner || 
                !user.TryGetComponent(out CombatModeComponent combat) || 
                !combat.IsInCombatMode)
            {
                return true;
            }

            if (!UserCanFire(user) || !WeaponCanFire())
            {
                return true;
            }
            
            if (TimeSinceLastFire < 1 / Barrel.FireRate)
            {
                return false;
            }

            // TODO: Should really be -=
            TimeSinceLastFire = 0.0f;

            if (ClumsyCheck &&
                user.HasComponent<ClumsyComponent>() &&
                IoCManager.Resolve<IRobustRandom>().Prob(ClumsyExplodeChance))
            {
                var soundSystem = EntitySystem.Get<AudioSystem>();
                soundSystem.PlayAtCoords("/Audio/Items/bikehorn.ogg",
                    Owner.Transform.GridPosition, AudioParams.Default, 5);

                soundSystem.PlayAtCoords("/Audio/Weapons/Guns/Gunshots/bang.ogg",
                    Owner.Transform.GridPosition, AudioParams.Default, 5);

                if (user.TryGetComponent(out IDamageableComponent health))
                {
                    health.ChangeDamage(DamageType.Blunt, 10, false, user);
                    health.ChangeDamage(DamageType.Heat, 5, false, user);
                }

                if (user.TryGetComponent(out StunnableComponent stun))
                {
                    stun.Paralyze(3f);
                }

                user.PopupMessage(user, Loc.GetString("The gun blows up in your face!"));

                Owner.Delete();
                return true;
            }

            FireHandler?.Invoke(user, message.MapId, message.Position);
            return true;
        }
    }
}
