using Content.Server.NodeContainer.NodeGroups;

namespace Content.Shared.NodeContainer;

public abstract class SharedNodeGroupSystem : EntitySystem
{
    public virtual void QueueRemakeGroup(BaseNodeGroup group)
    {

    }
}
