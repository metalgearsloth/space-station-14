using System.Threading;
using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Projectiles.Components;
using Content.Server.Singularity.Components;
using Content.Server.Storage.Components;
using Content.Shared.Audio;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Singularity.Components;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Singularity.EntitySystems
{
    [UsedImplicitly]
    public sealed class EmitterSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly PopupSystem _popup = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<EmitterComponent, PowerConsumerReceivedChanged>(ReceivedChanged);
            SubscribeLocalEvent<EmitterComponent, InteractHandEvent>(OnInteractHand);
            SubscribeLocalEvent<EmitterComponent, AnchorStateChangedEvent>(OnAnchorChange);
        }

        private void OnAnchorChange(EntityUid uid, EmitterComponent component, ref AnchorStateChangedEvent args)
        {
            if (!args.Anchored)
            {
                UpdateAppearance(component, EmitterVisualState.Off);
            }
        }

        private void OnInteractHand(EntityUid uid, EmitterComponent component, InteractHandEvent args)
        {
            args.Handled = true;
            if (EntityManager.TryGetComponent(uid, out LockComponent? lockComp) && lockComp.Locked)
            {
                component.Owner.PopupMessage(args.User, Loc.GetString("comp-emitter-access-locked", ("target", component.Owner)));
                return;
            }

            if (EntityManager.TryGetComponent(component.Owner, out PhysicsComponent? phys) && phys.BodyType == BodyType.Static)
            {
                if (!component.IsOn)
                {
                    SwitchOn(component);
                    component.Owner.PopupMessage(args.User, Loc.GetString("comp-emitter-turned-on", ("target", component.Owner)));
                }
                else
                {
                    SwitchOff(component);
                    component.Owner.PopupMessage(args.User, Loc.GetString("comp-emitter-turned-off", ("target", component.Owner)));
                }

                _adminLogger.Add(LogType.Emitter,
                    component.IsOn ? LogImpact.Medium : LogImpact.High,
                    $"{ToPrettyString(args.User):player} toggled {ToPrettyString(uid):emitter}");
            }
            else
            {
                component.Owner.PopupMessage(args.User, Loc.GetString("comp-emitter-not-anchored", ("target", component.Owner)));
            }
        }

        private void ReceivedChanged(
            EntityUid uid,
            EmitterComponent component,
            PowerConsumerReceivedChanged args)
        {
            if (!component.IsOn)
            {
                return;
            }

            if (args.ReceivedPower < args.DrawRate)
            {
                PowerOff(component);
            }
            else
            {
                PowerOn(component);
            }
        }

        public void SwitchOff(EmitterComponent component)
        {
            component.IsOn = false;
            if (TryComp<PowerConsumerComponent>(component.Owner, out var powerConsumer)) powerConsumer.DrawRate = 0;
            PowerOff(component);
            UpdateAppearance(component, EmitterVisualState.Off);
        }

        public void SwitchOn(EmitterComponent component)
        {
            component.IsOn = true;
            if (TryComp<PowerConsumerComponent>(component.Owner, out var powerConsumer)) powerConsumer.DrawRate = component.PowerUseActive;
            // Do not directly PowerOn().
            // OnReceivedPowerChanged will get fired due to DrawRate change which will turn it on.
            UpdateAppearance(component, EmitterVisualState.On);
        }

        public void PowerOff(EmitterComponent component)
        {
            UpdateAppearance(component, EmitterVisualState.Underpowered);
        }

        public void PowerOn(EmitterComponent component)
        {
            UpdateAppearance(component, EmitterVisualState.On);
        }

        private void UpdateAppearance(EmitterComponent component, EmitterVisualState state)
        {
            if (!TryComp<AppearanceComponent>(component.Owner, out var appearanceComponent))
                return;

            appearanceComponent.SetData(EmitterVisuals.VisualState, state);
        }
    }
}
