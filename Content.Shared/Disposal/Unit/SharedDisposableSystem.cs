using Content.Server.Disposal.Unit;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Tube;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Shared.Disposal.Unit;

public abstract class SharedDisposableSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedAtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDisposalUnitSystem _disposalUnitSystem = default!;
    [Dependency] private readonly SharedDisposalTubeSystem _disposalTubeSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<ItemComponent> _itemQuery;
    private EntityQuery<DisposalTubeComponent> _disposalTubeQuery;
    private EntityQuery<DisposalUnitComponent> _disposalUnitQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _disposalTubeQuery = GetEntityQuery<DisposalTubeComponent>();
        _disposalUnitQuery = GetEntityQuery<DisposalUnitComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _bodyQuery = GetEntityQuery<BodyComponent>();
        _itemQuery = GetEntityQuery<ItemComponent>();
        SubscribeLocalEvent<DisposalHolderComponent, ContainerIsInsertingAttemptEvent>(OnHolderCanInsert);
        SubscribeLocalEvent<DisposalHolderComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnHolderCanInsert(Entity<DisposalHolderComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (!_itemQuery.HasComp(args.EntityUid) && !_bodyQuery.HasComp(args.EntityUid))
        {
            args.Cancel();
        }
    }

    private void OnComponentStartup(EntityUid uid, DisposalHolderComponent holder, ComponentStartup args)
    {
        holder.Container = _containerSystem.EnsureContainer<Container>(uid, nameof(DisposalHolderComponent));
    }

    public bool TryInsert(EntityUid uid, EntityUid toInsert, DisposalHolderComponent? holder = null)
    {
        if (!Resolve(uid, ref holder))
            return false;

        if (!_containerSystem.Insert(toInsert, holder.Container))
            return false;

        return true;
    }

    public void ExitDisposals(EntityUid uid, DisposalHolderComponent? holder = null, TransformComponent? holderTransform = null)
    {
        DebugTools.Assert(!TerminatingOrDeleted(uid));

        if (!Resolve(uid, ref holder, ref holderTransform))
            return;

        if (holder.CurrentTube != null)
        {
            _disposalUnitSystem.TryEjectContents(holder.CurrentTube.Value, duc);
        }

        if (_atmosphereSystem.GetContainingMixture(uid, false, true) is { } environment)
        {
            _atmosphereSystem.Merge(environment, holder.Air);
            holder.Air.Clear();
        }

        Del(uid);
    }

    public bool EnterTube(EntityUid holderUid, EntityUid toUid, DisposalHolderComponent? holder = null, TransformComponent? holderTransform = null, DisposalTubeComponent? to = null, TransformComponent? toTransform = null)
    {
        if (!Resolve(holderUid, ref holder, ref holderTransform))
            return false;

        if (!Resolve(toUid, ref to, ref toTransform))
        {
            ExitDisposals(holderUid, holder, holderTransform);
            return false;
        }

        foreach (var ent in holder.Container.ContainedEntities)
        {
            var comp = EnsureComp<BeingDisposedComponent>(ent);
            comp.Holder = holderUid;
            Dirty(ent, comp);
        }

        // Insert into next tube
        if (!_containerSystem.Insert(holderUid, to.Contents))
        {
            ExitDisposals(holderUid, holder, holderTransform);
            return false;
        }

        if (holder.CurrentTube != null)
        {
            holder.PreviousTube = holder.CurrentTube;
            holder.PreviousDirection = holder.CurrentDirection;
        }

        holder.CurrentTube = toUid;
        var ev = new GetDisposalsNextDirectionEvent(holder);
        RaiseLocalEvent(toUid, ref ev);
        holder.CurrentDirection = ev.Next;
        holder.StartingTime = 0.1f;
        holder.TimeLeft = 0.1f;
        // Logger.InfoS("c.s.disposal.holder", $"Disposals dir {holder.CurrentDirection}");

        // Invalid direction = exit now!
        if (holder.CurrentDirection == Direction.Invalid)
        {
            ExitDisposals(holderUid, holder, holderTransform);
            return false;
        }

        // damage entities on turns and play sound
        if (holder.CurrentDirection != holder.PreviousDirection)
        {
            foreach (var ent in holder.Container.ContainedEntities)
            {
                _damageable.TryChangeDamage(ent, to.DamageOnTurn);
            }
            _audio.PlayPvs(to.ClangSound, toUid);
        }

        return true;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<DisposalHolderComponent>();
        while (query.MoveNext(out var uid, out var holder))
        {
            UpdateComp(uid, holder, frameTime);
        }
    }

    private void UpdateComp(EntityUid uid, DisposalHolderComponent holder, float frameTime)
    {
        while (frameTime > 0)
        {
            var time = frameTime;
            if (time > holder.TimeLeft)
            {
                time = holder.TimeLeft;
            }

            holder.TimeLeft -= time;
            frameTime -= time;

            if (!EntityManager.EntityExists(holder.CurrentTube))
            {
                ExitDisposals(uid, holder);
                break;
            }

            var currentTube = holder.CurrentTube!.Value;
            if (holder.TimeLeft > 0)
            {
                var progress = 1 - holder.TimeLeft / holder.StartingTime;
                var origin = _xformQuery.GetComponent(currentTube).Coordinates;
                var destination = holder.CurrentDirection.ToVec();
                var newPosition = destination * progress;

                // This is some supreme shit code.
                _xformSystem.SetCoordinates(uid, _xformSystem.WithEntityId(origin.Offset(newPosition), currentTube));
                continue;
            }

            // Past this point, we are performing inter-tube transfer!
            // Remove current tube content
            _containerSystem.Remove(uid, _disposalTubeQuery.GetComponent(currentTube).Contents, reparent: false, force: true);

            // Find next tube
            var nextTube = _disposalTubeSystem.NextTubeFor(currentTube, holder.CurrentDirection);
            if (!EntityManager.EntityExists(nextTube))
            {
                ExitDisposals(uid, holder);
                break;
            }

            // Perform remainder of entry process
            if (!EnterTube(uid, nextTube!.Value, holder))
            {
                break;
            }
        }
    }
}
