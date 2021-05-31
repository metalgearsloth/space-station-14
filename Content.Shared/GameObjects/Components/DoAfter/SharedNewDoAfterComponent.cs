using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Players;

namespace Content.Shared.GameObjects.Components.DoAfter
{
    public abstract class SharedNewDoAfterComponent : Component
    {
        public override string Name => "NewDoAfter";
        public override uint? NetID => ContentNetIDs.DO_AFTER;

        public IReadOnlyDictionary<NewDoAfter, Action?> DoAfters => _doAfters;

        protected Dictionary<NewDoAfter, Action?> _doAfters = new();

        public override ComponentState GetComponentState(ICommonSession player)
        {
            return new NewDoAfterComponentState(_doAfters.Keys.ToList());
        }

        public void Add(NewDoAfter doAfter, Action? callback = null)
        {
            if (_doAfters.ContainsKey(doAfter))
            {
                Logger.WarningS(SharedDoAfterSystem.DoAfterSawmill,
                    $"Tried to add do_after that's already on entity: {doAfter.UserUid}, {doAfter.TargetUid}, {doAfter.TargetComponent}");
                return;
            }

            _doAfters[doAfter] = callback;
            Dirty();
        }

        public void Remove(NewDoAfter doAfter)
        {
            if (!_doAfters.Remove(doAfter))
            {
                Logger.WarningS(SharedDoAfterSystem.DoAfterSawmill,
                    $"Tried to remove do_after that isn't on the entity: {doAfter.UserUid}, {doAfter.TargetUid}, {doAfter.TargetComponent}");
                return;
            }

            Dirty();
        }

        protected sealed class NewDoAfterComponentState : ComponentState
        {
            public List<NewDoAfter> DoAfters = new();

            public NewDoAfterComponentState(List<NewDoAfter> doAfters) : base(ContentNetIDs.DO_AFTER)
            {
                DoAfters = doAfters;
            }
        }
    }
}
