using Content.Shared.Atmos.Monitor.Components;
using Content.Shared.Interaction;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Wires;

namespace Content.Shared.Atmos.Monitor.Systems;

public abstract class SharedAirAlarmSystem : EntitySystem
{
    [Dependency] private    readonly SharedPowerReceiverSystem _receiver = default!;
    [Dependency] protected  readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirAlarmComponent, ActivateInWorldEvent>(OnActivate);
    }

    protected void OnActivate(EntityUid uid, AirAlarmComponent component, ActivateInWorldEvent args)
    {
        if (!args.Complex)
            return;

        if (TryComp<WiresPanelComponent>(uid, out var panel) && panel.Open)
        {
            args.Handled = false;
            return;
        }

        if (!_receiver.IsPowered(uid))
            return;

        _ui.OpenUi(uid, SharedAirAlarmInterfaceKey.Key, args.User);
        AddActiveInterface(uid);
        SyncAllDevices(uid);
        SharedUpdateUI(uid, component);
    }

    protected virtual void AddActiveInterface(EntityUid uid)
    {
    }

    protected virtual void SyncAllDevices(EntityUid uid)
    {
    }

    protected virtual void SharedUpdateUI(EntityUid uid, AirAlarmComponent? alarm = null)
    {

    }
}
