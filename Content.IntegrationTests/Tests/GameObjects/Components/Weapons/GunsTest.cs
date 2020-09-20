#nullable enable
using System.Threading.Tasks;
using Content.Server.GameObjects.Components.Weapon.Ranged;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Resources;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameObjects.Components.Weapons
{
    [TestFixture]
    public sealed class GunsTest : ContentIntegrationTest
    {
        /// <summary>
        ///     Tests whether the fillprototypes for all guns are valid prototypes
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task TestFillPrototypes()
        {
            var server = StartServerDummyTicker();
            await server.WaitIdleAsync();
            var entityManager = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var protoManager = server.ResolveDependency<IPrototypeManager>();

            server.Assert(() =>
            {
                var mapId = mapManager.CreateMap(new MapId(1));
                
                foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Components.ContainsKey("BoltActionBarrel"))
                    {
                        var entity = entityManager.SpawnEntity(proto.ID, mapManager.GetMapEntity(mapId).Transform.MapPosition);

                        if (!entity.TryGetComponent(out ServerBoltActionBarrelComponent? boltAction))
                            continue;

                        Assert.That(boltAction.FillPrototype == null ||
                                    protoManager.HasIndex<EntityPrototype>(boltAction.FillPrototype), $"{proto.ID} does not have valid FillPrototype");

                        continue;
                    }
                    
                    if (proto.Components.ContainsKey("PumpBarrel"))
                    {
                        var entity = entityManager.SpawnEntity(proto.ID, mapManager.GetMapEntity(new MapId(1)).Transform.MapPosition);

                        if (!entity.TryGetComponent(out ServerPumpBarrelComponent? pump))
                            continue;

                        Assert.That(pump.FillPrototype == null ||
                                    protoManager.HasIndex<EntityPrototype>(pump.FillPrototype), $"{proto.ID} does not have valid FillPrototype");

                        continue;
                    }
                    
                    if (proto.Components.ContainsKey("RevolverBarrel"))
                    {
                        var entity = entityManager.SpawnEntity(proto.ID, mapManager.GetMapEntity(new MapId(1)).Transform.MapPosition);

                        if (!entity.TryGetComponent(out ServerRevolverBarrelComponent? revolver))
                            continue;

                        Assert.That(revolver.FillPrototype == null ||
                                    protoManager.HasIndex<EntityPrototype>(revolver.FillPrototype), $"{proto.ID} does not have valid FillPrototype");
                    }
                }
            });
            
            await server.WaitIdleAsync();
        }

        /// <summary>
        ///     Verifies the paths to gun sounds exist
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task TestSoundPrototypes()
        {
            var server = StartServerDummyTicker();
            await server.WaitIdleAsync();
            var entityManager = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var protoManager = server.ResolveDependency<IPrototypeManager>();
            var resourceManager = server.ResolveDependency<IResourceManager>();

            server.Assert(() =>
            {
                mapManager.CreateMap(new MapId(1));
                
                foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Components.ContainsKey("BoltActionBarrel"))
                    {
                        var entity = entityManager.SpawnEntity(proto.ID, mapManager.GetMapEntity(new MapId(1)).Transform.MapPosition);

                        if (!entity.TryGetComponent(out ServerBoltActionBarrelComponent? boltAction))
                            continue;

                        foreach (var soundPath in new[]
                        {
                            boltAction.SoundGunshot, 
                            boltAction.SoundEmpty, 
                            boltAction.SoundCycle,
                            boltAction.SoundInsert,
                            boltAction.SoundBoltClosed,
                            boltAction.SoundBoltOpen,
                        })
                        {
                            if (soundPath == null)
                                continue;
                            
                            Assert.That(resourceManager.ContentFileExists(soundPath), $"Unable to find file {soundPath} for {proto.ID}");
                        }

                        continue;
                    }
                    
                    if (proto.Components.ContainsKey("PumpBarrel"))
                    {
                        var entity = entityManager.SpawnEntity(proto.ID, mapManager.GetMapEntity(new MapId(1)).Transform.MapPosition);

                        if (!entity.TryGetComponent(out ServerPumpBarrelComponent? pump))
                            continue;

                        foreach (var soundPath in new[]
                        {
                            pump.SoundGunshot, 
                            pump.SoundEmpty, 
                            pump.SoundCycle,
                            pump.SoundInsert,
                        })
                        {
                            if (soundPath == null)
                                continue;
                            
                            Assert.That(resourceManager.ContentFileExists(soundPath), $"Unable to find file {soundPath} for {proto.ID}");
                        }

                        continue;
                    }
                    
                    if (proto.Components.ContainsKey("RevolverBarrel"))
                    {
                        var entity = entityManager.SpawnEntity(proto.ID, mapManager.GetMapEntity(new MapId(1)).Transform.MapPosition);

                        if (!entity.TryGetComponent(out ServerRevolverBarrelComponent? revolver))
                            continue;

                        foreach (var soundPath in new[]
                        {
                            revolver.SoundGunshot, 
                            revolver.SoundEmpty,
                        })
                        {
                            if (soundPath == null)
                                continue;
                            
                            Assert.That(resourceManager.ContentFileExists(soundPath), $"Unable to find file {soundPath} for {proto.ID}");
                        }
                    }
                }
            });
            
            await server.WaitIdleAsync();
        }
    }
}