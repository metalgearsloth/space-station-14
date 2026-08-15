using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using ClientPuddleSystem = Content.Client.Fluids.PuddleSystem;
using ServerPuddleSystem = Content.Server.Fluids.EntitySystems.PuddleSystem;

namespace Content.IntegrationTests.Tests.Fluids
{
    [TestFixture]
    [TestOf(typeof(PuddleComponent))]
    public sealed class PuddleTest : GameTest
    {
        private const string PuddleShaderPrototype = "Puddle";

        [Test]
        public async Task TilePuddleTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();

            var spillSystem = server.System<ServerPuddleSystem>();

            await server.WaitAssertion(() =>
            {
                var solution = new Solution("Water", FixedPoint2.New(20));
                var tile = testMap.Tile;
                var gridUid = tile.GridUid;
                var (x, y) = tile.GridIndices;
                var coordinates = new EntityCoordinates(gridUid, x, y);

                Assert.That(spillSystem.TrySpillAt(coordinates, solution, out _), Is.True);
            });
        }

        [Test]
        public async Task SpaceNoPuddleTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();
            var grid = testMap.Grid;

            var spillSystem = server.System<ServerPuddleSystem>();
            var mapSystem = server.System<SharedMapSystem>();

            // Remove all tiles
            await server.WaitPost(() =>
            {
                var tiles = new List<(Vector2i GridIndices, Tile Tile)>();
                var tileEnumerator = mapSystem.GetAllTiles(grid.Owner, grid.Comp);

                foreach (var tile in tileEnumerator)
                {
                    tiles.Add((tile.GridIndices, Tile.Empty));
                }

                mapSystem.SetTiles(grid, tiles);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var coordinates = grid.Owner.ToCoordinates();
                var solution = new Solution("Water", FixedPoint2.New(20));

                Assert.That(spillSystem.TrySpillAt(coordinates, solution, out _), Is.False);
            });
        }

        /// <summary>
        /// Asserts that:
        /// - puddles only blend across visually joined puddles
        /// - respond to the graphics CVar
        /// - reset when color appearance is removed, and
        /// - recalculate neighbors when a puddle is deleted.
        /// </summary>
        [Test]
        public async Task PuddleShaderColorsUpdateAndReset()
        {
            var pair = Pair;
            var server = pair.Server;
            var client = pair.Client;

            var testMap = await pair.CreateTestMap();
            var spillSystem = server.System<ServerPuddleSystem>();
            var appearance = server.System<SharedAppearanceSystem>();
            var map = server.System<SharedMapSystem>();
            var clientPuddleSystem = client.System<ClientPuddleSystem>();
            var clientConfig = client.ResolveDependency<IConfigurationManager>();

            EntityUid leftPuddle = default;
            EntityUid rightPuddle = default;
            EntityUid northPuddle = default;
            EntityUid northEastPuddle = default;
            var red = Color.Red.WithAlpha(0.7f);
            var green = Color.Green.WithAlpha(0.7f);
            var blue = Color.Blue.WithAlpha(0.7f);
            var cyan = Color.Cyan.WithAlpha(0.7f);
            var yellow = Color.Yellow.WithAlpha(0.7f);

            // Set up adjacent, visually joined puddles with distinct colors and ensure shaders start enabled.
            await client.WaitPost(() =>
            {
                clientConfig.SetCVar(CCVars.PuddleShaders, true);
            });

            await server.WaitAssertion(() =>
            {
                var tile = testMap.Tile;
                var gridUid = tile.GridUid;
                var indices = tile.GridIndices;
                map.SetTile(testMap.Grid, new Vector2i(indices.X + 1, indices.Y), tile.Tile);
                map.SetTile(testMap.Grid, new Vector2i(indices.X, indices.Y + 1), tile.Tile);
                map.SetTile(testMap.Grid, new Vector2i(indices.X + 1, indices.Y + 1), tile.Tile);

                Assert.That(spillSystem.TrySpillAt(
                    new EntityCoordinates(gridUid, indices.X, indices.Y),
                    new Solution("Water", FixedPoint2.New(20)),
                    out leftPuddle),
                    Is.True);

                Assert.That(spillSystem.TrySpillAt(
                    new EntityCoordinates(gridUid, indices.X + 1, indices.Y),
                    new Solution("Water", FixedPoint2.New(20)),
                    out rightPuddle),
                    Is.True);

                Assert.That(spillSystem.TrySpillAt(
                    new EntityCoordinates(gridUid, indices.X, indices.Y + 1),
                    new Solution("Water", FixedPoint2.New(20)),
                    out northPuddle),
                    Is.True);

                Assert.That(spillSystem.TrySpillAt(
                    new EntityCoordinates(gridUid, indices.X + 1, indices.Y + 1),
                    new Solution("Water", FixedPoint2.New(20)),
                    out northEastPuddle),
                    Is.True);

                appearance.SetData(leftPuddle, PuddleVisuals.SolutionColor, red);
                appearance.SetData(rightPuddle, PuddleVisuals.SolutionColor, green);
                appearance.SetData(northPuddle, PuddleVisuals.SolutionColor, cyan);
                appearance.SetData(northEastPuddle, PuddleVisuals.SolutionColor, yellow);
                appearance.SetData(leftPuddle, PuddleVisuals.CurrentVolume, 1f);
                appearance.SetData(rightPuddle, PuddleVisuals.CurrentVolume, 1f);
                appearance.SetData(northPuddle, PuddleVisuals.CurrentVolume, 1f);
                appearance.SetData(northEastPuddle, PuddleVisuals.CurrentVolume, 1f);
            });

            await pair.RunUntilSynced();

            var leftNet = server.EntMan.GetNetEntity(leftPuddle);
            var rightNet = server.EntMan.GetNetEntity(rightPuddle);
            var northNet = server.EntMan.GetNetEntity(northPuddle);
            var northEastNet = server.EntMan.GetNetEntity(northEastPuddle);
            var clientLeft = client.EntMan.GetEntity(leftNet);
            var clientRight = client.EntMan.GetEntity(rightNet);
            var clientNorth = client.EntMan.GetEntity(northNet);
            var clientNorthEast = client.EntMan.GetEntity(northEastNet);

            // Joined puddles should have shaders and should set each other as cardinal and diagonal blend colors.
            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<PuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<PuddleComponent>(clientRight);
                var north = client.EntMan.GetComponent<PuddleComponent>(clientNorth);
                var northEast = client.EntMan.GetComponent<PuddleComponent>(clientNorthEast);
                var leftSprite = client.EntMan.GetComponent<SpriteComponent>(clientLeft);
                var rightSprite = client.EntMan.GetComponent<SpriteComponent>(clientRight);
                var northSprite = client.EntMan.GetComponent<SpriteComponent>(clientNorth);
                var northEastSprite = client.EntMan.GetComponent<SpriteComponent>(clientNorthEast);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientLeft, out var leftShader), Is.True);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientRight, out var rightShader), Is.True);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientNorth, out var northShader), Is.True);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientNorthEast, out var northEastShader), Is.True);

                Assert.That(HasPuddleLayerShader(leftSprite), Is.True);
                Assert.That(HasPuddleLayerShader(rightSprite), Is.True);
                Assert.That(HasPuddleLayerShader(northSprite), Is.True);
                Assert.That(HasPuddleLayerShader(northEastSprite), Is.True);

                Assert.That(left.SolutionColor, Is.EqualTo(red));
                Assert.That(right.SolutionColor, Is.EqualTo(green));
                Assert.That(north.SolutionColor, Is.EqualTo(cyan));
                Assert.That(northEast.SolutionColor, Is.EqualTo(yellow));
                Assert.That(leftShader.Color, Is.EqualTo(red));
                Assert.That(leftShader.NorthColor, Is.EqualTo(cyan));
                Assert.That(leftShader.EastColor, Is.EqualTo(green));
                Assert.That(leftShader.NorthEastColor, Is.EqualTo(yellow));
                Assert.That(northShader.SouthColor, Is.EqualTo(red));
                Assert.That(northShader.EastColor, Is.EqualTo(yellow));
                Assert.That(rightShader.WestColor, Is.EqualTo(red));
                Assert.That(rightShader.NorthColor, Is.EqualTo(yellow));
                Assert.That(northEastShader.SouthWestColor, Is.EqualTo(red));
            });

            // A low-volume puddle visually stops joining, so neither side should blend across that seam.
            await server.WaitPost(() =>
            {
                appearance.SetData(rightPuddle, PuddleVisuals.CurrentVolume, SharedPuddleSystem.LowThreshold / 2f);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientLeft, out var leftShader), Is.True);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientRight, out var rightShader), Is.True);

                Assert.That(leftShader.EastColor, Is.EqualTo(red));
                Assert.That(rightShader.WestColor, Is.EqualTo(green));
            });

            // Restoring joinable volume should restore the cross-puddle blend colors.
            await server.WaitPost(() =>
            {
                appearance.SetData(rightPuddle, PuddleVisuals.CurrentVolume, 1f);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientLeft, out var leftShader), Is.True);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientRight, out var rightShader), Is.True);

                Assert.That(leftShader.EastColor, Is.EqualTo(green));
                Assert.That(rightShader.WestColor, Is.EqualTo(red));
            });

            // Disabling the graphics CVar should remove shader instances and restore normal per-sprite tinting.
            await client.WaitPost(() =>
            {
                clientConfig.SetCVar(CCVars.PuddleShaders, false);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<PuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<PuddleComponent>(clientRight);
                var leftSprite = client.EntMan.GetComponent<SpriteComponent>(clientLeft);
                var rightSprite = client.EntMan.GetComponent<SpriteComponent>(clientRight);

                Assert.That(HasPuddleLayerShader(leftSprite), Is.False);
                Assert.That(HasPuddleLayerShader(rightSprite), Is.False);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientLeft, out _), Is.False);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientRight, out _), Is.False);
                Assert.That(leftSprite.Color, Is.EqualTo(red));
                Assert.That(rightSprite.Color, Is.EqualTo(green));
            });

            // Re-enabling the graphics CVar should recreate shaders and restore blend colors for all puddles.
            await client.WaitPost(() =>
            {
                clientConfig.SetCVar(CCVars.PuddleShaders, true);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<PuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<PuddleComponent>(clientRight);
                var leftSprite = client.EntMan.GetComponent<SpriteComponent>(clientLeft);
                var rightSprite = client.EntMan.GetComponent<SpriteComponent>(clientRight);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientLeft, out var leftShader), Is.True);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientRight, out var rightShader), Is.True);

                Assert.That(HasPuddleLayerShader(leftSprite), Is.True);
                Assert.That(HasPuddleLayerShader(rightSprite), Is.True);
                Assert.That(leftShader.Color, Is.EqualTo(red));
                Assert.That(leftShader.EastColor, Is.EqualTo(green));
                Assert.That(rightShader.WestColor, Is.EqualTo(red));
            });

            // Removing color appearance should reset the puddle to white and update its neighbor's blend color.
            await server.WaitPost(() =>
            {
                appearance.RemoveData(rightPuddle, PuddleVisuals.SolutionColor);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<PuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<PuddleComponent>(clientRight);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientLeft, out var leftShader), Is.True);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientRight, out var rightShader), Is.True);

                Assert.That(right.SolutionColor, Is.EqualTo(Color.White));
                Assert.That(rightShader.Color, Is.EqualTo(Color.White));
                Assert.That(leftShader.EastColor, Is.EqualTo(Color.White));
            });

            // Setting a new color should propagate to the puddle shader and adjacent blend color.
            await server.WaitPost(() =>
            {
                appearance.SetData(rightPuddle, PuddleVisuals.SolutionColor, blue);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<PuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<PuddleComponent>(clientRight);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientLeft, out var leftShader), Is.True);
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientRight, out var rightShader), Is.True);

                Assert.That(right.SolutionColor, Is.EqualTo(blue));
                Assert.That(rightShader.Color, Is.EqualTo(blue));
                Assert.That(leftShader.EastColor, Is.EqualTo(blue));
            });

            // Deleting a puddle should make its former neighbor fall back to its own color.
            await server.WaitPost(() =>
            {
                server.EntMan.DeleteEntity(rightPuddle);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<PuddleComponent>(clientLeft);
                var leftColor = left.SolutionColor;
                Assert.That(clientPuddleSystem.TryGetShaderDebugState(clientLeft, out var leftShader), Is.True);

                Assert.That(leftShader.Color, Is.EqualTo(leftColor));
                Assert.That(leftShader.EastColor, Is.EqualTo(leftColor));
            });
        }

        private static bool HasPuddleLayerShader(SpriteComponent sprite)
        {
            return sprite[0] is SpriteComponent.Layer layer &&
                   layer.ShaderPrototype == PuddleShaderPrototype;
        }
    }
}
