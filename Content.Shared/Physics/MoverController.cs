#nullable enable
using System;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.Shared.Physics
{
    public class MoverController : VirtualController
    {
        public override IPhysicsComponent? ControlledComponent { protected get; set; }

        private const float MaxChange = 0.01f;

        // Look I needed some kind of trickery so they'd slow down when you stopped pressing the button.
        // If you have a better solution be my guest.
        private bool _frictionModified = false;

        // Essentially do unique shit to make movement feel nicer.
        public void Push(Vector2 velocityDirection, float speed)
        {
            Force = Vector2.Zero;

            if (ControlledComponent == null)
                return;

            if (_frictionModified)
            {
                ControlledComponent.Friction /= 25.0f;
                _frictionModified = false;
            }

            var bodyVelocity = ControlledComponent.LinearVelocity;
            var deltaV = velocityDirection - bodyVelocity;
            var accel = deltaV * speed;

            // Just because I cbf tabbing anymore; body.LinearVelocity += body.Force * body.InvMass * deltaTime;
            // Thus mass gets cancelled out and so velocity += force * deltaTime
            var force = accel.Normalized * Math.Min(accel.Length, MaxChange) * ControlledComponent.Mass;

            Force = force;
        }

        public void StopMoving()
        {
            Force = Vector2.Zero;

            if (ControlledComponent == null)
                return;

            if (!_frictionModified)
            {
                ControlledComponent.Friction *= 25.0f;
                _frictionModified = true;
            }
        }
    }
}
