using System.Numerics;
using Content.Client.Eye;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Movement.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Eye;

[NonParallelizable]
public sealed class ShuttleEyeInterpolationTest : GameTest
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task EyeRotationFollowsRenderedShuttle(bool hasMover, bool turning)
    {
        await Client.WaitAssertion(() =>
        {
            var transforms = CEntMan.System<TransformSystem>();
            var maps = CEntMan.System<SharedMapSystem>();
            var eyes = CEntMan.System<EyeLerpingSystem>();
            var oldTick = CGameTiming.CurTick;
            var oldRemainder = CGameTiming.TickRemainder;
            var oldInSimulation = CGameTiming.InSimulation;
            EntityUid? map = null;
            try
            {
                // Spawn at the authoritative tick so this is not treated as predicted movement.
                CGameTiming.CurTick = CGameTiming.LastRealTick;
                map = maps.CreateMap(out var mapId);
                var grid = maps.CreateGridEntity(mapId);
                maps.SetTile(grid, Vector2i.Zero, new Tile(1));
                var uid = CEntMan.SpawnAttachedTo(null, new EntityCoordinates(grid, new Vector2(0.5f)));
                Assert.That(CEntMan.GetComponent<TransformComponent>(uid).GridUid, Is.EqualTo(grid.Owner));
                var eye = CEntMan.AddComponent<EyeComponent>(uid);
                if (hasMover)
                {
                    var mover = CEntMan.AddComponent<InputMoverComponent>(uid);
                    mover.RelativeEntity = grid;
                    mover.RelativeRotation = Angle.FromDegrees(15);
                    mover.TargetRelativeRotation = mover.RelativeRotation;
                }

                eyes.AddEye(uid, eye);
                transforms.SnapRenderTransform(grid, true);
                using (CGameTiming.StartStateApplicationArea())
                    transforms.SetLocalRotation(grid, Angle.FromDegrees(30));

                if (turning)
                {
                    var mover = CEntMan.GetComponent<InputMoverComponent>(uid);
                    mover.RelativeRotation = Angle.FromDegrees(25);
                    mover.TargetRelativeRotation = Angle.FromDegrees(45);
                }

                eyes.Update((float) CGameTiming.TickPeriod.TotalSeconds);
                CGameTiming.InSimulation = false;
                var relativeRotation = hasMover ? Angle.FromDegrees(15) : Angle.Zero;
                foreach (var phase in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                {
                    CGameTiming.TickRemainder = CGameTiming.CalcAdjustedTickPeriod() * phase / CGameTiming.TimeScale;
                    transforms.FrameUpdate(0f);
                    eyes.FrameUpdate(0f);
                    var renderedRotation = transforms.GetRenderWorldRotation(grid.Owner);
                    Assert.That(renderedRotation.Degrees, Is.EqualTo(30 * phase).Within(0.001));
                    var inputRotation = relativeRotation + Angle.FromDegrees(turning ? 10 * phase : 0);
                    Assert.That(Angle.ShortestDistance(-renderedRotation - inputRotation, eye.Eye.Rotation).Theta,
                        Is.EqualTo(0).Within(0.0001),
                        $"The eye and the shuttle must use the same rotation at phase {phase}.");
                }

                if (turning)
                {
                    // Changing the movement reference adjusts the relative input angles
                    // to preserve world direction. Camera history must follow that rebase.
                    var nextGrid = maps.CreateGridEntity(mapId);
                    transforms.SetLocalRotation(nextGrid, Angle.FromDegrees(60));
                    transforms.SnapRenderTransform(nextGrid);
                    var mover = CEntMan.GetComponent<InputMoverComponent>(uid);
                    mover.RelativeEntity = nextGrid;
                    mover.RelativeRotation -= Angle.FromDegrees(30);
                    mover.TargetRelativeRotation -= Angle.FromDegrees(30);
                    CGameTiming.InSimulation = true;
                    eyes.Update((float) CGameTiming.TickPeriod.TotalSeconds);
                    CGameTiming.InSimulation = false;
                    foreach (var phase in new[] { 0f, 0.5f, 1f })
                    {
                        CGameTiming.TickRemainder = CGameTiming.CalcAdjustedTickPeriod() * phase / CGameTiming.TimeScale;
                        eyes.FrameUpdate(0f);
                        Assert.That(Angle.ShortestDistance(Angle.FromDegrees(-55), eye.Eye.Rotation).Theta,
                            Is.EqualTo(0).Within(0.0001), "Changing reference must preserve the camera's world angle.");
                    }
                }
            }
            finally
            {
                if (map != null)
                    CEntMan.DeleteEntity(map.Value);
                CGameTiming.CurTick = oldTick;
                CGameTiming.TickRemainder = oldRemainder;
                CGameTiming.InSimulation = oldInSimulation;
            }
        });
    }
}
