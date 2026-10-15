using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.ERT;
using Content.Shared._RMC14.Evacuation;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class ERTMergeCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: RMCERTManualSpawnerTest
          parent: RMCSpawnerERTShuttle
          components:
          - type: GridSpawner
            ignoreGridFill: true
        """;

    [Test]
    public async Task ThunderdomeHasOneStartPadPerFaction()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var expected = new Dictionary<string, Vector2>
        {
            ["RMCERTShuttleStartPadFreelancers"] = new(-21.5f, -118.5f),
            ["RMCERTShuttleStartPadHostile"] = new(-48.5f, -13.5f),
            ["RMCERTShuttleStartPadUNMC"] = new(-62.5f, -116.5f),
            ["RMCERTShuttleStartPadEvent"] = new(-19.5f, -87.5f),
            ["RMCERTShuttleStartPadLaw"] = new(-47.5f, -89.5f),
            ["RMCERTShuttleStartPadSPP"] = new(-17.5f, -56.5f),
            ["RMCERTShuttleStartPadTSE"] = new(-11.5f, -13.5f),
            ["RMCERTShuttleStartPadWeYa"] = new(-39.5f, -51.5f),
        };

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<MapLoaderSystem>().TryLoadGeneric(
                new ResPath("/Maps/_RMC14/thunderdome.yml"), out var loaded), Is.True);
            Assert.That(loaded, Is.Not.Null);

            var pads = loaded!.Entities.Where(entMan.HasComponent<RMCERTShuttleStartPadComponent>).ToArray();
            Assert.That(pads, Has.Length.EqualTo(expected.Count));
            foreach (var (prototype, position) in expected)
            {
                var matches = pads.Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == prototype).ToArray();
                Assert.That(matches, Has.Length.EqualTo(1), prototype);
                Assert.That(entMan.GetComponent<TransformComponent>(matches[0]).LocalPosition, Is.EqualTo(position), prototype);
            }

            Assert.That(loaded.Entities.Any(uid =>
                entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID.StartsWith("RMCSpawnerERTShuttle") == true), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ManualSpawnerDoesNotSpawnEvenWhenIgnoringGridFill()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var gridsBefore = server.EntMan.Count<MapGridComponent>();
            var spawner = server.EntMan.SpawnEntity("RMCERTManualSpawnerTest", map.GridCoords);
            var component = server.EntMan.GetComponent<GridSpawnerComponent>(spawner);

            Assert.That(component.SpawnOnMapInit, Is.False);
            Assert.That(component.IgnoreGridFill, Is.True);
            Assert.That(server.EntMan.Count<MapGridComponent>(), Is.EqualTo(gridsBefore));
        });

        await pair.CleanReturnAsync();
    }
}
