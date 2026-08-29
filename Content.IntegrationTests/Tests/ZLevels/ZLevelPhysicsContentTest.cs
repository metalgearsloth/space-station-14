#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Animations;
using Content.Shared.CCVar;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Maps;
using Content.Shared.Throwing;
using Content.Shared.ZLevels;
using Robust.Client.GameObjects;
using Robust.Shared;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests.ZLevels;

[TestFixture]
[TestOf(typeof(ZLevelPhysicsContentSystem))]
public sealed class ZLevelPhysicsContentTest : InteractionTest
{
    private static readonly ProtoId<GameMapPrototype> DevMapId = "Dev";
    private const int ThrowComparisonSeed = 0x5A17;

    private sealed class LandListenerSystem : TestListenerSystem<LandEvent>;
    private readonly record struct ThrowVisualSample(
        int Tick,
        float TickFraction,
        Vector2 WorldPosition,
        Vector2 RenderedPosition,
        Vector2 GlobalRenderedPosition,
        Angle WorldRotation,
        Vector2 LinearVelocity,
        float RenderAbsoluteZ,
        float LocalZ,
        int RenderZLevel,
        int SourceZLevel,
        BodyStatus BodyStatus,
        bool Thrown,
        bool Disabled);

    [TestPrototypes]
    private const string ZLevelThrowTestPrototypes = @"
- type: entity
  id: ZLevelThrowTestHighGround
  components:
  - type: Transform
    anchored: true
  - type: ZLevelHighGround
    heightCurve: [1.05, 1.05]
";

    [SidedDependency(Side.Server)] private readonly ThrowingSystem _throwing = default!;
    [SidedDependency(Side.Server)] private readonly IRobustRandom _random = default!;

    [Test]
    public void DevMapUsesRandomOffsetAndRotation()
    {
        var devMap = SProtoMan.Index(DevMapId);

        Assert.Multiple(() =>
        {
            Assert.That(devMap.MaxRandomOffset, Is.GreaterThan(0f));
            Assert.That(devMap.RandomRotation, Is.True);
        });
    }

    [Test]
    public async Task CeilingImpactDoesNotRaiseLandEvent()
    {
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<TestListenerComponent>(SPlayer);
            SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
        });
        ClearEvents<LandEvent>(SPlayer);

        await Server.WaitPost(() =>
        {
            var ceiling = new ZLevelImpactEvent(5f, ZLevelImpactSurface.Ceiling);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref ceiling, true);
        });

        AssertEvent<LandEvent>(SPlayer, count: 0);

        await Server.WaitPost(() =>
        {
            var floor = new ZLevelImpactEvent(5f, ZLevelImpactSurface.Floor);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref floor, true);
        });

        var land = GetEvents<LandEvent>(SPlayer).Single();
        Assert.Multiple(() =>
        {
            Assert.That(land.User, Is.Null);
            Assert.That(land.PlaySound, Is.True);
        });
    }

    [Test]
    public async Task ThrownEntitiesDisableZPhysicsAndFloorImpactLandsOnce()
    {
        await AddGravity();

        EntityUid item = default;
        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            SEntMan.EnsureComponent<TestListenerComponent>(item);

            Assert.That(_throwing.TryThrow(
                item,
                new Vector2(4f, 0f),
                baseThrowSpeed: 4f,
                user: SPlayer,
                playSound: false), Is.True);

            var zPhysics = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            Assert.Multiple(() =>
            {
                Assert.That(zPhysics.Disabled, Is.True);
                Assert.That(zPhysics.Velocity, Is.Zero);
            });
        });

        ClearEvents<LandEvent>(item);

        await Server.WaitPost(() =>
        {
            var floor = new ZLevelImpactEvent(5f, ZLevelImpactSurface.Floor);
            SEntMan.EventBus.RaiseLocalEvent(item, ref floor, true);
            SEntMan.EventBus.RaiseLocalEvent(item, ref floor, true);
        });

        var events = GetEvents<LandEvent>(item).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].User, Is.EqualTo(SPlayer));
            Assert.That(events[0].PlaySound, Is.False);
        });
    }

    [Test]
    public async Task StopThrowReenablesZPhysics()
    {
        await AddGravity();

        EntityUid item = default;
        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            Assert.That(_throwing.TryThrow(
                item,
                new Vector2(4f, 0f),
                baseThrowSpeed: 4f,
                user: SPlayer,
                playSound: false), Is.True);

            Assert.That(SEntMan.GetComponent<ZLevelPhysicsComponent>(item).Disabled, Is.True);

            var thrown = SEntMan.GetComponent<ThrownItemComponent>(item);
            SEntMan.System<ThrownItemSystem>().StopThrow(item, thrown);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelPhysicsComponent>(item).Disabled, Is.False);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.False);
            });
        });
    }

    [Test]
    public async Task ThrownZPhysicsEntityDoesNotStepLocalPositionAtLowTickrate()
    {
        await OverrideCVar(Side.Server, CVars.NetTickrate, 5);
        await CreateZStack(12);
        await AddGravity();

        EntityUid item = default;
        const float startPosition = 0.25f;
        const float startVelocity = 8f;
        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var zSystem = SEntMan.System<ZLevelPhysicsSystem>();
            var zPhysics = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            zSystem.SetZPosition((item, zPhysics), startPosition);
            zSystem.SetZVelocity((item, zPhysics), startVelocity);

            Assert.That(_throwing.TryThrow(
                item,
                new Vector2(40f, 0f),
                baseThrowSpeed: 20f,
                user: SPlayer,
                playSound: false), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(zPhysics.Disabled, Is.True);
                Assert.That(zPhysics.LocalPosition, Is.EqualTo(startPosition).Within(0.001f));
                Assert.That(zPhysics.Velocity, Is.EqualTo(startVelocity).Within(0.001f));
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(item).BodyStatus, Is.EqualTo(BodyStatus.InAir));
            });
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var zPhysics = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.True);
                Assert.That(zPhysics.Disabled, Is.True);
                Assert.That(zPhysics.LocalPosition, Is.EqualTo(startPosition).Within(0.001f));
                Assert.That(zPhysics.Velocity, Is.EqualTo(startVelocity).Within(0.001f));
            });
        });
    }

    [Test]
    public async Task NonThrownZPhysicsEntityInterpolatesClientSpriteAtLowTickrate()
    {
        await OverrideCVar(Side.Server, CVars.NetTickrate, 5);
        await CreateZStack(4);
        await AddGravity();

        EntityUid item = default;
        NetEntity netItem = default;
        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            netItem = SEntMan.GetNetEntity(item);
        });

        await RunUntilSynced();

        await Server.WaitPost(() =>
        {
            var zSystem = SEntMan.System<ZLevelPhysicsSystem>();
            var zPhysics = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(item);
            zSystem.SetZPosition((item, zPhysics), 0.8f);
            zSystem.SetZVelocity((item, zPhysics), 8f);
        });

        await Pair.RunTicksSync(3);

        await AssertClientZSpriteInterpolates(netItem);
    }

    [Test]
    public async Task PickupAnimationLerpsSpriteZOffsetToTargetHeight()
    {
        EntityUid item = default;
        NetEntity netItem = default;
        const float itemHeight = 0.75f;
        const float targetHeight = 0.25f;

        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            netItem = SEntMan.GetNetEntity(item);

            var zSystem = SEntMan.System<ZLevelPhysicsSystem>();
            var itemZPhysics = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(item);
            itemZPhysics.Disabled = true;
            zSystem.SetZPosition((item, itemZPhysics), itemHeight, snapRender: true);

            var playerZPhysics = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
            playerZPhysics.Disabled = true;
            zSystem.SetZPosition((SPlayer, playerZPhysics), targetHeight, snapRender: true);
        });

        await RunUntilSynced();

        await Client.WaitPost(() =>
        {
            var clientItem = CEntMan.GetEntity(netItem);
            var itemXform = CEntMan.GetComponent<TransformComponent>(clientItem);
            var playerXform = CEntMan.GetComponent<TransformComponent>(CPlayer);
            var transforms = CEntMan.System<SharedTransformSystem>();
            var finalMapPos = transforms.ToMapCoordinates(playerXform.Coordinates).Position;
            var finalPos = Vector2.Transform(finalMapPos, transforms.GetInvWorldMatrix(itemXform.Coordinates.EntityId));

            CEntMan.System<EntityPickupAnimationSystem>().AnimateEntityPickup(
                clientItem,
                itemXform.Coordinates,
                finalPos,
                Angle.Zero,
                CPlayer);
        });

        await Client.WaitAssertion(() =>
        {
            var clone = GetClientPickupClone();
            var sprite = CEntMan.GetComponent<SpriteComponent>(clone);
            var (expectedInitial, _) = GetExpectedPickupSpriteOffsets(netItem, itemHeight, targetHeight);

            Assert.Multiple(() =>
            {
                Assert.That(sprite.Offset.X, Is.EqualTo(expectedInitial.X).Within(0.001f));
                Assert.That(sprite.Offset.Y, Is.EqualTo(expectedInitial.Y).Within(0.001f));
            });
        });

        await Client.WaitPost(() => CEntMan.FrameUpdate(0.0625f));

        await Client.WaitAssertion(() =>
        {
            var clone = GetClientPickupClone();
            var sprite = CEntMan.GetComponent<SpriteComponent>(clone);
            var (expectedInitial, expectedFinal) = GetExpectedPickupSpriteOffsets(netItem, itemHeight, targetHeight);

            Assert.Multiple(() =>
            {
                Assert.That(sprite.Offset.X, Is.EqualTo(expectedInitial.X).Within(0.001f));
                Assert.That(sprite.Offset.Y,
                    Is.GreaterThan(MathF.Min(expectedInitial.Y, expectedFinal.Y))
                        .And.LessThan(MathF.Max(expectedInitial.Y, expectedFinal.Y)));
            });
        });
    }

    [Test]
    public async Task ThrownEntityPreservesClientXyAndRotationAcrossZLevelHandoff()
    {
        await OverrideCVar(Side.Server, CVars.NetTickrate, 10);
        await OverrideCVar(Side.Server, CCVars.TileFrictionModifier, 0f);
        await OverrideCVar(Side.Server, CCVars.AirFriction, 0f);
        await OverrideCVar(Side.Server, CCVars.OffgridFriction, 0f);
        await OverrideCVar(Side.Server, CCVars.MinFriction, 0f);
        await CreateZStack(2);
        await BuildThrowRunway();

        var flatSamples = await RecordThrowVisualSamples();

        await SpawnZLevelThrowHighGround();
        var zSamples = await RecordThrowVisualSamples();

        Assert.That(zSamples, Has.Count.EqualTo(flatSamples.Count));
        Assert.That(zSamples.Any(sample => sample.SourceZLevel == 1 || sample.RenderZLevel == 1),
            Is.True,
            "The z-level comparison throw never crossed or rendered on the upper z-level.");
        Assert.That(zSamples.Any(sample => !sample.Thrown && sample.LinearVelocity.LengthSquared() > 0.01f),
            Is.True,
            "The z-level comparison throw never sampled the post-land sliding phase.");

        AssertRenderLayerDoesNotBacktrackDuringUpwardThrow(zSamples);

        for (var i = 0; i < flatSamples.Count; i++)
        {
            var flat = flatSamples[i];
            var z = zSamples[i];
            var positionDelta = Vector2.Distance(flat.WorldPosition, z.WorldPosition);
            var rotationDelta = Math.Abs(Angle.ShortestDistance(flat.WorldRotation, z.WorldRotation).Theta);

            Assert.Multiple(() =>
            {
                Assert.That(z.Tick, Is.EqualTo(flat.Tick));
                Assert.That(z.TickFraction, Is.EqualTo(flat.TickFraction));
                Assert.That(positionDelta,
                    Is.LessThan(0.025f),
                    $"sample {i}, tick {flat.Tick}+{flat.TickFraction:0.00}: flat XY {flat.WorldPosition}, z XY {z.WorldPosition}, flat z {flat.RenderAbsoluteZ}, z absolute {z.RenderAbsoluteZ}, render layer {z.RenderZLevel}, source layer {z.SourceZLevel}");
                Assert.That(rotationDelta,
                    Is.LessThan(0.025f),
                    $"sample {i}, tick {flat.Tick}+{flat.TickFraction:0.00}: flat rot {flat.WorldRotation}, z rot {z.WorldRotation}, render layer {z.RenderZLevel}, source layer {z.SourceZLevel}");
                Assert.That(Vector2.Distance(
                        z.GlobalRenderedPosition,
                        z.WorldPosition + new Vector2(0f, z.RenderAbsoluteZ * CVars.RenderZLevelVerticalOffset.DefaultValue)),
                    Is.LessThan(0.025f),
                    $"sample {i}, tick {z.Tick}+{z.TickFraction:0.00}: rendered sprite center {z.GlobalRenderedPosition} did not match absolute z {z.RenderAbsoluteZ:0.000}; pass-local render {z.RenderedPosition}, render/source layer {z.RenderZLevel}/{z.SourceZLevel}, local z {z.LocalZ:0.000}");
            });
        }
    }

    private static void AssertRenderLayerDoesNotBacktrackDuringUpwardThrow(IReadOnlyList<ThrowVisualSample> samples)
    {
        for (var i = 1; i < samples.Count; i++)
        {
            var previous = samples[i - 1];
            var current = samples[i];

            if (current.RenderAbsoluteZ + 0.001f < previous.RenderAbsoluteZ)
                continue;

            Assert.That(current.RenderZLevel,
                Is.GreaterThanOrEqualTo(previous.RenderZLevel),
                $"render layer moved backwards at sample {i}, tick {current.Tick}+{current.TickFraction:0.00}: previous abs/layer {previous.RenderAbsoluteZ:0.000}/{previous.RenderZLevel}, current abs/layer {current.RenderAbsoluteZ:0.000}/{current.RenderZLevel}, source layer {current.SourceZLevel}, thrown={current.Thrown}, disabled={current.Disabled}, body={current.BodyStatus}, linear velocity={current.LinearVelocity}");
        }
    }

    private async Task CreateZStack(int levels)
    {
        await Server.WaitPost(() =>
        {
            var maps = new EntityUid[levels];
            maps[0] = MapData.MapUid;
            for (var i = 1; i < maps.Length; i++)
                maps[i] = MapSystem.CreateMap(out _);

            Assert.That(SEntMan.System<ZLevelSystem>().TryCreateMapNetwork(maps, out _), Is.True);
        });
    }

    private async Task BuildThrowRunway()
    {
        await Server.WaitPost(() =>
        {
            var tile = new Tile(TileMan[Plating].TileId);
            for (var x = 0; x <= 10; x++)
                MapSystem.SetTile(MapData.Grid.Owner, MapData.Grid.Comp, new Vector2i(x, 0), tile);
        });

        await RunUntilSynced();
    }

    private async Task SpawnZLevelThrowHighGround()
    {
        await Server.WaitPost(() =>
        {
            for (var x = 4; x <= 10; x++)
            {
                var highGround = SEntMan.SpawnEntity(
                    "ZLevelThrowTestHighGround",
                    new EntityCoordinates(MapData.Grid, new Vector2(x + 0.5f, 0.5f)));

                Transform.SetLocalRotation(highGround, Angle.Zero);
            }
        });

        await RunUntilSynced();
    }

    private async Task<List<ThrowVisualSample>> RecordThrowVisualSamples()
    {
        EntityUid item = default;
        NetEntity netItem = default;

        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity(
                "Crowbar",
                MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            netItem = SEntMan.GetNetEntity(item);

            var zSystem = SEntMan.System<ZLevelPhysicsSystem>();
            var zPhysics = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            zSystem.SetZPosition((item, zPhysics), 0f, snapRender: true);
            zSystem.SetZVelocity((item, zPhysics), 0f);
        });

        await RunUntilSynced();

        await Server.WaitPost(() =>
        {
            _random.SetSeed(ThrowComparisonSeed);
            Assert.That(_throwing.TryThrow(
                item,
                new Vector2(4f, 0f),
                baseThrowSpeed: 8f,
                user: SPlayer,
                playSound: false), Is.True);
        });

        var samples = new List<ThrowVisualSample>();
        var fractions = new[] { 0f, 0.25f, 0.5f, 0.75f };

        for (var tick = 0; tick < 14; tick++)
        {
            await Pair.RunTicksSync(1);

            foreach (var fraction in fractions)
                await RecordClientThrowSample(netItem, tick, fraction, samples);
        }

        await Server.WaitPost(() =>
        {
            if (SEntMan.EntityExists(item))
                SEntMan.DeleteEntity(item);
        });
        await RunUntilSynced();

        return samples;
    }

    private async Task RecordClientThrowSample(
        NetEntity netItem,
        int tick,
        float tickFraction,
        List<ThrowVisualSample> samples)
    {
        await Client.WaitPost(() =>
        {
            var clientItem = CEntMan.GetEntity(netItem);
            var xform = CEntMan.GetComponent<TransformComponent>(clientItem);
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientItem);
            var transforms = CEntMan.System<SharedTransformSystem>();
            var zVisuals = CEntMan.System<ZLevelPhysicsVisualSystem>();

            CTiming.TickRemainder = TimeSpan.FromTicks((long) (CTiming.TickPeriod.Ticks * tickFraction));
            CEntMan.FrameUpdate((float) CTiming.TickPeriod.TotalSeconds);

            var (worldPosition, worldRotation) = transforms.GetWorldPositionRotation(xform);
            var renderedPosition = worldPosition + ZLevelPhysicsVisualSystem.GetWorldRenderOffset(
                sprite.Offset,
                worldRotation,
                Angle.Zero,
                sprite.NoRotation);
            var globalRenderedPosition = renderedPosition;
            var linearVelocity = Vector2.Zero;
            var renderAbsoluteZ = 0f;
            var localZ = 0f;
            var renderZLevel = 0;
            var sourceZLevel = 0;
            var bodyStatus = BodyStatus.OnGround;

            if (CEntMan.TryGetComponent<PhysicsComponent>(clientItem, out var physics))
            {
                linearVelocity = physics.LinearVelocity;
                bodyStatus = physics.BodyStatus;
            }

            if (CEntMan.TryGetComponent<ZLevelPhysicsComponent>(clientItem, out var zPhysics))
            {
                localZ = zPhysics.LocalPosition;
                var renderData = zVisuals.GetVisualRenderData(clientItem, zPhysics, xform);
                renderAbsoluteZ = renderData.RenderAbsolutePosition;
                renderZLevel = renderData.RenderZLevel;
                sourceZLevel = renderData.SourceZLevel;
                renderedPosition = zVisuals.GetSpriteRenderWorldPosition(
                    worldPosition,
                    sprite.Offset,
                    renderData,
                    worldRotation,
                    Angle.Zero,
                    sprite.NoRotation);
                globalRenderedPosition = renderedPosition + new Vector2(0f, renderZLevel * CVars.RenderZLevelVerticalOffset.DefaultValue);
            }

            samples.Add(new ThrowVisualSample(
                tick,
                tickFraction,
                worldPosition,
                renderedPosition,
                globalRenderedPosition,
                worldRotation,
                linearVelocity,
                renderAbsoluteZ,
                localZ,
                renderZLevel,
                sourceZLevel,
                bodyStatus,
                CEntMan.HasComponent<ThrownItemComponent>(clientItem),
                CEntMan.TryGetComponent<ZLevelPhysicsComponent>(clientItem, out var sampledZPhysics) && sampledZPhysics.Disabled));
        });
    }

    private EntityUid GetClientPickupClone()
    {
        EntityUid? clone = null;
        var query = CEntMan.AllEntityQueryEnumerator<EntityPickupAnimationComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            Assert.That(clone, Is.Null, "Expected only one active pickup animation clone.");
            clone = uid;
        }

        Assert.That(clone, Is.Not.Null, "Expected an active pickup animation clone.");
        return clone.Value;
    }

    private (Vector2 Initial, Vector2 Final) GetExpectedPickupSpriteOffsets(
        NetEntity netItem,
        float itemHeight,
        float targetHeight)
    {
        var clientItem = CEntMan.GetEntity(netItem);
        var zVisuals = CEntMan.System<ZLevelPhysicsVisualSystem>();
        var sourceSprite = CEntMan.GetComponent<SpriteComponent>(clientItem);
        var baseOffset = zVisuals.GetBaseSpriteOffset(clientItem, sourceSprite);
        var verticalOffset = Client.CfgMan.GetCVar(CVars.RenderZLevelVerticalOffset);

        return (
            baseOffset + new Vector2(0f, itemHeight * verticalOffset),
            baseOffset + new Vector2(0f, targetHeight * verticalOffset));
    }

    private async Task AssertClientZSpriteInterpolates(NetEntity netItem)
    {
        await Client.WaitAssertion(() =>
        {
            var clientItem = CEntMan.GetEntity(netItem);
            var xform = CEntMan.GetComponent<TransformComponent>(clientItem);
            var zPhysics = CEntMan.GetComponent<ZLevelPhysicsComponent>(clientItem);
            var physics = CEntMan.GetComponent<PhysicsComponent>(clientItem);
            var meta = CEntMan.GetComponent<MetaDataComponent>(clientItem);
            var containers = CEntMan.System<SharedContainerSystem>();
            var zSystem = CEntMan.System<ZLevelPhysicsSystem>();
            var visuals = CEntMan.System<ZLevelPhysicsVisualSystem>();
            var transforms = CEntMan.System<SharedTransformSystem>();
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientItem);

            CTiming.TickRemainder = CTiming.TickPeriod / 2;
            var renderPosition = visuals.GetRenderPosition(clientItem, zPhysics);
            var renderPrevious = zPhysics.RenderPreviousAbsolutePosition - zPhysics.CurrentZLevel;
            var renderTarget = zPhysics.RenderTargetAbsolutePosition - zPhysics.CurrentZLevel;
            var renderMin = MathF.Min(renderPrevious, renderTarget);
            var renderMax = MathF.Max(renderPrevious, renderTarget);
            var verticalOffset = Client.CfgMan.GetCVar(CVars.RenderZLevelVerticalOffset);
            var worldRotation = transforms.GetWorldRotation(xform);
            var expectedLocalZOffset = ZLevelPhysicsVisualSystem.GetLocalRenderOffset(
                renderPosition,
                verticalOffset,
                worldRotation,
                sprite.NoRotation);
            var minLocalZOffset = ZLevelPhysicsVisualSystem.GetLocalRenderOffset(
                renderMin,
                verticalOffset,
                worldRotation,
                sprite.NoRotation);
            var maxLocalZOffset = ZLevelPhysicsVisualSystem.GetLocalRenderOffset(
                renderMax,
                verticalOffset,
                worldRotation,
                sprite.NoRotation);

            CEntMan.FrameUpdate((float) CTiming.TickPeriod.TotalSeconds);
            var expectedSpriteOffset = zPhysics.SpriteOffsetDefault + expectedLocalZOffset;
            var spriteOffsetMin = zPhysics.SpriteOffsetDefault + minLocalZOffset;
            var spriteOffsetMax = zPhysics.SpriteOffsetDefault + maxLocalZOffset;

            Assert.Multiple(() =>
            {
                Assert.That(zPhysics.Velocity, Is.Not.EqualTo(0f),
                    $"client z velocity {zPhysics.Velocity}, z local {zPhysics.LocalPosition}, body status {physics.BodyStatus}");
                Assert.That(zPhysics.RenderStateInitialized, Is.True);
                Assert.That(zSystem.ActiveBodies, Does.Contain(clientItem),
                    $"client active z bodies missing item; z velocity {zPhysics.Velocity}, local {zPhysics.LocalPosition}, parent {xform.ParentUid}, grid {xform.GridUid}, map {xform.MapUid}, z map {CEntMan.HasComponent<ZLevelMapComponent>(xform.MapUid)}, body type {physics.BodyType}, body status {physics.BodyStatus}, anchored {xform.Anchored}, meta flags {meta.Flags}, in container {containers.IsEntityOrParentInContainer(clientItem, meta, xform)}");
                Assert.That(MathF.Abs(zPhysics.RenderTargetAbsolutePosition - zPhysics.RenderPreviousAbsolutePosition),
                    Is.GreaterThan(0.001f),
                    $"client z render prev {zPhysics.RenderPreviousAbsolutePosition}, target {zPhysics.RenderTargetAbsolutePosition}, tick {zPhysics.RenderLerpTick}, current {CTiming.CurTick}");
                Assert.That(renderPosition, Is.GreaterThan(renderMin).And.LessThan(renderMax),
                    $"client z render {renderPosition}, prev {zPhysics.RenderPreviousAbsolutePosition}, target {zPhysics.RenderTargetAbsolutePosition}, tick {zPhysics.RenderLerpTick}, current {CTiming.CurTick}, local {zPhysics.LocalPosition}");
                Assert.That(zPhysics.SpriteOffsetDefaultCached, Is.True);
                Assert.That(sprite.Offset.Y, Is.GreaterThan(MathF.Min(spriteOffsetMin.Y, spriteOffsetMax.Y)).And.LessThan(MathF.Max(spriteOffsetMin.Y, spriteOffsetMax.Y)),
                    $"client sprite offset {sprite.Offset}, expected {expectedSpriteOffset}, render {renderPosition}, prev {zPhysics.RenderPreviousAbsolutePosition}, target {zPhysics.RenderTargetAbsolutePosition}");
                Assert.That(sprite.Offset.X, Is.EqualTo(expectedSpriteOffset.X).Within(0.001f));
                Assert.That(sprite.Offset.Y, Is.EqualTo(expectedSpriteOffset.Y).Within(0.001f));
            });
        });
    }
}
