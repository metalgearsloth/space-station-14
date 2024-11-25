using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Content.Shared.Administration.Logs;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.Doors;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using WiresComponent = Content.Shared.Wires.Components.WiresComponent;

namespace Content.Shared.Wires;

public abstract partial class SharedWiresSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager ProtoMan = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] private   readonly ActivatableUISystem _activatableUI = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;

    [Dependency] private   readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private   readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private   readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] protected readonly SharedToolSystem Tool = default!;
    [Dependency] private   readonly SharedUserInterfaceSystem _uiSystem = default!;

    protected float _toolTime = 0f;

    public override void Initialize()
    {
        base.Initialize();

        // this is a broadcast event
        SubscribeLocalEvent<WiresComponent, PanelChangedEvent>(OnPanelChanged);
        SubscribeLocalEvent<WiresComponent, DoorBoltChangedEvent>(OnBoltChanged);
        SubscribeLocalEvent<WiresComponent, WiresActionMessage>(OnWiresActionMessage);
        SubscribeLocalEvent<WiresComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<WiresComponent, TimedWireEvent>(OnTimedWire);
        SubscribeLocalEvent<WiresComponent, PowerChangedEvent>(OnWiresPowered);
        SubscribeLocalEvent<WiresComponent, WireDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<WiresPanelSecurityComponent, WiresPanelSecurityEvent>(SetWiresPanelSecurity);

        InitializePanel();
    }

    public bool CanWireAct(Entity<ToolComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return false;

        return Tool.HasQuality(ent.Owner, SharedToolSystem.CutQuality, tool: ent.Comp) ||
               Tool.HasQuality(ent.Owner, SharedToolSystem.PulseQuality, tool: ent.Comp);
    }

    private void OnBoltChanged(Entity<WiresComponent> ent, ref DoorBoltChangedEvent args)
    {
        UpdateUserInterface(ent.Owner, ent.Comp);
    }

    #region DoAfters
    private void OnTimedWire(EntityUid uid, WiresComponent component, TimedWireEvent args)
    {
        args.Delegate(args.Wire);
        UpdateUserInterface(uid);
    }

    /// <summary>
    ///     Tries to cancel an active wire action via the given key that it's stored in.
    /// </summary>
    /// <param name="key">The key used to cancel the action.</param>
    public bool TryCancelWireAction(EntityUid owner, object key)
    {
        if (TryGetData<CancellationTokenSource?>(owner, key, out var token))
        {
            token.Cancel();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Starts a timed action for this entity.
    /// </summary>
    /// <param name="delay">How long this takes to finish</param>
    /// <param name="key">The key used to cancel the action</param>
    /// <param name="onFinish">The event that is sent out when the wire is finished <see cref="TimedWireEvent" /></param>
    public void StartWireAction(EntityUid owner, float delay, object key, TimedWireEvent onFinish)
    {
        if (!HasComp<WiresComponent>(owner))
        {
            return;
        }

        if (!_activeWires.ContainsKey(owner))
        {
            _activeWires.Add(owner, new());
        }

        CancellationTokenSource tokenSource = new();

        // Starting an already started action will do nothing.
        if (HasData(owner, key))
        {
            return;
        }

        SetData(owner, key, tokenSource);

        _activeWires[owner].Add(new ActiveWireAction
        (
            key,
            delay,
            tokenSource.Token,
            onFinish
        ));
    }

    private Dictionary<EntityUid, List<ActiveWireAction>> _activeWires = new();
    private List<(EntityUid, ActiveWireAction)> _finishedWires = new();

    public override void Update(float frameTime)
    {
        foreach (var (owner, activeWires) in _activeWires)
        {
            if (!HasComp<WiresComponent>(owner))
                _activeWires.Remove(owner);

            foreach (var wire in activeWires)
            {
                if (wire.CancelToken.IsCancellationRequested)
                {
                    RaiseLocalEvent(owner, wire.OnFinish, true);
                    _finishedWires.Add((owner, wire));
                }
                else
                {
                    wire.TimeLeft -= frameTime;
                    if (wire.TimeLeft <= 0)
                    {
                        RaiseLocalEvent(owner, wire.OnFinish, true);
                        _finishedWires.Add((owner, wire));
                    }
                }
            }
        }

        if (_finishedWires.Count != 0)
        {
            foreach (var (owner, wireAction) in _finishedWires)
            {
                if (!_activeWires.TryGetValue(owner, out var activeWire))
                {
                    continue;
                }

                activeWire.RemoveAll(action => action.CancelToken == wireAction.CancelToken);

                if (activeWire.Count == 0)
                {
                    _activeWires.Remove(owner);
                }

                RemoveData(owner, wireAction.Id);
            }

            _finishedWires.Clear();
        }
    }

    private sealed class ActiveWireAction
    {
        /// <summary>
        ///     The wire action's ID. This is so that once the action is finished,
        ///     any related data can be removed from the state dictionary.
        /// </summary>
        public object Id;

        /// <summary>
        ///     How much time is left in this action before it finishes.
        /// </summary>
        public float TimeLeft;

        /// <summary>
        ///     The token used to cancel the action.
        /// </summary>
        public CancellationToken CancelToken;

        /// <summary>
        ///     The event called once the action finishes.
        /// </summary>
        public TimedWireEvent OnFinish;

        public ActiveWireAction(object identifier, float time, CancellationToken cancelToken, TimedWireEvent onFinish)
        {
            Id = identifier;
            TimeLeft = time;
            CancelToken = cancelToken;
            OnFinish = onFinish;
        }
    }

    #endregion

    #region Event Handling
    private void OnWiresPowered(EntityUid uid, WiresComponent component, ref PowerChangedEvent args)
    {
        UpdateUserInterface(uid);
        foreach (var wire in component.WiresList)
        {
            wire.Action?.Update(wire);
        }
    }

    private void OnWiresActionMessage(EntityUid uid, WiresComponent component, WiresActionMessage args)
    {
        var player = args.Actor;

        if (!EntityManager.TryGetComponent(player, out HandsComponent? handsComponent))
        {
            _popupSystem.PopupEntity(Loc.GetString("wires-component-ui-on-receive-message-no-hands"), uid, player);
            return;
        }

        if (!_interactionSystem.InRangeUnobstructed(player, uid))
        {
            _popupSystem.PopupEntity(Loc.GetString("wires-component-ui-on-receive-message-cannot-reach"), uid, player);
            return;
        }

        var activeHand = handsComponent.ActiveHand;

        if (activeHand == null)
            return;

        if (activeHand.HeldEntity == null)
            return;

        var activeHandEntity = activeHand.HeldEntity.Value;
        if (!EntityManager.TryGetComponent(activeHandEntity, out ToolComponent? tool))
            return;

        TryDoWireAction(uid, player, activeHandEntity, args.Id, args.Action, component, tool);
    }

    private void OnDoAfter(EntityUid uid, WiresComponent component, WireDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            component.WiresQueue.Remove(args.Id);
            return;
        }

        if (args.Handled || args.Args.Target == null || args.Args.Used == null)
            return;

        UpdateWires(args.Args.Target.Value, args.Args.User, args.Args.Used.Value, args.Id, args.Action, component);

        args.Handled = true;
    }

    private void OnInteractUsing(EntityUid uid, WiresComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ToolComponent>(args.Used, out var tool))
            return;

        if (!IsPanelOpen(uid))
            return;

        if (Tool.HasQuality(args.Used, SharedToolSystem.CutQuality, tool) ||
            Tool.HasQuality(args.Used, SharedToolSystem.PulseQuality, tool))
        {
            if (TryComp(args.User, out ActorComponent? actor))
            {
                _uiSystem.OpenUi(uid, WiresUiKey.Key, actor.PlayerSession);
                args.Handled = true;
            }
        }
    }

    private void OnPanelChanged(Entity<WiresComponent> ent, ref PanelChangedEvent args)
    {
        if (args.Open)
            return;

        _uiSystem.CloseUi(ent.Owner, WiresUiKey.Key);
    }
    #endregion

    #region Entity API

    protected void GenerateSerialNumber(EntityUid uid, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        Span<char> data = stackalloc char[9];
        data[4] = '-';

        if (Random.Prob(0.01f))
        {
            for (var i = 0; i < 4; i++)
            {
                // Cyrillic Letters
                data[i] = (char) Random.Next(0x0410, 0x0430);
            }
        }
        else
        {
            for (var i = 0; i < 4; i++)
            {
                // Letters
                data[i] = (char) Random.Next(0x41, 0x5B);
            }
        }

        for (var i = 5; i < 9; i++)
        {
            // Digits
            data[i] = (char) Random.Next(0x30, 0x3A);
        }

        wires.SerialNumber = new string(data);
        UpdateUserInterface(uid);
    }

    protected void UpdateUserInterface(EntityUid uid, WiresComponent? wires = null, UserInterfaceComponent? ui = null)
    {
        if (!Resolve(uid, ref wires, ref ui, false)) // logging this means that we get a bunch of errors
            return;

        var clientList = new List<ClientWire>();
        foreach (var entry in wires.WiresList)
        {
            clientList.Add(new ClientWire(entry.Id, entry.IsCut, entry.Color,
                entry.Letter));

            var statusData = entry.Action?.GetStatusLightData(entry);
            if (statusData != null && entry.Action?.StatusKey != null)
            {
                wires.Statuses[entry.Action.StatusKey] = (entry.OriginalPosition, statusData);
            }
        }

        var statuses = new List<(int position, object key, object value)>();
        foreach (var (key, value) in wires.Statuses)
        {
            var valueCast = ((int position, StatusLightData? value)) value;
            statuses.Add((valueCast.position, key, valueCast.value!));
        }

        statuses.Sort((a, b) => a.position.CompareTo(b.position));

        _uiSystem.SetUiState((uid, ui), WiresUiKey.Key, new WiresBoundUserInterfaceState(
            clientList.ToArray(),
            statuses.Select(p => new StatusEntry(p.key, p.value)).ToArray(),
            Loc.GetString(wires.BoardName),
            wires.SerialNumber,
            wires.WireSeed));
    }

    /// <summary>
    ///     Tries to get a wire on this entity by its integer id.
    /// </summary>
    /// <returns>The wire if found, otherwise null</returns>
    public Wire? TryGetWire(EntityUid uid, int id, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return null;

        return id >= 0 && id < wires.WiresList.Count
            ? wires.WiresList[id]
            : null;
    }

    /// <summary>
    ///     Tries to get all the wires on this entity by the wire action type.
    /// </summary>
    /// <returns>Enumerator of all wires in this entity according to the given type.</returns>
    public IEnumerable<Wire> TryGetWires<T>(EntityUid uid, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            yield break;

        foreach (var wire in wires.WiresList)
        {
            if (wire.GetType() == typeof(T))
            {
                yield return wire;
            }
        }
    }

    public void SetWiresPanelSecurity(EntityUid uid, WiresPanelSecurityComponent component, WiresPanelSecurityEvent args)
    {
        component.Examine = args.Examine;
        component.WiresAccessible = args.WiresAccessible;

        Dirty(uid, component);

        if (!args.WiresAccessible)
        {
            _uiSystem.CloseUi(uid, WiresUiKey.Key);
        }
    }

    private void TryDoWireAction(EntityUid target, EntityUid user, EntityUid toolEntity, int id, WiresAction action, WiresComponent? wires = null, ToolComponent? tool = null)
    {
        if (!Resolve(target, ref wires)
            || !Resolve(toolEntity, ref tool))
            return;

        if (wires.WiresQueue.Contains(id))
            return;

        var wire = TryGetWire(target, id, wires);

        if (wire == null)
            return;

        switch (action)
        {
            case WiresAction.Cut:
                if (!Tool.HasQuality(toolEntity, SharedToolSystem.CutQuality, tool))
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-need-wirecutters"), user);
                    return;
                }

                if (wire.IsCut)
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-cut-cut-wire"), user);
                    return;
                }

                break;
            case WiresAction.Mend:
                if (!Tool.HasQuality(toolEntity, SharedToolSystem.CutQuality, tool))
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-need-wirecutters"), user);
                    return;
                }

                if (!wire.IsCut)
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-mend-uncut-wire"), user);
                    return;
                }

                break;
            case WiresAction.Pulse:
                if (!Tool.HasQuality(toolEntity, SharedToolSystem.PulseQuality, tool))
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-need-multitool"), user);
                    return;
                }

                if (wire.IsCut)
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-pulse-cut-wire"), user);
                    return;
                }

                break;
        }

        wires.WiresQueue.Add(id);

        if (_toolTime > 0f)
        {
            var args = new DoAfterArgs(EntityManager, user, _toolTime, new WireDoAfterEvent(action, id), target, target: target, used: toolEntity)
            {
                NeedHand = true,
                BreakOnDamage = true,
                BreakOnMove = true
            };

            _doAfter.TryStartDoAfter(args);
        }
        else
        {
            UpdateWires(target, user, toolEntity, id, action, wires);
        }
    }

    private void UpdateWires(EntityUid used, EntityUid user, EntityUid toolEntity, int id, WiresAction action, WiresComponent? wires = null, ToolComponent? tool = null)
    {
        if (!Resolve(used, ref wires))
            return;

        if (!wires.WiresQueue.Contains(id))
            return;

        if (!Resolve(toolEntity, ref tool))
        {
            wires.WiresQueue.Remove(id);
            return;
        }

        var wire = TryGetWire(used, id, wires);

        if (wire == null)
        {
            wires.WiresQueue.Remove(id);
            return;
        }

        switch (action)
        {
            case WiresAction.Cut:
                if (!Tool.HasQuality(toolEntity, "Cutting", tool))
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-need-wirecutters"), user);
                    break;
                }

                if (wire.IsCut)
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-cut-cut-wire"), user);
                    break;
                }

                Tool.PlayToolSound(toolEntity, tool, null);
                if (wire.Action == null || wire.Action.Cut(user, wire))
                {
                    wire.IsCut = true;
                }

                UpdateUserInterface(used);
                break;
            case WiresAction.Mend:
                if (!Tool.HasQuality(toolEntity, "Cutting", tool))
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-need-wirecutters"), user);
                    break;
                }

                if (!wire.IsCut)
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-mend-uncut-wire"), user);
                    break;
                }

                Tool.PlayToolSound(toolEntity, tool, null);
                if (wire.Action == null || wire.Action.Mend(user, wire))
                {
                    wire.IsCut = false;
                }

                UpdateUserInterface(used);
                break;
            case WiresAction.Pulse:
                if (!Tool.HasQuality(toolEntity, "Pulsing", tool))
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-need-multitool"), user);
                    break;
                }

                if (wire.IsCut)
                {
                    _popupSystem.PopupCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-pulse-cut-wire"), user);
                    break;
                }

                wire.Action?.Pulse(user, wire);

                UpdateUserInterface(used);
                Audio.PlayPvs(wires.PulseSound, used);
                break;
        }

        wire.Action?.Update(wire);
        wires.WiresQueue.Remove(id);
    }

    /// <summary>
    ///     Tries to get the stateful data stored in this entity's WiresComponent.
    /// </summary>
    /// <param name="identifier">The key that stores the data in the WiresComponent.</param>
    public bool TryGetData<T>(EntityUid uid, object identifier, [NotNullWhen(true)] out T? data, WiresComponent? wires = null)
    {
        data = default(T);
        if (!Resolve(uid, ref wires))
            return false;

        wires.StateData.TryGetValue(identifier, out var result);

        if (result is not T)
        {
            return false;
        }

        data = (T) result;

        return true;
    }

    /// <summary>
    ///     Sets data in the entity's WiresComponent state dictionary by key.
    /// </summary>
    /// <param name="identifier">The key that stores the data in the WiresComponent.</param>
    /// <param name="data">The data to store using the given identifier.</param>
    public void SetData(EntityUid uid, object identifier, object data, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        if (wires.StateData.TryGetValue(identifier, out var storedMessage))
        {
            if (storedMessage == data)
            {
                return;
            }
        }

        wires.StateData[identifier] = data;
        UpdateUserInterface(uid, wires);
    }

    /// <summary>
    ///     If this entity has data stored via this key in the WiresComponent it has
    /// </summary>
    public bool HasData(EntityUid uid, object identifier, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return false;

        return wires.StateData.ContainsKey(identifier);
    }

    /// <summary>
    ///     Removes data from this entity stored in the given key from the entity's WiresComponent.
    /// </summary>
    /// <param name="identifier">The key that stores the data in the WiresComponent.</param>
    public void RemoveData(EntityUid uid, object identifier, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        wires.StateData.Remove(identifier);
    }
    #endregion
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class Wire
{
    /// <summary>
    /// Whether the wire is cut.
    /// </summary>
    [DataField]
    public bool IsCut;

    /// <summary>
    /// Used in client-server communication to identify a wire without telling the client what the wire does.
    /// </summary>
    [DataField]
    public int Id;

    /// <summary>
    /// The original position of this wire in the prototype.
    /// </summary>
    [DataField]
    public int OriginalPosition;

    /// <summary>
    /// The color of the wire.
    /// </summary>
    [DataField]
    public WireColor Color;

    /// <summary>
    /// The greek letter shown below the wire.
    /// </summary>
    [DataField]
    public WireLetter Letter;

    /// <summary>
    ///     The action that this wire performs when mended, cut or puled. This also determines the status lights that this wire adds.
    /// </summary>
    // NOOP on client.
    [field: NonSerialized]
    public IWireAction? Action { get; set; }

    public Wire(bool isCut, WireColor color, WireLetter letter, int position, IWireAction? action)
    {
        IsCut = isCut;
        Color = color;
        OriginalPosition = position;
        Letter = letter;
        Action = action;
    }
}

// this is here so that when a DoAfter event is called,
// WiresSystem can call the action in question after the
// doafter is finished (either through cancellation
// or completion - this is implementation dependent)
public delegate void WireActionDelegate(Wire wire);

// callbacks over the event bus,
// because async is banned
public sealed class TimedWireEvent : EntityEventArgs
{
    /// <summary>
    ///     The function to be called once
    ///     the timed event is complete.
    /// </summary>
    public WireActionDelegate Delegate { get; }

    /// <summary>
    ///     The wire tied to this timed wire event.
    /// </summary>
    public Wire Wire { get; }

    public TimedWireEvent(WireActionDelegate @delegate, Wire wire)
    {
        Delegate = @delegate;
        Wire = wire;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class WireLayout
{
    // why is this an <int, WireData>?
    // List<T>.Insert panics,
    // and I needed a uniquer key for wires
    // which allows me to have a unified identifier
    [DataField]
    public Dictionary<int, WireData> Specifications;

    public WireLayout(Dictionary<int, WireData> specifications)
    {
        Specifications = specifications;
    }

    [DataRecord, Serializable, NetSerializable]
    public readonly record struct WireData(WireLetter Letter, WireColor Color, int Position)
    {
        public readonly WireLetter Letter = Letter;
        public readonly WireColor Color = Color;
        public readonly int Position = Position;
    }
}
