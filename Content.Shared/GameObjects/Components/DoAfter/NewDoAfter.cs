using System;
using Content.Shared.GameObjects.Components.Items;
using Content.Shared.GameObjects.Components.Mobs;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.GameObjects.Components.DoAfter
{
    [Serializable, NetSerializable]
    public sealed class NewDoAfter
    {
        public EntityUid UserUid { get; private set; }
        public EntityUid TargetUid { get; private set; }

        /// <summary>
        /// do_afters are unique when considering: user uid, target uid, and target component.
        /// </summary>
        public string TargetComponent { get; private set; }

        public bool BreakOnUserMove { get; }

        public bool BreakOnTargetMove { get; }

        /// <summary>
        /// How far the target is allowed to move; this is mainly to prevent rounding issues.
        /// </summary>
        public float MovementThreshold { get; } = 0.1f;

        public bool BreakOnDamage { get; }

        public bool BreakOnStun { get; }

        public bool NeedHand { get; }

        public TimeSpan StartTime { get; }

        /// <summary>
        /// How long the do_after goes for
        /// </summary>
        public float Duration { get; set; }

        public float TimeRemaining
        {
            get
            {
                var currentTime = IoCManager.Resolve<IGameTiming>().CurTime;
                return MathF.Max((float) (currentTime - StartTime).TotalSeconds - Duration, 0.0f);
            }
        }

        public EntityCoordinates UserGrid { get; }

        public EntityCoordinates TargetGrid { get; }

        public bool TookDamage { get; set; }

        // NeedHand
        private readonly string? _activeHand;
        private readonly EntityUid? _activeItem;

        [field: NonSerialized]
        public Func<bool>? ExtraCheck { get; set; }

        public bool Finished { get; set; }
        public bool ActionRun { get; set; }

        private bool _cancelled = false;

        public NewDoAfter(EntityUid userUid, EntityUid targetUid, string targetComponent)
        {
            UserUid = userUid;
            TargetUid = targetUid;
            TargetComponent = targetComponent;

            var entityManager = IoCManager.Resolve<IEntityManager>();

            StartTime = IoCManager.Resolve<IGameTiming>().CurTime;
            var user = entityManager.GetEntity(UserUid);
            var target = entityManager.GetEntity(TargetUid);

            if (BreakOnUserMove)
            {
                UserGrid = user.Transform.Coordinates;
            }

            if (BreakOnTargetMove)
            {
                // Target should never be null if the bool is set.
                TargetGrid = target.Transform.Coordinates;
            }

            // For this we need to stay on the same hand slot and need the same item in that hand slot
            // (or if there is no item there we need to keep it free).
            /* TODO
            if (NeedHand && user.TryGetComponent(out SharedHandsComponent? handsComponent))
            {
                _activeHand = handsComponent.ActiveHand;
                _activeItem = handsComponent.GetActiveHand;
            }
            */
        }

        public void Cancel()
        {
            _cancelled = true;
        }

        public void Run()
        {
            if (IsFinished())
            {
                return;
            }

            if (IsCancelled())
            {
                return;
            }
        }

        private bool IsCancelled()
        {
            if (_cancelled)
            {
                return true;
            }

            var entityManager = IoCManager.Resolve<IEntityManager>();

            var user = entityManager.GetEntity(UserUid);
            var target = entityManager.GetEntity(TargetUid);

            var compType = IoCManager.Resolve<IComponentFactory>().GetComponent(TargetComponent);
            var comp = IoCManager.Resolve<IComponentManager>().GetComponent(TargetUid, compType.GetType());

            if (comp.Deleted || user.Deleted || target.Deleted)
            {
                return true;
            }

            // TODO :Handle inertia in space.
            if (BreakOnUserMove && !user.Transform.Coordinates.InRange(
                entityManager, UserGrid, MovementThreshold))
            {
                return true;
            }

            if (BreakOnTargetMove && target.Transform.Coordinates.InRange(
                entityManager, TargetGrid, MovementThreshold))
            {
                return true;
            }

            if (BreakOnDamage && TookDamage)
            {
                return true;
            }

            if (ExtraCheck?.Invoke() == true)
            {
                return true;
            }

            if (BreakOnStun &&
                user.TryGetComponent(out SharedStunnableComponent? stunnableComponent) &&
                stunnableComponent.Stunned)
            {
                return true;
            }

            if (NeedHand)
            {
                if (!user.TryGetComponent(out SharedHandsComponent? handsComponent))
                {
                    // If we had a hand but no longer have it that's still a paddlin'
                    if (_activeHand != null)
                    {
                        return true;
                    }
                }
                else
                {
                    /* TODO
                    var currentActiveHand = handsComponent.ActiveHand;
                    if (_activeHand != currentActiveHand)
                    {
                        return true;
                    }

                    var currentItem = handsComponent.GetActiveHand;
                    if (_activeItem != currentItem)
                    {
                        return true;
                    }
                    */
                }
            }

            return false;
        }

        private bool IsFinished()
        {
            if (TimeRemaining > 0.0f)
            {
                return false;
            }

            return true;
        }
    }
}
