using Content.Shared.GameObjects.Components.DoAfter;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.DoAfter
{
    [ComponentReference(typeof(SharedNewDoAfterComponent))]
    internal sealed class NewDoAfterComponent : SharedNewDoAfterComponent
    {
        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (curState is not NewDoAfterComponentState state) return;

            foreach (var doAfter in state.DoAfters)
            {
                var reconciled = false;
                // Reconcile UserUid + TargetUid + TargetComponent with client comps
                // That is: Copy all of the server shit over to the existing client one
                // If a client one isn't found in networked then hold onto it I guess?

                foreach (var (existing, _) in _doAfters)
                {
                    if (existing.UserUid != doAfter.UserUid ||
                        existing.TargetUid != doAfter.TargetUid ||
                        existing.TargetComponent != doAfter.TargetComponent) continue;

                    // Reconcile!
                    reconciled = true;
                    Reconcile(existing, doAfter);
                }

                if (!reconciled)
                {
                    _doAfters.Add(doAfter, null);
                }
            }
        }

        private void Reconcile(NewDoAfter existingDoAfter, NewDoAfter doAfter)
        {
            existingDoAfter.Duration = doAfter.Duration;
            existingDoAfter.StartTime = doAfter.StartTime;
            existingDoAfter.MovementThreshold = doAfter.MovementThreshold;
        }
    }
}
