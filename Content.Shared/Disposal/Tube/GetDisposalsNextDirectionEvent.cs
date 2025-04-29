using Content.Server.Disposal.Unit;

namespace Content.Server.Disposal.Tube;

[ByRefEvent]
public record struct GetDisposalsNextDirectionEvent(Shared.Disposal.Unit.DisposalHolderComponent Holder)
{
    public Direction Next;
}
