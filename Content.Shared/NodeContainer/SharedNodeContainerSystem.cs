using Content.Shared.Examine;
using Content.Shared.NodeContainer.NodeGroups;

namespace Content.Shared.NodeContainer;

public abstract class SharedNodeContainerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NodeContainerComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(EntityUid uid, NodeContainerComponent component, ExaminedEvent args)
    {
        if (!component.Examinable || !args.IsInDetailsRange)
            return;

        foreach (var node in component.Nodes.Values)
        {
            switch (node.NodeGroupID)
            {
                case NodeGroupID.HVPower:
                    args.PushMarkup(
                        Loc.GetString("node-container-component-on-examine-details-hvpower"));
                    break;
                case NodeGroupID.MVPower:
                    args.PushMarkup(
                        Loc.GetString("node-container-component-on-examine-details-mvpower"));
                    break;
                case NodeGroupID.Apc:
                    args.PushMarkup(
                        Loc.GetString("node-container-component-on-examine-details-apc"));
                    break;
            }
        }
    }
}
