using System.Numerics;

namespace Content.Shared.NPC;

public abstract partial class SharedPathfindingSystem
{
    public bool IsBeyond(Vector2 position, Vector2 pointA, Vector2 pointB)
    {
        // See https://www.habrador.com/tutorials/math/2-passed-waypoint/

        //The vector between the character and the waypoint we are going from
        var a = position - pointA;

        //The vector between the waypoints
        var b = pointB - pointA;

        //Vector projection from https://en.wikipedia.org/wiki/Vector_projection
        //To know if we have passed the upcoming waypoint we need to find out how much of b is a1
        //a1 = (a.b / |b|^2) * b
        //a1 = progress * b -> progress = a1 / b -> progress = (a.b / |b|^2)
        float progress = (a.X * b.X + a.Y * b.Y) / (b.X * b.X + b.Y * b.Y);

        //If progress is above 1 we know we have passed the waypoint
        return progress > 1f;
    }
}
