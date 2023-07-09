using System.Numerics;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Dynamics.Contacts;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Movement.Systems;

public sealed class MobPushingSystem : VirtualController
{
    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(SharedMoverController));
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);
        var query = EntityQueryEnumerator<MobPushingComponent, PhysicsComponent>();

        while (query.MoveNext(out var uid, out var pushing, out var physics))
        {
            if (physics.ContactCount == 0)
                continue;

            foreach (var contact in PhysicsSystem.GetContacts(uid, physics))
            {
                var otherEnt = contact.GetOtherEntity(uid);

                if (!HasComp<HumanoidAppearanceComponent>(otherEnt) ||
                    contact.FixtureA!.Hard ||
                    contact.FixtureB!.Hard)
                {
                    continue;
                }

                var xformA = Transform(uid);
                var xformB = Transform(otherEnt);
                var localNormal = (xformB.LocalPosition - xformA.LocalPosition).ToAngle().GetCardinalDir().ToAngle().ToVec();

                var otherVelocity = Comp<PhysicsComponent>(otherEnt).LinearVelocity;
                var dotProduct = Vector2.Dot(localNormal, otherVelocity);

                if (dotProduct > 0f)
                    continue;

                var newVelocity = otherVelocity * 0.5f;
                PhysicsSystem.SetLinearVelocity(otherEnt, newVelocity);
                var velocityReduction = otherVelocity - newVelocity;

                var offsetAmount = (velocityReduction / 2f * frameTime).Length();

                var ourPos = xformA.LocalPosition;
                // Also need to apply the pusher's speed otherwise they will slowly clip through
                var additionalOffset = newVelocity * frameTime;

                PhysicsSystem.WakeBody(uid, body: physics);
                TransformSystem.SetLocalPosition(uid, ourPos - localNormal * offsetAmount + additionalOffset);

                var otherPos = xformB.LocalPosition;
                TransformSystem.SetLocalPosition(otherEnt, otherPos + localNormal * offsetAmount);
            }
        }
    }
}
