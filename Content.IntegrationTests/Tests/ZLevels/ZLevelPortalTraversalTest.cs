#nullable enable
using System;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.ZLevels;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.ZLevels;

[TestFixture]
[NonParallelizable]
public sealed class ZLevelPortalTraversalTest : MovementTest
{
    protected override int Tiles => 5;
    protected override bool AddWalls => false;

    [Test]
    public async Task LadderDoAfterTraversesBothDirections()
    {
        EntityUid upperMap = default;
        EntityUid upperGrid = default;
        EntityUid lowerLadder = default;
        EntityUid upperLadder = default;
        var ladderPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            (upperMap, upperGrid) = CreateLinkedStack();
            PlacePlayer(MapData.Grid.Owner, ladderPosition);
            lowerLadder = SpawnLadder("ZLevelLadderUp", MapData.Grid.Owner, ladderPosition);
        });
        await RunTicks(4);

        await Server.WaitAssertion(() =>
        {
            upperLadder = GetPairedEndpoint(lowerLadder);
            SetClimbDelay(lowerLadder, OneTickDelay());
            SetClimbDelay(upperLadder, OneTickDelay());
        });

        await Interact(lowerLadder, new EntityCoordinates(MapData.Grid.Owner, ladderPosition));

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(upperMap));
                Assert.That(xform.GridUid, Is.EqualTo(upperGrid));
            });
        });

        await Interact(upperLadder, new EntityCoordinates(upperGrid, ladderPosition));

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(MapData.MapUid));
                Assert.That(xform.GridUid, Is.EqualTo(MapData.Grid.Owner));
            });
        });
    }

    [Test]
    public async Task CancelledLadderDoAfterDoesNotTraverse()
    {
        EntityUid lowerLadder = default;
        var ladderPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            CreateLinkedStack();
            PlacePlayer(MapData.Grid.Owner, ladderPosition);
            lowerLadder = SpawnLadder("ZLevelLadderUp", MapData.Grid.Owner, ladderPosition);
        });
        await RunTicks(4);

        await Server.WaitAssertion(() => SetClimbDelay(lowerLadder, STiming.TickPeriod * 10));
        await Interact(lowerLadder, new EntityCoordinates(MapData.Grid.Owner, ladderPosition), awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));

        await CancelDoAfters();

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(MapData.MapUid));
                Assert.That(xform.GridUid, Is.EqualTo(MapData.Grid.Owner));
            });
        });
    }

    [Test]
    public async Task LadderDoAfterFailsSafelyWhenDestinationBecomesBlocked()
    {
        EntityUid upperGrid = default;
        EntityUid lowerLadder = default;
        var ladderPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            (_, upperGrid) = CreateLinkedStack();
            PlacePlayer(MapData.Grid.Owner, ladderPosition);
            lowerLadder = SpawnLadder("ZLevelLadderUp", MapData.Grid.Owner, ladderPosition);
        });
        await RunTicks(4);

        await Server.WaitAssertion(() => SetClimbDelay(lowerLadder, STiming.TickPeriod * 8));
        await Interact(lowerLadder, new EntityCoordinates(MapData.Grid.Owner, ladderPosition), awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));

        await Server.WaitPost(() =>
        {
            SEntMan.SpawnAttachedTo(WallPrototype, new EntityCoordinates(upperGrid, ladderPosition));
        });

        await AwaitDoAfters();

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(MapData.MapUid));
                Assert.That(xform.GridUid, Is.EqualTo(MapData.Grid.Owner));
            });
        });
    }

    [Test]
    public async Task EndpointAutoSpawnsOnlyOneAdjacentCounterpart()
    {
        EntityUid upperMap = default;
        EntityUid upperGrid = default;
        EntityUid lowerLadder = default;
        var ladderPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            (upperMap, upperGrid) = CreateLinkedStack();
            lowerLadder = SpawnLadder("ZLevelLadderUp", MapData.Grid.Owner, ladderPosition);
        });
        await RunTicks(12);

        await Server.WaitAssertion(() =>
        {
            var upperLadder = GetPairedEndpoint(lowerLadder);
            var upperXform = SEntMan.GetComponent<TransformComponent>(upperLadder);
            Assert.Multiple(() =>
            {
                Assert.That(upperXform.MapUid, Is.EqualTo(upperMap));
                Assert.That(upperXform.GridUid, Is.EqualTo(upperGrid));
                Assert.That(CountPortals(upperMap, "ZLevelLadderDown"), Is.EqualTo(1));
                Assert.That(CountPortals(MapData.MapUid, "ZLevelLadderUp"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ExistingCompatibleEndpointLinksInsteadOfDuplicating()
    {
        EntityUid upperMap = default;
        EntityUid upperGrid = default;
        EntityUid lowerLadder = default;
        EntityUid upperLadder = default;
        var ladderPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            upperMap = MapSystem.CreateMap(out var upperMapId);
            upperGrid = MapSystem.CreateGridEntity(upperMapId).Owner;
            FillGrid(upperGrid);

            lowerLadder = SpawnLadder("ZLevelLadderUp", MapData.Grid.Owner, ladderPosition);
            upperLadder = SpawnLadder("ZLevelLadderDown", upperGrid, ladderPosition);

            Assert.That(SEntMan.System<ZLevelSystem>().TryCreateMapNetwork([MapData.MapUid, upperMap], out _), Is.True);
            Assert.That(SEntMan.System<ZLevelSystem>().TryLinkGrids(MapData.Grid.Owner, upperGrid), Is.True);
        });
        await RunTicks(12);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetPairedEndpoint(lowerLadder), Is.EqualTo(upperLadder));
                Assert.That(GetPairedEndpoint(upperLadder), Is.EqualTo(lowerLadder));
                Assert.That(CountPortals(upperMap, "ZLevelLadderDown"), Is.EqualTo(1));
                Assert.That(CountPortals(MapData.MapUid, "ZLevelLadderUp"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task PairingDefersUntilAdjacentZMapExists()
    {
        EntityUid upperMap = default;
        EntityUid lowerLadder = default;
        var ladderPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            lowerLadder = SpawnLadder("ZLevelLadderUp", MapData.Grid.Owner, ladderPosition);
        });
        await RunTicks(4);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ZLevelPortalComponent>(lowerLadder).PairedEndpoint, Is.Null);
        });

        await Server.WaitPost(() =>
        {
            upperMap = MapSystem.CreateMap(out _);
            Assert.That(SEntMan.System<ZLevelSystem>().TryCreateMapNetwork([MapData.MapUid, upperMap], out _), Is.True);
        });
        await RunTicks(12);

        await Server.WaitAssertion(() =>
        {
            var upperLadder = GetPairedEndpoint(lowerLadder);
            var upperXform = SEntMan.GetComponent<TransformComponent>(upperLadder);
            Assert.Multiple(() =>
            {
                Assert.That(upperXform.MapUid, Is.EqualTo(upperMap));
                Assert.That(upperXform.GridUid, Is.Null);
            });
        });
    }

    [Test]
    public async Task DeletingGeneratedSourceDeletesCounterpartWithoutRecursion()
    {
        EntityUid lowerLadder = default;
        EntityUid upperLadder = default;

        await Server.WaitPost(() =>
        {
            CreateLinkedStack();
            lowerLadder = SpawnLadder("ZLevelLadderUp", MapData.Grid.Owner, new Vector2(0.5f, 0.5f));
        });
        await RunTicks(4);

        await Server.WaitAssertion(() =>
        {
            upperLadder = GetPairedEndpoint(lowerLadder);
            Assert.That(SEntMan.EntityExists(upperLadder), Is.True);
        });

        await Server.WaitPost(() => SEntMan.DeleteEntity(lowerLadder));
        await RunTicks(4);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(upperLadder), Is.False);
        });
    }

    [Test]
    public async Task DeletingGeneratedCounterpartClearsSourceReference()
    {
        EntityUid lowerLadder = default;
        EntityUid upperLadder = default;

        await Server.WaitPost(() =>
        {
            CreateLinkedStack();
            lowerLadder = SpawnLadder("ZLevelLadderUp", MapData.Grid.Owner, new Vector2(0.5f, 0.5f));
        });
        await RunTicks(4);

        await Server.WaitAssertion(() =>
        {
            upperLadder = GetPairedEndpoint(lowerLadder);
            Assert.That(SEntMan.EntityExists(upperLadder), Is.True);
        });

        await Server.WaitPost(() => SEntMan.DeleteEntity(upperLadder));
        await RunTicks(4);

        await Server.WaitAssertion(() =>
        {
            var sourcePortal = SEntMan.GetComponent<ZLevelPortalComponent>(lowerLadder);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityExists(lowerLadder), Is.True);
                Assert.That(sourcePortal.PairedEndpoint, Is.Null);
            });
        });
    }

    [Test]
    public async Task DeletingDestinationMapClearsSourcePairReference()
    {
        EntityUid upperMap = default;
        EntityUid lowerLadder = default;

        await Server.WaitPost(() =>
        {
            (upperMap, _) = CreateLinkedStack();
            lowerLadder = SpawnLadder("ZLevelLadderUp", MapData.Grid.Owner, new Vector2(0.5f, 0.5f));
        });
        await RunTicks(4);

        await Server.WaitAssertion(() => Assert.That(GetPairedEndpoint(lowerLadder), Is.Not.EqualTo(EntityUid.Invalid)));

        await Server.WaitPost(() =>
        {
            var mapId = SEntMan.GetComponent<MapComponent>(upperMap).MapId;
            MapSystem.DeleteMap(mapId);
        });
        await RunTicks(6);

        await Server.WaitAssertion(() =>
        {
            var sourcePortal = SEntMan.GetComponent<ZLevelPortalComponent>(lowerLadder);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityExists(lowerLadder), Is.True);
                Assert.That(sourcePortal.PairedEndpoint, Is.Null);
            });
        });
    }

    private TimeSpan OneTickDelay()
        => STiming.TickPeriod / 2;

    private EntityUid SpawnLadder(string prototype, EntityUid parent, Vector2 localPosition)
    {
        var ladder = SEntMan.SpawnAttachedTo(prototype, new EntityCoordinates(parent, localPosition));
        Assert.That(SEntMan.HasComponent<ZLevelPortalComponent>(ladder), Is.True);
        Assert.That(SEntMan.HasComponent<ZLevelLadderComponent>(ladder), Is.True);
        return ladder;
    }

    private EntityUid GetPairedEndpoint(EntityUid source)
    {
        var portal = SEntMan.GetComponent<ZLevelPortalComponent>(source);
        Assert.That(portal.PairedEndpoint, Is.Not.Null);
        var paired = portal.PairedEndpoint!.Value;
        var pairedPortal = SEntMan.GetComponent<ZLevelPortalComponent>(paired);
        Assert.Multiple(() =>
        {
            Assert.That(pairedPortal.PairedEndpoint, Is.EqualTo(source));
            Assert.That(pairedPortal.DestinationOffset, Is.EqualTo(-portal.DestinationOffset));
        });
        return paired;
    }

    private void SetClimbDelay(EntityUid ladder, TimeSpan delay)
    {
        typeof(ZLevelLadderComponent)
            .GetField(nameof(ZLevelLadderComponent.ClimbDelay))!
            .SetValue(SEntMan.GetComponent<ZLevelLadderComponent>(ladder), delay);
    }

    private int CountPortals(EntityUid map, string prototype)
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<ZLevelPortalComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == map &&
                SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == prototype)
            {
                count++;
            }
        }

        return count;
    }

    private void PlacePlayer(EntityUid parent, Vector2 localPosition)
    {
        Transform.SetCoordinates(SPlayer, new EntityCoordinates(parent, localPosition));
        var vertical = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
        vertical.VelocityGravity = false;
        SEntMan.System<ZLevelPhysicsSystem>().SetZPosition((SPlayer, vertical), 0f);
    }

    private (EntityUid Map, EntityUid Grid) CreateLinkedStack()
    {
        var upperMap = MapSystem.CreateMap(out var upperMapId);
        var upperGrid = MapSystem.CreateGridEntity(upperMapId).Owner;
        Assert.That(SEntMan.System<ZLevelSystem>().TryCreateMapNetwork([MapData.MapUid, upperMap], out _), Is.True);
        Assert.That(SEntMan.System<ZLevelSystem>().TryLinkGrids(MapData.Grid.Owner, upperGrid), Is.True);
        FillGrid(upperGrid);
        return (upperMap, upperGrid);
    }

    private void FillGrid(EntityUid grid)
    {
        var tile = new Tile(TileMan[Plating].TileId);
        var gridComp = SEntMan.GetComponent<MapGridComponent>(grid);
        for (var x = -Tiles; x <= Tiles; x++)
        {
            for (var y = -Tiles; y <= Tiles; y++)
            {
                MapSystem.SetTile(grid, gridComp, new Vector2i(x, y), tile);
            }
        }
    }
}
