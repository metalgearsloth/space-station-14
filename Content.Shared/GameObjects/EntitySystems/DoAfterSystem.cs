using System;
using System.Collections.Generic;
using Content.Shared.GameObjects.Components.DoAfter;
using Robust.Shared.GameObjects;

namespace Content.Shared.GameObjects.EntitySystems
{
    public abstract class SharedDoAfterSystem : EntitySystem
    {
        public const string DoAfterSawmill = "doafter";

        public void DoAfter(NewDoAfter doAfter, Action callback)
        {
            var user = EntityManager.GetEntity(doAfter.UserUid);
            var comp = GetComp();

            comp.Add(doAfter);
        }

        protected abstract SharedNewDoAfterComponent GetComp();

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var finished = new List<Action?>();

            foreach (var comp in ComponentManager.EntityQuery<SharedNewDoAfterComponent>())
            {
                foreach (var (doAfter, _) in comp.DoAfters)
                {
                    doAfter.Run();
                }

                var toRemove = new List<NewDoAfter>();

                foreach (var (doAfter, action) in comp.DoAfters)
                {
                    if (doAfter.Cancelled)
                    {
                        toRemove.Add(doAfter);
                        continue;
                    }

                    if (doAfter.Finished)
                    {
                        finished.Add(action);
                        toRemove.Add(doAfter);
                        continue;
                    }
                }

                foreach (var doAfter in toRemove)
                {
                    comp.Remove(doAfter);
                }
            }

            foreach (var callback in finished)
            {
                callback?.Invoke();
            }
        }
    }
}
