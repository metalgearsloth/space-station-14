using Content.Shared.Administration.Managers;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration.Systems;

/// <summary>
/// Allows you to clone entities via a verb and re-use for later.
/// </summary>
public abstract class SharedEntityCloningSystem : EntitySystem
{
    [Dependency] protected readonly ISharedAdminManager _adminManager = default!;

    [Serializable, NetSerializable]
    protected sealed class RequestCloneMessage : EntityEventArgs
    {
        public NetEntity Entity;
    }
}
