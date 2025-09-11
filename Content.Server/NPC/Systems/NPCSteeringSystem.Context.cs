using System.Linq;
using System.Numerics;
using Content.Server.Examine;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Climbing;
using Content.Shared.Interaction;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;
using ClimbingComponent = Content.Shared.Climbing.Components.ClimbingComponent;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCSteeringSystem
{
    private void ApplySeek(Span<float> interest, Vector2 direction, float weight)
    {
        if (weight == 0f || direction == Vector2.Zero)
            return;

        var directionAngle = (float)direction.ToAngle().Theta;

        for (var i = 0; i < InterestDirections; i++)
        {
            var angle = i * InterestRadians;
            var dot = MathF.Cos(directionAngle - angle);
            dot = (dot + 1f) * 0.5f;
            interest[i] = Math.Clamp(interest[i] + dot * weight, 0f, 1f);
        }
    }

    #region Seek

    /// <summary>
    /// Takes into account agent-specific context that may allow it to bypass a node which is not FreeSpace.
    /// </summary>
    private bool IsFreeSpace(
        EntityUid uid,
        NPCSteeringComponent steering,
        PathPoly node)
    {
        if (node.Data.IsFreeSpace)
        {
            return true;
        }
        // Handle the case where the node is a climb, we can climb, and we are climbing.
        else if ((node.Data.Flags & PathfindingBreadcrumbFlag.Climb) != 0x0 &&
            (steering.Flags & PathFlags.Climbing) != 0x0 &&
            TryComp<ClimbingComponent>(uid, out var climbing) &&
            climbing.IsClimbing)
        {
            return true;
        }

        // TODO: Ideally for "FreeSpace" we check all entities on the tile and build flags dynamically (pathfinder refactor in future).
        var ents = _entSetPool.Get();
        _lookup.GetLocalEntitiesIntersecting(node.GraphUid, node.Box.Enlarged(-0.04f), ents, flags: LookupFlags.Static);
        var result = true;

        if (ents.Count > 0)
        {
            var fixtures = _fixturesQuery.GetComponent(uid);
            var physics = _physicsQuery.GetComponent(uid);

            foreach (var intersecting in ents)
            {
                if (!_physics.IsCurrentlyHardCollidable((uid, fixtures, physics), intersecting))
                {
                    continue;
                }

                result = false;
                break;
            }
        }

        _entSetPool.Return(ents);
        return result;
    }

    /// <summary>
    /// Attempts to head to the target destination, either via the next pathfinding node or the final target.
    /// </summary>
    private bool TrySeek(
        EntityUid uid,
        NPCSteeringComponent steering,
        PhysicsComponent body,
        TransformComponent xform,
        Angle offsetRot,
        float moveSpeed,
        Span<float> interest,
        float frameTime,
        ref bool forceSteer)
    {
        var ourCoordinates = xform.Coordinates;
        var destinationCoordinates = steering.Coordinates;
        var inLos = true;

        // Check if we're in LOS if that's required.
        // TODO: Need something uhh better not sure on the interaction between these.
        if (!steering.ForceMove && steering.ArriveOnLineOfSight)
        {
            // TODO: use vision range
            inLos = _interaction.InRangeUnobstructed(uid, steering.Coordinates, 10f);

            if (inLos)
            {
                steering.LineOfSightTimer += frameTime;

                if (steering.LineOfSightTimer >= steering.LineOfSightTimeRequired)
                {
                    steering.Status = SteeringStatus.InRange;
                    ResetStuck(steering, ourCoordinates);
                    return true;
                }
            }
            else
            {
                steering.LineOfSightTimer = 0f;
            }
        }
        else
        {
            steering.LineOfSightTimer = 0f;
            steering.ForceMove = false;
        }

        // We've arrived, nothing else matters.
        if (xform.Coordinates.TryDistance(EntityManager, destinationCoordinates, out var targetDistance) &&
            inLos &&
            targetDistance <= steering.Range)
        {
            steering.Status = SteeringStatus.InRange;
            ResetStuck(steering, ourCoordinates);
            return true;
        }

        // Stop steering if we can't get there.
        if (!steering.Coordinates.IsValid(EntityManager))
        {
            steering.Status = SteeringStatus.NoPath;
            return false;
        }

        var nextNode = steering.NextNode;

        // If the next node is invalid then get new ones
        if (nextNode == null || !nextNode.IsValid())
        {
            ResetStuck(steering, ourCoordinates);
            RequestPath(uid, steering, xform);
            return false;
        }

        var targetMap = _transform.ToMapCoordinates(steering.Coordinates);
        var ourMap = _transform.ToMapCoordinates(ourCoordinates);
        var ourPoly = _pathfindingSystem.GetPoly(ourCoordinates);
        var nodeMap = _transform.ToMapCoordinates(nextNode.Coordinates);

        var nodeDistance = (nodeMap.Position - ourMap.Position).Length();

        DebugTools.Assert((targetMap.Position - ourMap.Position).Length() >= steering.Range);

        // Check if target moved from the target poly in which case get a new path.
        // Following existing path still is okay.
        if (steering.CurrentPath.Count < 2 ||
            !steering.CurrentPath[0].Equals(_pathfindingSystem.GetPoly(steering.Coordinates)))
        {
            ResetStuck(steering, ourCoordinates);
            RequestPath(uid, steering, xform);
        }

        // Need to be pretty close if it's just a node to make sure LOS for door bashes or the likes.
        bool arrived;

        // Check if we're beyond the next node regardless of whether we're obstacle handling it.
        if (steering.CurrentPath.Count > 2)
        {
            var firstNode = steering.CurrentPath[^1];
            var secondNode = steering.CurrentPath[^2];

            var firstPos = _transform.ToMapCoordinates(firstNode.Coordinates);
            var secondPos = _transform.ToMapCoordinates(secondNode.Coordinates);

            // If it's cross-map then this doesn't matter, likely a portal.
            if (firstPos.MapId == secondPos.MapId)
            {
                // If we're beyond the node or we're on it assume we've arrived.
                arrived = ourPoly?.Equals(secondNode) == true ||
                          _pathfindingSystem.IsBeyond(
                              ourMap.Position, firstPos.Position, secondPos.Position);
            }
            else
            {
                if (secondPos.MapId != ourMap.MapId)
                {
                    arrived = false;
                }
                else
                {
                    // Pop the node
                    arrived = true;
                }
            }

            // Handle the popped node.
            if (arrived)
            {
                steering.CurrentPath.RemoveAt(steering.CurrentPath.Count - 1);
                nextNode = steering.NextNode;

                // Should never happen.
                if (nextNode == null)
                {
                    Log.Error($"Error when popping path for {ToPrettyString(uid)}");
                    steering.Status = SteeringStatus.NoPath;
                    return false;
                }

                // Update map value
                nodeMap = _transform.ToMapCoordinates(nextNode.Coordinates);
            }
        }
        // If the next one is an obstacle then just get interaction range.
        else if (nextNode.Data.Flags != PathfindingBreadcrumbFlag.None)
        {
            // TODO: Dynamic bounds
            arrived = nodeDistance <= SharedInteractionSystem.InteractionRange - 0.05f + 0.35f;
        }
        // Try getting into blocked range I guess?
        // TODO: Consider melee range or the likes.
        else
        {
            arrived = nodeDistance <= 0.5f;
        }

        // Are we in range of the next node.
        // If so try handling any special obstacles.
        if (!arrived && nextNode.Data.Flags != PathfindingBreadcrumbFlag.None)
        {
            // Node needs some kind of special handling like access or smashing.
            if (!IsFreeSpace(uid, steering, nextNode))
            {
                // Ignore stuck while handling obstacles.
                Array.Clear(steering.Interest);
                ResetStuck(steering, ourCoordinates);
                SteeringObstacleStatus status;

                // Breaking behaviours and the likes.
                lock (_obstacles)
                {
                    // We're still coming to a stop so wait for the do_after.
                    if (body.LinearVelocity.LengthSquared() > 0.01f)
                    {
                        return true;
                    }

                    status = TryHandleFlags(uid, steering, nextNode);
                }

                // TODO: Need to handle re-pathing in case the target moves around.
                switch (status)
                {
                    case SteeringObstacleStatus.Completed:
                        steering.DoAfterId = null;
                        break;
                    case SteeringObstacleStatus.Failed:
                        steering.DoAfterId = null;
                        // TODO: Blacklist the poly for next query
                        steering.Status = SteeringStatus.NoPath;
                        return false;
                    case SteeringObstacleStatus.Continuing:
                        steering.Status = SteeringStatus.Moving;
                        ResetStuck(steering, ourCoordinates);
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Obstacle cleared so we can keep going.
            }
        }
        // Stuck detection
        // Check if we have moved further than the movespeed * stuck time.
        else if (AntiStuck &&
                 ourCoordinates.TryDistance(EntityManager, steering.LastStuckCoordinates, out var stuckDistance) &&
                 stuckDistance < NPCSteeringComponent.StuckDistance)
        {
            var stuckTime = _timing.CurTime - steering.LastStuckTime;
            // Either 1 second or how long it takes to move the stuck distance + buffer if we're REALLY slow.
            var maxStuckTime = Math.Max(1, NPCSteeringComponent.StuckDistance / (moveSpeed * 1.5f));

            if (stuckTime.TotalSeconds > maxStuckTime)
            {
                // TODO: Blacklist nodes (pathfinder factor wehn)
                // TODO: This should be a warning but
                // A) NPCs get stuck on non-anchored static bodies still (e.g. closets)
                // B) NPCs still try to move in locked containers (e.g. cow, hamster)
                // and I don't want to spam grafana even harder than it gets spammed rn.
                Log.Debug($"NPC {ToPrettyString(uid)} found stuck at {ourCoordinates}");
                RequestPath(uid, steering, xform);

                // Stop moving in the interim.
                Array.Clear(steering.Interest);

                if (stuckTime.TotalSeconds > maxStuckTime * 3)
                {
                    steering.Status = SteeringStatus.NoPath;
                    return false;
                }

                steering.Status = SteeringStatus.Moving;
                return true;
            }
        }
        else
        {
            ResetStuck(steering, ourCoordinates);
        }

        // If we don't have a path yet then do nothing; this is to avoid stutter-stepping if it turns out there's no path
        // available but we assume there was.
        if (steering is { Pathfind: true, CurrentPath.Count: 0 })
            return true;

        var direction = nodeMap.Position - ourMap.Position;

        if (moveSpeed == 0f || direction == Vector2.Zero)
        {
            steering.Status = SteeringStatus.NoPath;
            return false;
        }

        var input = direction.Normalized();
        var tickMovement = moveSpeed * frameTime;

        // We have the input in world terms but need to convert it back to what movercontroller is doing.
        input = offsetRot.RotateVec(input);
        var norm = input.Normalized();
        var weight = MapValue(direction.Length(), tickMovement * 0.5f, tickMovement * 0.75f);

        ApplySeek(interest, norm, weight);

        // Prefer our current direction
        if (weight > 0f && body.LinearVelocity.LengthSquared() > 0f)
        {
            const float sameDirectionWeight = 0.1f;
            norm = body.LinearVelocity.Normalized();

            ApplySeek(interest, norm, sameDirectionWeight);
        }

        return true;
    }

    private void ResetStuck(NPCSteeringComponent component, EntityCoordinates ourCoordinates)
    {
        component.LastStuckCoordinates = ourCoordinates;
        component.LastStuckTime = _timing.CurTime;
    }


    /// <summary>
    /// Gets the fraction this value is between min and max
    /// </summary>
    /// <returns></returns>
    private float MapValue(float value, float minValue, float maxValue)
    {
        if (maxValue > minValue)
        {
            var mapped = (value - minValue) / (maxValue - minValue);
            return Math.Clamp(mapped, 0f, 1f);
        }

        return value >= minValue ? 1f : 0f;
    }

    #endregion

    #region Static Avoidance

    /// <summary>
    /// Tries to avoid static blockers such as walls.
    /// </summary>
    private void CollisionAvoidance(
        EntityUid uid,
        Angle offsetRot,
        Vector2 worldPos,
        float agentRadius,
        int layer,
        int mask,
        TransformComponent xform,
        Span<float> danger)
    {
        var objectRadius = 0.05f;
        var detectionRadius = MathF.Max(0.35f, agentRadius + objectRadius);
        var ents = _entSetPool.Get();
        _lookup.GetEntitiesInRange(uid, detectionRadius, ents, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Approximate);

        foreach (var ent in ents)
        {
            // TODO: If we can access the door or smth.
            if (!_physicsQuery.TryGetComponent(ent, out var otherBody) ||
                !otherBody.Hard ||
                !otherBody.CanCollide ||
                otherBody.BodyType == BodyType.KinematicController ||
                (mask & otherBody.CollisionLayer) == 0x0 &&
                (layer & otherBody.CollisionMask) == 0x0)
            {
                continue;
            }

            var xformB = _xformQuery.GetComponent(ent);

            if (!_physics.TryGetNearest(uid, ent,
                    out var pointA, out var pointB, out var distance,
                    xform, xformB))
            {
                continue;
            }

            if (distance > detectionRadius)
                continue;

            var obstacleDirection = pointB - pointA;
            float adjustedDistance;

            // Inside each other so just use worldPos
            if (distance == 0f)
            {
                adjustedDistance = 1f;
                obstacleDirection = _transform.GetWorldPosition(xformB) - worldPos;
            }
            else
            {
                adjustedDistance = (detectionRadius - distance) / detectionRadius;
            }

            if (obstacleDirection == Vector2.Zero)
                continue;

            var weight = adjustedDistance * 0.1f;
            obstacleDirection = offsetRot.RotateVec(obstacleDirection);
            var norm = obstacleDirection.Normalized();

            for (var i = 0; i < InterestDirections; i++)
            {
                var dot = Vector2.Dot(norm, Directions[i]);
                danger[i] = MathF.Max(dot * weight, danger[i]);
            }
        }

        _entSetPool.Return(ents);
    }

    #endregion

    #region Dynamic Avoidance

    /// <summary>
    /// Tries to avoid mobs of the same faction.
    /// </summary>
    private void Separation(
        EntityUid uid,
        Angle offsetRot,
        Vector2 worldPos,
        float agentRadius,
        int layer,
        int mask,
        PhysicsComponent body,
        TransformComponent xform,
        Span<float> danger)
    {
        var objectRadius = 0.25f;
        var detectionRadius = MathF.Max(0.35f, agentRadius + objectRadius);
        var ourVelocity = body.LinearVelocity;
        _factionQuery.TryGetComponent(uid, out var ourFaction);
        var ents = _entSetPool.Get();
        _lookup.GetEntitiesInRange(uid, detectionRadius, ents, LookupFlags.Dynamic | LookupFlags.Approximate);

        foreach (var ent in ents)
        {
            // TODO: If we can access the door or smth.
            if (!_physicsQuery.TryGetComponent(ent, out var otherBody) ||
                !otherBody.Hard ||
                !otherBody.CanCollide ||
                (mask & otherBody.CollisionLayer) == 0x0 &&
                (layer & otherBody.CollisionMask) == 0x0 ||
                !_factionQuery.TryGetComponent(ent, out var otherFaction) ||
                !_npcFaction.IsEntityFriendly((uid, ourFaction), (ent, otherFaction)) ||
                // Use <= 0 so we ignore stationary friends in case.
                Vector2.Dot(otherBody.LinearVelocity, ourVelocity) <= 0f)
            {
                continue;
            }

            var xformB = _xformQuery.GetComponent(ent);

            if (!_physics.TryGetNearest(uid, ent, out var pointA, out var pointB, out var distance, xform, xformB))
            {
                continue;
            }

            if (distance > detectionRadius)
                continue;

            var weight = 1f;
            var obstacleDirection = pointB - pointA;

            // Inside each other so just use worldPos
            if (distance == 0f)
            {
                obstacleDirection = _transform.GetWorldPosition(xformB) - worldPos;

                // Welp
                if (obstacleDirection == Vector2.Zero)
                {
                    obstacleDirection = _random.NextAngle().ToVec();
                }
            }
            else
            {
                weight = distance / detectionRadius;
            }

            obstacleDirection = offsetRot.RotateVec(obstacleDirection);
            var norm = obstacleDirection.Normalized();
            weight *= 0.25f;

            for (var i = 0; i < InterestDirections; i++)
            {
                var dot = Vector2.Dot(norm, Directions[i]);
                danger[i] = MathF.Max(dot * weight, danger[i]);
            }
        }

        _entSetPool.Return(ents);
    }

    #endregion

    // TODO: Alignment

    // TODO: Cohesion
    private void Blend(NPCSteeringComponent steering, float frameTime, Span<float> interest, Span<float> danger)
    {
        /*
         * Future sloth notes:
         * Pathfinder cleanup:
            - Cleanup whatever the fuck is happening in pathfinder
            - Use Flee for melee behavior / actions and get the seek direction from that rather than bulldozing
            - Must always have a path
            - Path should return the full version + the snipped version
            - Pathfinder needs to do diagonals
            - Next node is either <current node + 1> or <nearest node + 1> (on the full path)
            - If greater than <1.5m distance> repath
         */

        // IDK why I didn't do this sooner but blending is a lot better than lastdir for fixing stuttering.
        const float BlendWeight = 10f;
        var blendValue = Math.Min(1f, frameTime * BlendWeight);

        for (var i = 0; i < InterestDirections; i++)
        {
            var currentInterest = interest[i];
            var lastInterest = steering.Interest[i];
            var interestDiff = (currentInterest - lastInterest) * blendValue;
            steering.Interest[i] = lastInterest + interestDiff;

            var currentDanger = danger[i];
            var lastDanger = steering.Danger[i];
            var dangerDiff = (currentDanger - lastDanger) * blendValue;
            steering.Danger[i] = lastDanger + dangerDiff;
        }
    }
}
