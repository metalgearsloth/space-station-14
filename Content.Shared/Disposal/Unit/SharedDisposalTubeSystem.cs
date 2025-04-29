using System.Linq;
using System.Text;
using Content.Server.Disposal.Tube;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Mailing;
using Content.Shared.Disposal.Tube;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Shared.Disposal.Unit;

public abstract class SharedDisposalTubeSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedAtmosphereSystem _atmosSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedDisposableSystem _disposableSystem = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DisposalTubeComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<DisposalTubeComponent, ComponentStartup>(OnStartup);

        SubscribeLocalEvent<DisposalTubeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DisposalTubeComponent, AnchorStateChangedEvent>(OnAnchorChange);

        SubscribeLocalEvent<DisposalBendComponent, GetDisposalsConnectableDirectionsEvent>(OnGetBendConnectableDirections);
        SubscribeLocalEvent<DisposalBendComponent, GetDisposalsNextDirectionEvent>(OnGetBendNextDirection);

        SubscribeLocalEvent<DisposalEntryComponent, GetDisposalsConnectableDirectionsEvent>(OnGetEntryConnectableDirections);
        SubscribeLocalEvent<DisposalEntryComponent, GetDisposalsNextDirectionEvent>(OnGetEntryNextDirection);

        SubscribeLocalEvent<DisposalJunctionComponent, GetDisposalsConnectableDirectionsEvent>(OnGetJunctionConnectableDirections);
        SubscribeLocalEvent<DisposalJunctionComponent, GetDisposalsNextDirectionEvent>(OnGetJunctionNextDirection);

        SubscribeLocalEvent<DisposalRouterComponent, GetDisposalsConnectableDirectionsEvent>(OnGetRouterConnectableDirections);
        SubscribeLocalEvent<DisposalRouterComponent, GetDisposalsNextDirectionEvent>(OnGetRouterNextDirection);

        SubscribeLocalEvent<DisposalTransitComponent, GetDisposalsConnectableDirectionsEvent>(OnGetTransitConnectableDirections);
        SubscribeLocalEvent<DisposalTransitComponent, GetDisposalsNextDirectionEvent>(OnGetTransitNextDirection);

        SubscribeLocalEvent<DisposalTaggerComponent, GetDisposalsConnectableDirectionsEvent>(OnGetTaggerConnectableDirections);
        SubscribeLocalEvent<DisposalTaggerComponent, GetDisposalsNextDirectionEvent>(OnGetTaggerNextDirection);

        Subs.BuiEvents<DisposalRouterComponent>(DisposalRouterUiKey.Key, subs =>
        {
            subs.Event<DisposalRouterUiActionMessage>(OnUiAction);
        });

        Subs.BuiEvents<DisposalTaggerComponent>(DisposalTaggerUiKey.Key, subs =>
        {
            subs.Event<DisposalTaggerUiActionMessage>(OnUiAction);
        });
    }

    private void OnComponentInit(EntityUid uid, DisposalTubeComponent tube, ComponentInit args)
    {
        tube.Contents = _containerSystem.EnsureContainer<Container>(uid, tube.ContainerId);
    }

    private void OnAnchorChange(EntityUid uid, DisposalTubeComponent component, ref AnchorStateChangedEvent args)
    {
        UpdateAnchored(uid, component, args.Anchored);
    }

    public void DisconnectTube(EntityUid _, DisposalTubeComponent tube)
    {
        var query = GetEntityQuery<DisposalHolderComponent>();
        foreach (var entity in tube.Contents.ContainedEntities.ToArray())
        {
            if (query.TryGetComponent(entity, out var holder))
                _disposableSystem.ExitDisposals(entity, holder);
        }
    }

    private void UpdateAnchored(EntityUid uid, DisposalTubeComponent component, bool anchored)
    {
        if (anchored)
        {
            // TODO this visual data should just generalized into some anchored-visuals system/comp, this has nothing to do with disposal tubes.
            _appearanceSystem.SetData(uid, DisposalTubeVisuals.VisualState, DisposalTubeVisualState.Anchored);
        }
        else
        {
            DisconnectTube(uid, component);
            _appearanceSystem.SetData(uid, DisposalTubeVisuals.VisualState, DisposalTubeVisualState.Free);
        }
    }

    public EntityUid? NextTubeFor(EntityUid target, Direction nextDirection, DisposalTubeComponent? targetTube = null)
    {
        if (!Resolve(target, ref targetTube))
            return null;
        var oppositeDirection = nextDirection.GetOpposite();

        var xform = Transform(target);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        var position = xform.Coordinates;
        foreach (var entity in _map.GetInDir(xform.GridUid.Value, grid, position, nextDirection))
        {
            if (!TryComp(entity, out DisposalTubeComponent? tube))
            {
                continue;
            }

            if (!CanConnect(entity, tube, oppositeDirection))
            {
                continue;
            }

            if (!CanConnect(target, targetTube, nextDirection))
            {
                continue;
            }

            return entity;
        }

        return null;
    }

    public bool CanConnect(EntityUid tubeId, DisposalTubeComponent tube, Direction direction)
    {
        if (!Transform(tubeId).Anchored)
        {
            return false;
        }

        var ev = new GetDisposalsConnectableDirectionsEvent();
        RaiseLocalEvent(tubeId, ref ev);
        return ev.Connectable.Contains(direction);
    }

    public void PopupDirections(EntityUid tubeId, DisposalTubeComponent _, EntityUid recipient)
    {
        var ev = new GetDisposalsConnectableDirectionsEvent();
        RaiseLocalEvent(tubeId, ref ev);
        var directions = string.Join(", ", ev.Connectable);

        _popups.PopupEntity(Loc.GetString("disposal-tube-component-popup-directions-text", ("directions", directions)), tubeId, recipient);
    }

    public bool TryInsert(EntityUid uid, DisposalUnitComponent from, IEnumerable<string>? tags = default, DisposalEntryComponent? entry = null)
    {
        if (!Resolve(uid, ref entry))
            return false;

        var xform = Transform(uid);
        var holder = Spawn(entry.HolderPrototypeId, _transform.GetMapCoordinates(uid, xform: xform));
        var holderComponent = Comp<DisposalHolderComponent>(holder);

        foreach (var entity in from.Container.ContainedEntities.ToArray())
        {
            _disposableSystem.TryInsert(holder, entity, holderComponent);
        }

        _atmosSystem.Merge(holderComponent.Air, from.Air);
        from.Air.Clear();

        if (tags != null)
            holderComponent.Tags.UnionWith(tags);

        return _disposableSystem.EnterTube(holder, uid, holderComponent);
    }

    private void OnGetBendConnectableDirections(EntityUid uid, DisposalBendComponent component, ref GetDisposalsConnectableDirectionsEvent args)
    {
        var direction = Transform(uid).LocalRotation;
        var side = new Angle(MathHelper.DegreesToRadians(direction.Degrees - 90));

        args.Connectable = new[] { direction.GetDir(), side.GetDir() };
    }

    private void OnGetBendNextDirection(EntityUid uid, DisposalBendComponent component, ref GetDisposalsNextDirectionEvent args)
    {
        var ev = new GetDisposalsConnectableDirectionsEvent();
        RaiseLocalEvent(uid, ref ev);

        var previousDF = args.Holder.PreviousDirectionFrom;

        if (previousDF == Direction.Invalid)
        {
            args.Next = ev.Connectable[0];
            return;
        }

        args.Next = previousDF == ev.Connectable[0] ? ev.Connectable[1] : ev.Connectable[0];
    }

    private void OnGetEntryConnectableDirections(EntityUid uid, DisposalEntryComponent component, ref GetDisposalsConnectableDirectionsEvent args)
    {
        args.Connectable = new[] { Transform(uid).LocalRotation.GetDir() };
    }

    private void OnGetEntryNextDirection(EntityUid uid, Shared.Disposal.Tube.DisposalEntryComponent component, ref GetDisposalsNextDirectionEvent args)
    {
        // Ejects contents when they come from the same direction the entry is facing.
        if (args.Holder.PreviousDirectionFrom != Direction.Invalid)
        {
            args.Next = Direction.Invalid;
            return;
        }

        var ev = new GetDisposalsConnectableDirectionsEvent();
        RaiseLocalEvent(uid, ref ev);
        args.Next = ev.Connectable[0];
    }

    private void OnGetJunctionConnectableDirections(EntityUid uid, Shared.Disposal.Tube.DisposalJunctionComponent component, ref GetDisposalsConnectableDirectionsEvent args)
    {
        var direction = Transform(uid).LocalRotation;

        args.Connectable = component.Degrees
            .Select(degree => new Angle(degree.Theta + direction.Theta).GetDir())
            .ToArray();
    }

    private void OnGetJunctionNextDirection(EntityUid uid, Shared.Disposal.Tube.DisposalJunctionComponent component, ref GetDisposalsNextDirectionEvent args)
    {
        var next = Transform(uid).LocalRotation.GetDir();
        var ev = new GetDisposalsConnectableDirectionsEvent();
        RaiseLocalEvent(uid, ref ev);
        var directions = ev.Connectable.Skip(1).ToArray();

        if (args.Holder.PreviousDirectionFrom == Direction.Invalid ||
            args.Holder.PreviousDirectionFrom == next)
        {
            args.Next = _random.Pick(directions);
            return;
        }

        args.Next = next;
    }

    private void OnGetRouterConnectableDirections(EntityUid uid, DisposalRouterComponent component, ref GetDisposalsConnectableDirectionsEvent args)
    {
        OnGetJunctionConnectableDirections(uid, component, ref args);
    }

    private void OnGetRouterNextDirection(EntityUid uid, DisposalRouterComponent component, ref GetDisposalsNextDirectionEvent args)
    {
        var ev = new GetDisposalsConnectableDirectionsEvent();
        RaiseLocalEvent(uid, ref ev);

        if (args.Holder.Tags.Overlaps(component.Tags))
        {
            args.Next = ev.Connectable[1];
            return;
        }

        args.Next = Transform(uid).LocalRotation.GetDir();
    }

    private void OnGetTransitConnectableDirections(EntityUid uid, DisposalTransitComponent component, ref GetDisposalsConnectableDirectionsEvent args)
    {
        var rotation = Transform(uid).LocalRotation;
        var opposite = new Angle(rotation.Theta + Math.PI);

        args.Connectable = new[] { rotation.GetDir(), opposite.GetDir() };
    }

    private void OnGetTransitNextDirection(EntityUid uid, DisposalTransitComponent component, ref GetDisposalsNextDirectionEvent args)
    {
        var ev = new GetDisposalsConnectableDirectionsEvent();
        RaiseLocalEvent(uid, ref ev);
        var previousDF = args.Holder.PreviousDirectionFrom;
        var forward = ev.Connectable[0];

        if (previousDF == Direction.Invalid)
        {
            args.Next = forward;
            return;
        }

        var backward = ev.Connectable[1];
        args.Next = previousDF == forward ? backward : forward;
    }

    /// <summary>
    /// Handles ui messages from the client. For things such as button presses
    /// which interact with the world and require server action.
    /// </summary>
    /// <param name="msg">A user interface message from the client.</param>
    private void OnUiAction(EntityUid uid, DisposalTaggerComponent tagger, DisposalTaggerUiActionMessage msg)
    {
        if (TryComp<PhysicsComponent>(uid, out var physBody) && physBody.BodyType != BodyType.Static)
            return;

        //Check for correct message and ignore maleformed strings
        if (msg.Action == DisposalTaggerUiAction.Ok && DisposalTaggerComponent.TagRegex.IsMatch(msg.Tag))
        {
            tagger.Tag = msg.Tag.Trim();
            _audio.PlayPredicted(tagger.ClickSound, uid, msg.Actor, AudioParams.Default.WithVolume(-2f));
        }
    }


    /// <summary>
    /// Handles ui messages from the client. For things such as button presses
    /// which interact with the world and require server action.
    /// </summary>
    /// <param name="msg">A user interface message from the client.</param>
    private void OnUiAction(EntityUid uid, DisposalRouterComponent router, DisposalRouterUiActionMessage msg)
    {
        if (TryComp<PhysicsComponent>(uid, out var physBody) && physBody.BodyType != BodyType.Static)
            return;

        //Check for correct message and ignore maleformed strings
        if (msg.Action == DisposalRouterUiAction.Ok && DisposalRouterComponent.TagRegex.IsMatch(msg.Tags))
        {
            router.Tags.Clear();
            foreach (var tag in msg.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = tag.Trim();
                if (trimmed == "")
                    continue;

                router.Tags.Add(trimmed);
            }

            _audio.PlayPredicted(router.ClickSound, uid, msg.Actor, AudioParams.Default.WithVolume(-2f));
        }
    }

    private void OnGetTaggerConnectableDirections(EntityUid uid, DisposalTaggerComponent component, ref GetDisposalsConnectableDirectionsEvent args)
    {
        OnGetTransitConnectableDirections(uid, component, ref args);
    }

    private void OnGetTaggerNextDirection(EntityUid uid, DisposalTaggerComponent component, ref GetDisposalsNextDirectionEvent args)
    {
        args.Holder.Tags.Add(component.Tag);
        OnGetTransitNextDirection(uid, component, ref args);
    }

    private void OnStartup(EntityUid uid, DisposalTubeComponent component, ComponentStartup args)
    {
        UpdateAnchored(uid, component, Transform(uid).Anchored);
    }

    private void OnBreak(EntityUid uid, DisposalTubeComponent component, BreakageEventArgs args)
    {
        DisconnectTube(uid, component);
    }
}
