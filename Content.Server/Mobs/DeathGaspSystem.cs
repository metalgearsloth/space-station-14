using Content.Server.Chat.Systems;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Mobs;

public sealed class DeathGaspSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    // Can move this to mobstate when we get proper shared chat
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeathGaspComponent, MobStateChangedEvent>(OnDeathState);
    }

    private void OnDeathState(EntityUid uid, DeathGaspComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        _chat.TryEmoteWithChat(uid, _prototype.Index<EmotePrototype>(component.Prototype), checkBlocker: false);
    }
}
