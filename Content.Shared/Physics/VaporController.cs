﻿using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.Shared.Physics
{
    public class VaporController : VirtualController
    {
        public void Move(Vector2 velocityDirection, float speed)
        {
            Impulse = velocityDirection * 500 * speed;
        }
    }
}
