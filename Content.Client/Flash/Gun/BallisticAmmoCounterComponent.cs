using System;
using Content.Client.GameObjects.EntitySystems;
using Content.Client.IoC;
using Content.Client.Items.Components;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    // Temporary Event don't fucken @ me
    internal sealed class AmmoCounterDirtyEvent : EntityEventArgs
    {

    }

    [RegisterComponent]
    internal sealed class BallisticAmmoCounterComponent : SharedAmmoCounterComponent, IItemStatus
    {
        // TODO: make this handle both battery and ballistics?

        /// <summary>
        ///     True if a bullet is chambered.
        /// </summary>
        [ViewVariables]
        public bool Chambered { get; set; }

        /// <summary>
        ///     Count of bullets in the magazine.
        /// </summary>
        /// <remarks>
        ///     Null if no magazine is inserted.
        /// </remarks>
        [ViewVariables]
        public (int count, int max)? MagazineCount { get; set; }

        [ViewVariables(VVAccess.ReadWrite)] [DataField("lmgAlarmAnimation")] private bool _isLmgAlarmAnimation = default;

        private StatusControl? _statusControl;

        public void UpdateControl()
        {
            _statusControl?.Update();
        }

        public Control MakeControl()
        {
            // TODO: God this is ugly but interactions aren't predicted
            Owner.EntityManager.EventBus.RaiseLocalEvent(Owner.Uid, new AmmoCounterDirtyEvent());
            _statusControl = new StatusControl(this);
            _statusControl.Update();
            return _statusControl;
        }

        public void DestroyControl(Control control)
        {
            if (_statusControl != control) return;

            Owner.EntityManager.EventBus.RaiseLocalEvent(Owner.Uid, new AmmoCounterDirtyEvent());
            _statusControl = null;
        }

        private sealed class StatusControl : Control
        {
            private readonly HBoxContainer _bulletsList;
            private readonly TextureRect _chamberedBullet;
            private readonly Label _noMagazineLabel;
            private readonly Label _ammoCount;

            private BallisticAmmoCounterComponent _parent;

            public StatusControl(BallisticAmmoCounterComponent parent)
            {
                _parent = parent;
                MinHeight = 15;
                HorizontalExpand = true;
                VerticalAlignment = VAlignment.Center;

                AddChild(new HBoxContainer
                {
                    HorizontalExpand = true,
                    Children =
                    {
                        (_chamberedBullet = new TextureRect
                        {
                            Texture = StaticIoC.ResC.GetTexture("/Textures/Interface/ItemStatus/Bullets/chambered_rotated.png"),
                            VerticalAlignment = VAlignment.Center,
                            HorizontalAlignment = HAlignment.Right,
                        }),
                        new Control { MinSize = (5,0) },
                        new Control
                        {
                            HorizontalExpand = true,
                            Children =
                            {
                                (_bulletsList = new HBoxContainer
                                {
                                    VerticalAlignment = VAlignment.Center,
                                    SeparationOverride = 0
                                }),
                                (_noMagazineLabel = new Label
                                {
                                    Text = "No Magazine!",
                                    StyleClasses = {StyleNano.StyleClassItemStatus}
                                })
                            }
                        },
                        new Control { MinSize = (5,0) },
                        (_ammoCount = new Label
                        {
                            StyleClasses = {StyleNano.StyleClassItemStatus},
                            HorizontalAlignment = HAlignment.Right,
                        }),
                    }
                });
            }

            public void Update()
            {
                _chamberedBullet.ModulateSelfOverride =
                    _parent.Chambered ? Color.FromHex("#d7df60") : Color.Black;

                _bulletsList.RemoveAllChildren();

                if (_parent.MagazineCount == null)
                {
                    _noMagazineLabel.Visible = true;
                    _ammoCount.Visible = false;
                    return;
                }

                var (count, capacity) = _parent.MagazineCount.Value;

                _noMagazineLabel.Visible = false;
                _ammoCount.Visible = true;

                var texturePath = "/Textures/Interface/ItemStatus/Bullets/normal.png";
                var texture = StaticIoC.ResC.GetTexture(texturePath);

                _ammoCount.Text = $"x{count:00}";
                capacity = Math.Min(capacity, 20);
                FillBulletRow(_bulletsList, count, capacity, texture);
            }

            private static void FillBulletRow(Control container, int count, int capacity, Texture texture)
            {
                var colorA = Color.FromHex("#b68f0e");
                var colorB = Color.FromHex("#d7df60");
                var colorGoneA = Color.FromHex("#000000");
                var colorGoneB = Color.FromHex("#222222");

                var altColor = false;

                // Draw the empty ones
                for (var i = count; i < capacity; i++)
                {
                    container.AddChild(new TextureRect
                    {
                        Texture = texture,
                        ModulateSelfOverride = altColor ? colorGoneA : colorGoneB,
                        Stretch = TextureRect.StretchMode.KeepCentered
                    });

                    altColor ^= true;
                }

                // Draw the full ones, but limit the count to the capacity
                count = Math.Min(count, capacity);
                for (var i = 0; i < count; i++)
                {
                    container.AddChild(new TextureRect
                    {
                        Texture = texture,
                        ModulateSelfOverride = altColor ? colorA : colorB,
                        Stretch = TextureRect.StretchMode.KeepCentered
                    });

                    altColor ^= true;
                }
            }

            public void PlayAlarmAnimation()
            {
                var animation = _parent._isLmgAlarmAnimation ? AmmoCounterSystem.AlarmAnimationLmg : AmmoCounterSystem.AlarmAnimationSmg;
                _noMagazineLabel.PlayAnimation(animation, "alarm");
            }
        }
    }
}
