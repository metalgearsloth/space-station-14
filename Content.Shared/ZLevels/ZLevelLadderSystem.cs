using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Content.Shared.ZLevels;

/// <summary>
/// Performs ladder traversal through the same authoritative portal operation used by ramps.
/// </summary>
public sealed partial class ZLevelLadderSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ZLevelPortalSystem _portals = default!;

    [Dependency] private EntityQuery<ZLevelPortalComponent> _portalQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZLevelLadderComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ZLevelLadderComponent, ZLevelLadderTraverseDoAfterEvent>(OnTraverseComplete);
    }

    private void OnInteractHand(Entity<ZLevelLadderComponent> ladder, ref InteractHandEvent args)
    {
        // Ladders are delayed portals: starting the DoAfter only reserves intent. The destination is validated again
        // on completion so newly blocked exits fail without moving the user.
        if (!_net.IsServer ||
            args.Handled ||
            !_portalQuery.TryComp(ladder.Owner, out var portal) ||
            !_portals.TryGetPairedEndpoint((ladder.Owner, portal), out _))
        {
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ladder.Comp.ClimbDelay,
            new ZLevelLadderTraverseDoAfterEvent(),
            ladder.Owner,
            target: ladder.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnTraverseComplete(
        Entity<ZLevelLadderComponent> ladder,
        ref ZLevelLadderTraverseDoAfterEvent args)
    {
        if (!_net.IsServer ||
            args.Cancelled ||
            !_portalQuery.TryComp(ladder.Owner, out var portal) ||
            !_portals.TryGetPairedEndpoint((ladder.Owner, portal), out _))
        {
            return;
        }

        args.Handled = _portals.TryTraverseZ(args.User, (ladder.Owner, portal), portal.DestinationOffset);
    }
}
