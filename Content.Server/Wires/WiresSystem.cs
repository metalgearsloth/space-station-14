using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared.Wires;

namespace Content.Server.Wires;

public sealed class WiresSystem : SharedWiresSystem
{
    [Dependency] private readonly ConstructionSystem _construction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WiresComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, WiresComponent component, MapInitEvent args)
    {
        if (!string.IsNullOrEmpty(component.LayoutId))
            SetOrCreateWireLayout(uid, component);

        if (component.SerialNumber == null)
            GenerateSerialNumber(uid, component);

        if (component.WireSeed == 0)
            component.WireSeed = _random.Next(1, int.MaxValue);

        // Update the construction graph to make sure that it starts on the node specified by WiresPanelSecurityComponent
        if (TryComp<WiresPanelSecurityComponent>(uid, out var wiresPanelSecurity) &&
            !string.IsNullOrEmpty(wiresPanelSecurity.SecurityLevel) &&
            TryComp<ConstructionComponent>(uid, out var construction))
        {
            _construction.ChangeNode(uid, null, wiresPanelSecurity.SecurityLevel, true, construction);
        }

        UpdateUserInterface(uid);
    }

    protected void SetOrCreateWireLayout(EntityUid uid, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        WireLayout? layout = null;
        List<Wire>? wireSet = null;
        if (!wires.AlwaysRandomize)
        {
            TryGetLayout(wires.LayoutId, out layout);
        }

        List<IWireAction> wireActions = new();
        var dummyWires = 0;

        if (!_protoMan.TryIndex(wires.LayoutId, out WireLayoutPrototype? layoutPrototype))
        {
            return;
        }

        dummyWires += layoutPrototype.DummyWires;

        if (layoutPrototype.Wires != null)
        {
            wireActions.AddRange(layoutPrototype.Wires);
        }

        // does the prototype have a parent (and are the wires empty?) if so, we just create
        // a new layout based on that
        foreach (var parentLayout in _protoMan.EnumerateParents<WireLayoutPrototype>(wires.LayoutId))
        {
            if (parentLayout.Wires != null)
            {
                wireActions.AddRange(parentLayout.Wires);
            }

            dummyWires += parentLayout.DummyWires;
        }

        if (wireActions.Count > 0)
        {
            foreach (var wire in wireActions)
            {
                wire.Initialize();
            }

            wireSet = CreateWireSet(uid, layout, wireActions, dummyWires);
        }

        if (wireSet == null || wireSet.Count == 0)
        {
            return;
        }

        wires.WiresList.AddRange(wireSet);

        var types = new Dictionary<object, int>();

        if (layout != null)
        {
            for (var i = 0; i < wireSet.Count; i++)
            {
                wires.WiresList[layout.Specifications[i].Position] = wireSet[i];
            }

            var id = 0;
            foreach (var wire in wires.WiresList)
            {
                wire.Id = id++;
                if (wire.Action == null)
                    continue;

                var wireType = wire.Action.GetType();
                if (types.ContainsKey(wireType))
                {
                    types[wireType] += 1;
                }
                else
                {
                    types.Add(wireType, 1);
                }

                // don't care about the result, this should've
                // been handled in layout creation
                wire.Action.AddWire(wire, types[wireType]);
            }
        }
        else
        {
            var enumeratedList = new List<(int, Wire)>();
            var data = new Dictionary<int, WireLayout.WireData>();
            for (int i = 0; i < wireSet.Count; i++)
            {
                enumeratedList.Add((i, wireSet[i]));
            }
            _random.Shuffle(enumeratedList);

            for (var i = 0; i < enumeratedList.Count; i++)
            {
                (int id, Wire d) = enumeratedList[i];
                d.Id = i;

                if (d.Action != null)
                {
                    var actionType = d.Action.GetType();
                    if (!types.TryAdd(actionType, 1))
                        types[actionType] += 1;

                    if (!d.Action.AddWire(d, types[actionType]))
                        d.Action = null;
                }

                data.Add(id, new WireLayout.WireData(d.Letter, d.Color, i));
                wires.WiresList[i] = wireSet[id];
            }

            if (!wires.AlwaysRandomize && !string.IsNullOrEmpty(wires.LayoutId))
            {
                AddLayout(wires.LayoutId, new WireLayout(data));
            }
        }
    }
}
