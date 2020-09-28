#nullable enable
using Content.Client.UserInterface.Stylesheets;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Robust.Client.Graphics.Drawing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.ViewVariables;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Client.GameObjects.Components.Mobs;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Client.GameObjects;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Utility;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    public class ClientBatteryBarrelComponent : SharedBatteryBarrelComponent, IItemStatus
    {
        private StatusControl? _statusControl;

        /// <summary>
        ///     Count of bullets in the magazine.
        /// </summary>
        /// <remarks>
        ///     Null if no magazine is inserted.
        ///     Didn't call it Capacity because that's the battery capacity rather than shots left capacity like the other guns.
        /// </remarks>
        [ViewVariables]
        public (float CurrentCharge, float MaxCharge)? PowerCell { get; private set; }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            if (!(curState is BatteryBarrelComponentState cast))
                return;

            PowerCell = cast.PowerCell;
            _statusControl?.Update();
            UpdateAppearance();
        }

        private void UpdateAppearance()
        {
            if (!Owner.TryGetComponent(out AppearanceComponent? appearanceComponent))
                return;

            var count = PowerCell?.CurrentCharge / BaseFireCost ?? 0;
            var max = PowerCell?.MaxCharge / BaseFireCost ?? 0;
            
            appearanceComponent?.SetData(MagazineBarrelVisuals.MagLoaded, PowerCell != null);
            appearanceComponent?.SetData(AmmoVisuals.AmmoCount, count);
            appearanceComponent?.SetData(AmmoVisuals.AmmoMax, max);
        }

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
                return false;
            
            if (PowerCell == null)
                return false;

            var (currentCharge, maxCharge) = PowerCell.Value;
            if (currentCharge < LowerChargeLimit)
                return false;

            var fireCharge = Math.Min(currentCharge, BaseFireCost);
            
            ToFireCharge += fireCharge;
            PowerCell = (currentCharge - fireCharge, maxCharge);
            return true;
        }

        protected override void Shoot(int shotCount, List<Angle> spreads)
        {
            DebugTools.Assert(ToFireCharge > 0);
            
            var shooter = Shooter();
            CameraRecoilComponent? cameraRecoilComponent = null;
            shooter?.TryGetComponent(out cameraRecoilComponent);

            for (var i = 0; i < shotCount; i++)
            {
                var fireCharge = Math.Min(BaseFireCost, ToFireCharge);
                ToFireCharge -= fireCharge;

                cameraRecoilComponent?.Kick(-spreads[i].ToVec().Normalized * RecoilMultiplier * fireCharge / BaseFireCost);

                if (SoundGunshot == null)
                    continue;
                
                // TODO: Could look at modifying volume based on charge %
                EntitySystem.Get<AudioSystem>().Play(SoundGunshot, Owner, AudioHelpers.WithVariation(GunshotVariation));
                // TODO: Show effect here once we can get the spread predicted.
            }

            UpdateAppearance();
            _statusControl?.Update();
        }

        public override Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public override bool UseEntity(UseEntityEventArgs eventArgs)
        {
            throw new NotImplementedException();
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
            private readonly ClientBatteryBarrelComponent _parent;
            private readonly HBoxContainer _bulletsList;
            private readonly Label _noBatteryLabel;
            private readonly Label _ammoCount;

            public StatusControl(ClientBatteryBarrelComponent parent)
            {
                _parent = parent;
                SizeFlagsHorizontal = SizeFlags.FillExpand;
                SizeFlagsVertical = SizeFlags.ShrinkCenter;

                AddChild(new HBoxContainer
                {
                    SizeFlagsHorizontal = SizeFlags.FillExpand,
                    Children =
                    {
                        new Control
                        {
                            SizeFlagsHorizontal = SizeFlags.FillExpand,
                            Children =
                            {
                                (_bulletsList = new HBoxContainer
                                {
                                    SizeFlagsVertical = SizeFlags.ShrinkCenter,
                                    SeparationOverride = 4
                                }),
                                (_noBatteryLabel = new Label
                                {
                                    Text = "No Battery!",
                                    StyleClasses = {StyleNano.StyleClassItemStatus}
                                })
                            }
                        },
                        new Control() { CustomMinimumSize = (5,0) },
                        (_ammoCount = new Label
                        {
                            StyleClasses = {StyleNano.StyleClassItemStatus},
                            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
                        }),
                    }
                });
            }

            public void Update()
            {
                _bulletsList.RemoveAllChildren();

                if (_parent.PowerCell == null)
                {
                    _noBatteryLabel.Visible = true;
                    _ammoCount.Visible = false;
                    return;
                }

                var (count, capacity) = ((int) (_parent.PowerCell.Value.CurrentCharge / _parent.BaseFireCost), (int) (_parent.PowerCell.Value.MaxCharge / _parent.BaseFireCost));

                _noBatteryLabel.Visible = false;
                _ammoCount.Visible = true;

                _ammoCount.Text = $"x{count:00}";
                capacity = Math.Min(capacity, 8);
                FillBulletRow(_bulletsList, count, capacity);
            }

            private static void FillBulletRow(Control container, int count, int capacity)
            {
                var colorGone = Color.FromHex("#000000");
                var color = Color.FromHex("#E00000");

                // Draw the empty ones
                for (var i = count; i < capacity; i++)
                {
                    container.AddChild(new PanelContainer
                    {
                        PanelOverride = new StyleBoxFlat()
                        {
                            BackgroundColor = colorGone,
                        },
                        CustomMinimumSize = (10, 15),
                    });
                }

                // Draw the full ones, but limit the count to the capacity
                count = Math.Min(count, capacity);
                for (var i = 0; i < count; i++)
                {
                    container.AddChild(new PanelContainer
                    {
                        PanelOverride = new StyleBoxFlat()
                        {
                            BackgroundColor = color,
                        },
                        CustomMinimumSize = (10, 15),
                    });
                }
            }

            protected override Vector2 CalculateMinimumSize()
            {
                return Vector2.ComponentMax((0, 15), base.CalculateMinimumSize());
            }
        }
    }
}
