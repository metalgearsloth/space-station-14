using Content.Shared.Crayon;

namespace Content.Server.Crayon;

public sealed class CrayonSystem : SharedCrayonSystem
{
    protected override void UseUpCrayon(EntityUid uid, EntityUid user)
    {
        base.UseUpCrayon(uid, user);
        EntityManager.QueueDeleteEntity(uid);
    }
}
