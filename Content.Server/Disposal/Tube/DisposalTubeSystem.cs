using Content.Server.Construction.Completions;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;

namespace Content.Server.Disposal.Tube;

public sealed class DisposalTubeSystem : SharedDisposalTubeSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DisposalTubeComponent, ConstructionBeforeDeleteEvent>(OnDeconstruct);
    }

    private void OnDeconstruct(EntityUid uid, DisposalTubeComponent component, ConstructionBeforeDeleteEvent args)
    {
        DisconnectTube(uid, component);
    }
}
