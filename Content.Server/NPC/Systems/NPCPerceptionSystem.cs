using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.MobState;
using Content.Shared.MobState.Components;
using Robust.Shared.Map;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Handles sight + sounds for NPCs.
/// </summary>
public sealed partial class NPCPerceptionSystem : EntitySystem
{
    [Dependency] private readonly FactionSystem _factions = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;

    /// <summary>
    /// Regardless of pathfinding or LOS these are the max we'll check
    /// </summary>
    private const int MaxConsideredTargets = 10;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateRecentlyInjected(frameTime);
    }

    public async Task<List<(EntityUid Entity, float Distance)>> GetHostileTargets(NPCBlackboard blackboard, EntityUid existingTarget, CancellationToken? cancelToken = null)
    {
        var mobQuery = GetEntityQuery<MobStateComponent>();
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var ownerCoordinates =
            blackboard.GetValueOrDefault<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, EntityManager);
        var xformQuery = GetEntityQuery<TransformComponent>();
        var targets = new List<(EntityUid Entity, float Distance)>();
        var paths = new List<Task>();
        var radius = blackboard.GetValueOrDefault<float>(NPCBlackboard.VisionRadius, EntityManager);
        bool canMove

        // TODO: Check if our old targets is up to date, otherwise a
        // TODO: Look at memory and check what needs updating.

        var count = 0;
        cancelToken ??= CancellationToken.None;

        foreach (var target in _factions
                     .GetNearbyHostiles(owner, radius))
        {
            if (mobQuery.TryGetComponent(target, out var mobState) &&
                mobState.CurrentState > DamageState.Alive ||
                target == existingTarget)
            {
                continue;
            }

            count++;

            if (count >= MaxConsideredTargets)
                break;

            paths.Add(UpdateTarget(owner, target, existingTarget, ownerCoordinates, blackboard, radius, canMove, xformQuery, targets, cancelToken));
        }

        await Task.WhenAll(paths);

        return targets;
    }

    private async Task UpdateTarget(
        EntityUid owner,
        EntityUid target,
        EntityUid existingTarget,
        EntityCoordinates ownerCoordinates,
        NPCBlackboard blackboard,
        float radius,
        bool canMove,
        EntityQuery<TransformComponent> xformQuery,
        List<(EntityUid Entity, float Distance)> targets,
        CancellationToken cancelToken)
    {
        if (!xformQuery.TryGetComponent(target, out var targetXform))
            return;

        var inLos = false;

        // If it's not an existing target then check LOS.
        if (target != existingTarget)
        {
            inLos = ExamineSystemShared.InRangeUnOccluded(owner, target, radius, null);

            if (!inLos)
                return;
        }

        // Turret or the likes, check LOS only.
        if (IsRanged && !canMove)
        {
            inLos = inLos || ExamineSystemShared.InRangeUnOccluded(owner, target, radius, null);

            if (!inLos || !targetXform.Coordinates.TryDistance(EntityManager, ownerCoordinates, out var distance))
                return;

            targets.Add((target, distance));
            return;
        }

        var nDistance = await _pathfinding.GetPathDistance(owner, targetXform.Coordinates,
            SharedInteractionSystem.InteractionRange, cancelToken, _pathfinding.GetFlags(blackboard));

        if (nDistance == null)
            return;

        targets.Add((target, nDistance.Value));
    }
}
