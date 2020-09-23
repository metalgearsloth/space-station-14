#nullable enable
using Content.Client.UserInterface.Stylesheets;
using Content.Client.Utility;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using System;
using System.Collections.Generic;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.GameObjects.Verbs;
using Content.Shared.Interfaces;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    public class ClientBoltActionBarrelComponent : SharedBoltActionBarrelComponent, IExamine, IItemStatus
    {

        private bool? _chamber;
        private Stack<bool?> _ammo = new Stack<bool?>();
        
        private Queue<bool> _toFireAmmo = new Queue<bool>();

        private StatusControl? _statusControl;

        // TODO: Do this on pump I think
        /// <summary>
        ///     Not including chamber
        /// </summary>
        private int ShotsLeft => _ammo.Count + UnspawnedCount;

        public override void Initialize()
        {
            base.Initialize();
            if (FillPrototype == null)
            {
                UnspawnedCount = 0;
            }
            else
            {
                UnspawnedCount += Capacity;
            }
            UpdateAppearance();
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
           if (!(curState is BoltActionBarrelComponentState cast))
                return;

           UnspawnedCount = 0;
           _chamber = cast.Chamber;
           _ammo = cast.Bullets;
           SetBolt(cast.BoltOpen);
            _statusControl?.Update();
        }

        protected override void SetBolt(bool value)
        {
            if (BoltOpen == value)
            {
                return;
            }

            var gunSystem = EntitySystem.Get<SharedRangedWeaponSystem>();

            if (value)
            {
                TryEjectChamber();
                if (SoundBoltOpen != null)
                {
                    gunSystem.PlaySound(Shooter(), Owner, SoundBoltOpen);
                }
            }
            else
            {
                TryFeedChamber();
                if (SoundBoltClosed != null)
                {
                    gunSystem.PlaySound(Shooter(), Owner, SoundBoltClosed);
                }
            }

            BoltOpen = value;
            UpdateAppearance();
            _statusControl?.Update();
        }

        private void UpdateAppearance()
        {
            if (!Owner.TryGetComponent(out AppearanceComponent? appearanceComponent))
            {
                return;
            }
            
            appearanceComponent.SetData(BarrelBoltVisuals.BoltOpen, BoltOpen);
            appearanceComponent.SetData(AmmoVisuals.AmmoCount, ShotsLeft + (_chamber != null ? 1 : 0));
            appearanceComponent.SetData(AmmoVisuals.AmmoMax, (int) Capacity);
        }

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }

            if (_chamber != null)
            {
                _toFireAmmo.Enqueue(_chamber.Value);
                _chamber = null;

                if (AutoCycle)
                {
                    Cycle();
                }
                return true;
            }
            
            if (AutoCycle && _ammo.Count > 0)
            {
                Cycle();
            }

            return false;
        }

        protected override void Cycle(bool manual = false)
        {
            TryEjectChamber();
            TryFeedChamber();
            var shooter = Shooter();

            if (_chamber == null && manual)
            {
                SetBolt(true);
                if (shooter != null)
                {
                    Owner.PopupMessage(shooter, Loc.GetString("Bolt opened"));
                }
                return;
            }

            //AudioParams.Default.WithVolume(-2)
            //EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(shooter, Owner, SoundCycle, true);
        }

        public override bool TryInsertBullet(IEntity user, SharedAmmoComponent ammoComponent)
        {
            throw new NotImplementedException();
        }

        protected override void TryEjectChamber()
        {
            _chamber = null;
        }

        protected override void TryFeedChamber()
        {
            if (_ammo.TryPop(out var ammo))
            {
                _chamber = ammo;
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundCycle, true);
                return;
            }

            if (UnspawnedCount > 0)
            {
                _chamber = true;
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundCycle, true);
                UnspawnedCount--;
            }
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            DebugTools.Assert(shotCount == _toFireAmmo.Count);

            while (_toFireAmmo.Count > 0)
            {
                var entity = _toFireAmmo.Dequeue();
                var sound = entity ? SoundGunshot : SoundEmpty;
                
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, sound);
            }
            
            UpdateAppearance();
            _statusControl?.Update();
        }

        void IExamine.Examine(FormattedMessage message, bool inDetailsRange)
        {
            message.AddMarkup(Loc.GetString("\nIt uses [color=white]{0}[/color] ammo.", Caliber));
        }

        [Verb]
        private sealed class OpenBoltVerb : Verb<ClientBoltActionBarrelComponent>
        {
            protected override void GetData(IEntity user, ClientBoltActionBarrelComponent component, VerbData data)
            {
                // TODO: Check shooter on the other verbs
                if (!ActionBlockerSystem.CanInteract(user) || component.Shooter() != user)
                {
                    data.Visibility = VerbVisibility.Invisible;
                    return;
                }

                data.Text = Loc.GetString("Open bolt");
                data.Visibility = component.BoltOpen ? VerbVisibility.Invisible : VerbVisibility.Visible;
            }

            protected override void Activate(IEntity user, ClientBoltActionBarrelComponent component)
            {
                component.SetBolt(true);
                component.SendNetworkMessage(new BoltChangedComponentMessage(component.BoltOpen));
            }
        }

        [Verb]
        private sealed class CloseBoltVerb : Verb<ClientBoltActionBarrelComponent>
        {
            protected override void GetData(IEntity user, ClientBoltActionBarrelComponent component, VerbData data)
            {
                if (!ActionBlockerSystem.CanInteract(user) || component.Shooter() != user)
                {
                    data.Visibility = VerbVisibility.Invisible;
                    return;
                }

                data.Text = Loc.GetString("Close bolt");
                data.Visibility = component.BoltOpen ? VerbVisibility.Visible : VerbVisibility.Invisible;
            }

            protected override void Activate(IEntity user, ClientBoltActionBarrelComponent component)
            {
                component.SetBolt(false);
                component.SendNetworkMessage(new BoltChangedComponentMessage(component.BoltOpen));
            }
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
            private readonly ClientBoltActionBarrelComponent _parent;
            private readonly HBoxContainer _bulletsListTop;
            private readonly HBoxContainer _bulletsListBottom;
            private readonly TextureRect _chamberedBullet;
            private readonly Label _noMagazineLabel;

            public StatusControl(ClientBoltActionBarrelComponent parent)
            {
                _parent = parent;
                SizeFlagsHorizontal = SizeFlags.FillExpand;
                SizeFlagsVertical = SizeFlags.ShrinkCenter;
                AddChild(new VBoxContainer
                {
                    SizeFlagsHorizontal = SizeFlags.FillExpand,
                    SizeFlagsVertical = SizeFlags.ShrinkCenter,
                    SeparationOverride = 0,
                    Children =
                    {
                        (_bulletsListTop = new HBoxContainer {SeparationOverride = 0}),
                        new HBoxContainer
                        {
                            SizeFlagsHorizontal = SizeFlags.FillExpand,
                            Children =
                            {
                                new Control
                                {
                                    SizeFlagsHorizontal = SizeFlags.FillExpand,
                                    Children =
                                    {
                                        (_bulletsListBottom = new HBoxContainer
                                        {
                                            SizeFlagsVertical = SizeFlags.ShrinkCenter,
                                            SeparationOverride = 0
                                        }),
                                        (_noMagazineLabel = new Label
                                        {
                                            Text = "No Magazine!",
                                            StyleClasses = {StyleNano.StyleClassItemStatus}
                                        })
                                    }
                                },
                                (_chamberedBullet = new TextureRect
                                {
                                    Texture = StaticIoC.ResC.GetTexture("/Textures/Interface/ItemStatus/Bullets/chambered.png"),
                                    SizeFlagsVertical = SizeFlags.ShrinkCenter,
                                    SizeFlagsHorizontal = SizeFlags.ShrinkEnd | SizeFlags.Fill,
                                })
                            }
                        }
                    }
                });
            }

            public void Update()
            {
                _chamberedBullet.ModulateSelfOverride =
                    _parent._chamber != null ?
                    !_parent._chamber.Value ? Color.Red : Color.FromHex("#d7df60")
                    : Color.Black;

                _bulletsListTop.RemoveAllChildren();
                _bulletsListBottom.RemoveAllChildren();

                var count = _parent.ShotsLeft;
                // Excluding chamber
                var capacity = _parent.Capacity - 1;

                _noMagazineLabel.Visible = false;

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

                const int tinyMaxRow = 60;

                if (capacity > tinyMaxRow)
                {
                    FillBulletRow(_bulletsListBottom, Math.Min(tinyMaxRow, count), tinyMaxRow, texture);
                    FillBulletRow(_bulletsListTop, Math.Max(0, count - tinyMaxRow), capacity - tinyMaxRow, texture);
                }
                else
                {
                    FillBulletRow(_bulletsListBottom, count, capacity, texture);
                }
            }

            private static void FillBulletRow(Control container, int count, int capacity, Texture texture)
            {
                var colorA = Color.FromHex("#b68f0e");
                var colorB = Color.FromHex("#d7df60");
                var colorGoneA = Color.FromHex("#000000");
                var colorGoneB = Color.FromHex("#222222");

                var altColor = false;

                for (var i = count; i < capacity; i++)
                {
                    container.AddChild(new TextureRect
                    {
                        Texture = texture,
                        ModulateSelfOverride = altColor ? colorGoneA : colorGoneB
                    });

                    altColor ^= true;
                }

                for (var i = 0; i < count; i++)
                {
                    container.AddChild(new TextureRect
                    {
                        Texture = texture,
                        ModulateSelfOverride = altColor ? colorA : colorB
                    });

                    altColor ^= true;
                }
            }

            protected override Vector2 CalculateMinimumSize()
            {
                return Vector2.ComponentMax((0, 15), base.CalculateMinimumSize());
            }
        }
    }
}
