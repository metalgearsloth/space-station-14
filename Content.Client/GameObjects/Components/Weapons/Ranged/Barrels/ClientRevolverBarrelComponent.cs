using Content.Client.Utility;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.ViewVariables;
using System;
using Content.Client.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeapon))]
    public class ClientRevolverBarrelComponent : SharedRevolverBarrelComponent, IItemStatus
    {
        // TODO: Need a way to make this common

        public override Angle? FireAngle { get; set; }
        
        private StatusControl _statusControl;

        /// <summary>
        /// A array that lists the bullet states
        /// true means a spent bullet
        /// false means a "shootable" bullet
        /// null means no bullet
        /// </summary>
        [ViewVariables]
        public bool?[] Bullets { get; private set; }

        protected override ushort Capacity => (ushort) Bullets.Length;
        
        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction(
                "capacity",
                6,
                cap => Bullets = new bool?[cap],
                () => Bullets.Length);
        }
        
        public override void Initialize()
        {
            base.Initialize();
            
            // Mark every bullet as unspent
            if (FillPrototype != null)
            {
                for (var i = 0; i < Bullets.Length; i++)
                {
                    Bullets[i] = true;
                    UnspawnedCount--;
                }
            }
        }

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
                return false;
            
            if (Bullets[CurrentSlot] == true)
            {
                Bullets[CurrentSlot] = false;
                Cycle();
                _statusControl?.Update();
                return true;
            }

            Cycle();
            _statusControl?.Update();
            return false;
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            var shooter = Shooter();
            
            if (shooter != null && shooter.TryGetComponent(out CameraRecoilComponent recoilComponent))
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

            for (var i = 0; i < Bullets.Length; i++)
            {
                var slot = Bullets[i];

                if (slot == null)
                    continue;

                // TODO: Play SOUND. Once we have prediction and know what the ammo component is.
                
                count++;
                Bullets[i] = null;
            }

            _statusControl?.Update();
            return count;
        }

        protected override bool TryInsertBullet(IEntity user, SharedAmmoComponent ammoComponent)
        {
            if (!base.TryInsertBullet(user, ammoComponent))
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }
            
            for (var i = Bullets.Length - 1; i >= 0; i--)
            {
                var slot = Bullets[i];
                if (slot == null)
                {
                    CurrentSlot = (byte) i;
                    Bullets[i] = !ammoComponent.Spent;
                    // TODO: CLIENT-SIDE PREDICTED CONTAINERS HERE
                    var extraTime = FireRate > 0 ? TimeSpan.FromSeconds(1 / FireRate) : TimeSpan.FromSeconds(0.3);
                    
                    NextFire = IoCManager.Resolve<IGameTiming>().CurTime + extraTime;
                    _statusControl?.Update();
                    return true;
                }
            }
            
            return false;
        }

        // TODO: Copy from existing guns.
        private void Spin()
        {
            CurrentSlot = (ushort) IoCManager.Resolve<IRobustRandom>().Next(Bullets.Length);
            SendNetworkMessage(new RevolverSpinMessage(CurrentSlot));
            // TODO: Predict sound
            EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(null, Owner, SoundSpin, true);
        }

        // Item status etc.
        public override void HandleComponentState(ComponentState curState, ComponentState nextState)
        {
            if (!(curState is RevolverBarrelComponentState cast))
                return;

            CurrentSlot = cast.CurrentSlot;
            Bullets = cast.Bullets;
            _statusControl?.Update();
        }

        public Control MakeControl()
        {
            _statusControl = new StatusControl(this);
            _statusControl.Update();
            return _statusControl;
        }

        public void DestroyControl(Control control)
        {
            if (_statusControl == control)
            {
                _statusControl = null;
            }
        }

        private sealed class StatusControl : Control
        {
            private readonly ClientRevolverBarrelComponent _parent;
            private readonly HBoxContainer _bulletsList;

            public StatusControl(ClientRevolverBarrelComponent parent)
            {
                _parent = parent;
                SizeFlagsHorizontal = SizeFlags.FillExpand;
                SizeFlagsVertical = SizeFlags.ShrinkCenter;
                AddChild((_bulletsList = new HBoxContainer
                {
                    SizeFlagsHorizontal = SizeFlags.FillExpand,
                    SizeFlagsVertical = SizeFlags.ShrinkCenter,
                    SeparationOverride = 0
                }));
            }

            public void Update()
            {
                _bulletsList.RemoveAllChildren();

                var capacity = _parent.Bullets.Length;

                string texturePath;
                if (capacity <= 20)
                {
                    texturePath = "/Textures/Interface/ItemStatus/Bullets/normal.png";
                }
                else if (capacity <= 30)
                {
                    texturePath = "/Textures/Interface/ItemStatus/Bullets/small.png";
                }
                else
                {
                    texturePath = "/Textures/Interface/ItemStatus/Bullets/tiny.png";
                }

                var texture = StaticIoC.ResC.GetTexture(texturePath);
                var spentTexture = StaticIoC.ResC.GetTexture("/Textures/Interface/ItemStatus/Bullets/empty.png");

                FillBulletRow(_bulletsList, texture, spentTexture);
            }

            private void FillBulletRow(Control container, Texture texture, Texture emptyTexture)
            {
                var colorA = Color.FromHex("#b68f0e");
                var colorB = Color.FromHex("#d7df60");
                var colorSpentA = Color.FromHex("#b50e25");
                var colorSpentB = Color.FromHex("#d3745f");
                var colorGoneA = Color.FromHex("#000000");
                var colorGoneB = Color.FromHex("#222222");

                var altColor = false;
                const float scale = 1.3f;

                for (var i = 0; i < _parent.Bullets.Length; i++)
                {
                    var bulletSpent = !_parent.Bullets[i];
                    // Add a outline
                    var box = new Control
                    {
                        CustomMinimumSize = texture.Size * scale,
                    };
                    if (i == _parent.CurrentSlot)
                    {
                        box.AddChild(new TextureRect
                        {
                            Texture = texture,
                            TextureScale = (scale, scale),
                            ModulateSelfOverride = Color.Green,
                        });
                    }
                    Color color;
                    var bulletTexture = texture;

                    if (bulletSpent.HasValue)
                    {
                        if (bulletSpent.Value)
                        {
                            color = altColor ? colorSpentA : colorSpentB;
                            bulletTexture = emptyTexture;
                        }
                        else
                        {
                            color = altColor ? colorA : colorB;
                        }
                    }
                    else
                    {
                        color = altColor ? colorGoneA : colorGoneB;
                    }

                    box.AddChild(new TextureRect
                    {
                        SizeFlagsHorizontal = SizeFlags.Fill,
                        SizeFlagsVertical = SizeFlags.Fill,
                        Stretch = TextureRect.StretchMode.KeepCentered,
                        Texture = bulletTexture,
                        ModulateSelfOverride = color,
                    });
                    altColor ^= true;
                    container.AddChild(box);
                }
            }

            protected override Vector2 CalculateMinimumSize()
            {
                return Vector2.ComponentMax((0, 15), base.CalculateMinimumSize());
            }
        }
    }
}
