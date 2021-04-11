#nullable enable
using System;
using Content.Client.Utility;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.ViewVariables;
using Content.Client.GameObjects.Components.Mobs;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.GameObjects.EntitySystems.ActionBlocker;
using Content.Shared.GameObjects.Verbs;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    [ComponentReference(typeof(SharedRevolverBarrelComponent))]
    public class ClientRevolverBarrelComponent : SharedRevolverBarrelComponent, IItemStatus
    {
        private StatusControl? _statusControl;

        /// <summary>
        /// A array that lists the bullet states
        /// true means a spent bullet
        /// false means a "shootable" bullet
        /// null means no bullet
        /// </summary>
        [ViewVariables]
        public bool?[] Ammo { get; private set; } = default!;

        public override void Initialize()
        {
            base.Initialize();
            Ammo = new bool?[Capacity];

            // Mark every bullet as unspent
            if (FillPrototype != null)
            {
                for (var i = 0; i < Ammo.Length; i++)
                {
                    Ammo[i] = true;
                    UnspawnedCount--;
                }
            }
        }

        protected override void EjectAllSlots()
        {
            for (var i = 0; i < Ammo.Length; i++)
            {
                var slot = Ammo[i];

                if (slot == null)
                    continue;

                // TODO: Play SOUND. Once we have prediction and know what the ammo component is.

                Ammo[i] = null;
            }

            _statusControl?.Update();
            return;
        }

        public override bool TryInsertBullet(IEntity user, SharedAmmoComponent ammoComponent)
        {
            // TODO
            return true;
        }

        private void Spin()
        {
            CurrentSlot = IoCManager.Resolve<IRobustRandom>().Next(Ammo.Length - 1);
            _statusControl?.Update(true);
            SendNetworkMessage(new RevolverSpinMessage(CurrentSlot));

            if (SoundSpin != null)
                SoundSystem.Play(Filter.Local(), SoundSpin, Owner, AudioHelpers.WithVariation(SpinVariation).WithVolume(SpinVolume));
        }

        // Item status etc.
        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (curState is not RevolverBarrelComponentState cast)
                return;

            CurrentSlot = cast.CurrentSlot;
            Ammo = cast.Bullets;

            if (nextState == null)
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
                MinHeight = 15;
                _parent = parent;
                HorizontalExpand = true;
                VerticalAlignment = VAlignment.Center;
                AddChild((_bulletsList = new HBoxContainer
                {
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Center,
                    SeparationOverride = 0
                }));
            }

            public void Update(bool hideMarker = false)
            {
                _bulletsList.RemoveAllChildren();

                var capacity = _parent.Ammo.Length;

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

                FillBulletRow(_bulletsList, texture, spentTexture, hideMarker);
            }

            private void FillBulletRow(Control container, Texture texture, Texture emptyTexture, bool hideMarker)
            {
                var colorA = Color.FromHex("#b68f0e");
                var colorB = Color.FromHex("#d7df60");
                var colorSpentA = Color.FromHex("#b50e25");
                var colorSpentB = Color.FromHex("#d3745f");
                var colorGoneA = Color.FromHex("#000000");
                var colorGoneB = Color.FromHex("#222222");

                var altColor = false;
                const float scale = 1.3f;

                for (var i = 0; i < _parent.Ammo.Length; i++)
                {
                    var bulletSpent = !_parent.Ammo[i];
                    // Add a outline
                    var box = new Control
                    {
                        MinSize = texture.Size * scale,
                    };
                    if (i == _parent.CurrentSlot && !hideMarker)
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

                    if (bulletSpent != null)
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
                        Stretch = TextureRect.StretchMode.KeepCentered,
                        Texture = bulletTexture,
                        ModulateSelfOverride = color,
                    });
                    altColor ^= true;
                    container.AddChild(box);
                }
            }
        }

        [Verb]
        private sealed class SpinRevolverVerb : Verb<ClientRevolverBarrelComponent>
        {
            protected override void GetData(IEntity user, ClientRevolverBarrelComponent component, VerbData data)
            {
                if (!ActionBlockerSystem.CanInteract(user))
                {
                    data.Visibility = VerbVisibility.Invisible;
                    return;
                }

                data.Text = Loc.GetString("Spin");
                if (component.Capacity <= 1)
                {
                    data.Visibility = VerbVisibility.Invisible;
                    return;
                }

                data.Visibility = VerbVisibility.Visible;
            }

            protected override void Activate(IEntity user, ClientRevolverBarrelComponent component)
            {
                component.Spin();
            }
        }
    }
}
