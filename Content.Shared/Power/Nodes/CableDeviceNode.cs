using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.Nodes;
using Robust.Shared.Map.Components;

namespace Content.Shared.Power.Nodes
{
    /// <summary>
    ///     Type of node that connects to a <see cref="Shared.Power.Nodes.CableNode"/> below it.
    /// </summary>
    [DataDefinition]
    [Virtual]
    public partial class CableDeviceNode : Node
    {
        /// <summary>
        /// If disabled, this cable device will never connect.
        /// </summary>
        /// <remarks>
        /// If you change this,
        /// you must manually call <see cref="NodeGroupSystem.QueueReflood"/> to update the node connections.
        /// </remarks>
        [DataField]
        public bool Enabled = true;

        public override bool Connectable(IEntityManager entMan, TransformComponent? xform = null)
        {
            if (!Enabled)
                return false;

            return base.Connectable(entMan, xform);
        }

        public override IEnumerable<Node> GetReachableNodes(TransformComponent xform,
            EntityQuery<NodeContainerComponent> nodeQuery,
            EntityQuery<TransformComponent> xformQuery,
            MapGridComponent? grid,
            IEntityManager entMan)
        {
            if (!xform.Anchored || grid == null)
                yield break;

            var gridIndex = grid.TileIndicesFor(xform.Coordinates);

            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, gridIndex))
            {
                if (node is CableNode)
                    yield return node;
            }
        }
    }
}
