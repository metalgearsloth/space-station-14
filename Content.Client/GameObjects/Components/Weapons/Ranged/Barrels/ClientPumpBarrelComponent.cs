#nullable enable
using Content.Client.UserInterface.Stylesheets;
using Content.Client.Utility;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using System;
using System.Collections.Generic;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    public class ClientPumpBarrelComponent : SharedPumpBarrelComponent, IItemStatus, IExamine
    {
        private StatusControl? _statusControl;

        public bool? ChamberContainer { get; private set; }

        public Stack<bool> AmmoContainer { get; private set; } = new Stack<bool>();
        
        private Queue<bool> _toFireAmmo = new Queue<bool>();

        public override void Initialize()
        {
            base.Initialize();
            _statusControl?.Update();
            // TODO: TEMPORARY BEFORE PREDICTIONS
            UnspawnedCount = 0;
        }

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }
            
            if (ChamberContainer != null)
            {
                _toFireAmmo.Enqueue(ChamberContainer.Value);
                if (!ManualCycle)
                {
                    Cycle();
                }

                ChamberContainer = null;
                _statusControl?.Update();
                return true;
            }

            return false;
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            while (_toFireAmmo.Count > 0)
            {
                _toFireAmmo.Dequeue();

                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundGunshot);
            }
        }
        
        protected override void Cycle(bool manual = false)
        {
            // Doesn't work coz no interact prediction
            return;
            
            if (ChamberContainer != null)
            {
                if (AmmoContainer.TryPop(out var ammo))
                {
                    ChamberContainer = ammo;
                }

                ChamberContainer = null;
            }

            if (UnspawnedCount > 0)
            {
                UnspawnedCount--;
                AmmoContainer.Push(true);
            }

            if (manual)
            {
                if (SoundCycle != null)
                {
                    EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundCycle, true);
                }
            }
        }

        // TODO: Need interaction prediction AHHHHHHHHHHHHHHHHHHHH
        
        // I know I know no deadcode but I need interaction predictions
        public override bool TryInsertBullet(IEntity user, IEntity ammo)
        {
            return true;
            
            if (!ammo.TryGetComponent(out SharedAmmoComponent? ammoComponent))
            {
                return false;
            }

            if (ammoComponent.Caliber != Caliber)
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }

            if (AmmoContainer.Count < Capacity - 1)
            {
                AmmoContainer.Push(!ammoComponent.Spent);
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(user, Owner, SoundInsert);
                return true;
            }

            return false;
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            if (!(curState is PumpBarrelComponentState cast))
                return;

            ChamberContainer = cast.Chamber;
            AmmoContainer = cast.Ammo;
            Capacity = cast.Capacity;
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
            private readonly ClientPumpBarrelComponent _parent;
            private readonly HBoxContainer _bulletsListTop;
            private readonly HBoxContainer _bulletsListBottom;
            private readonly TextureRect _chamberedBullet;
            private readonly Label _noMagazineLabel;

            public StatusControl(ClientPumpBarrelComponent parent)
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
                    _parent.ChamberContainer != null ?
                    !_parent.ChamberContainer.Value ? Color.Red : Color.FromHex("#d7df60")
                    : Color.Black;

                _bulletsListTop.RemoveAllChildren();
                _bulletsListBottom.RemoveAllChildren();

                if (_parent.Capacity == 1)
                {
                    _noMagazineLabel.Visible = true;
                    return;
                }

                var capacity = _parent.Capacity;
                var count = _parent.AmmoContainer.Count + _parent.UnspawnedCount;

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

        public void Examine(FormattedMessage message, bool inDetailsRange)
        {
            message.AddMarkup(Loc.GetString("\nIt uses [color=white]{0}[/color] ammo.", Caliber));
        }
    }
}
