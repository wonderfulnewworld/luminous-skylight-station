using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.CCVar;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests.Station;

[TestFixture]
[TestOf(typeof(EmergencyShuttleSystem))]
public sealed class EvacShuttleTest
{
    /// <summary>
    /// Ensure that the emergency shuttle can be called, and that it will travel to centcomm
    /// </summary>
    [Test]
    public async Task EmergencyEvacTest()
    {
        TestContext.Progress.WriteLine("=== EmergencyEvacTest starting ===");

        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        TestContext.Progress.WriteLine("Acquired server/client pair.");
        TestContext.Progress.WriteLine($"Initial RunLevel: {ticker.RunLevel}");
        TestContext.Progress.WriteLine($"Initial entity count: {entMan.EntityCount}");
        TestContext.Progress.WriteLine($"Initial centcomm station count: {entMan.Count<StationCentcommComponent>()}");

        // Dummy ticker tests should not have centcomm
        Assert.That(entMan.Count<StationCentcommComponent>(), Is.Zero);

        Assert.That(pair.Server.CfgMan.GetCVar(CCVars.GridFill), Is.False);
        pair.Server.CfgMan.SetCVar(CCVars.EmergencyShuttleEnabled, true);
        pair.Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        var gameMap = pair.Server.CfgMan.GetCVar(CCVars.GameMap);
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, "StarlightCog"); //Starlight edit to a map we actually have

        TestContext.Progress.WriteLine(
            $"Configured CVars: EmergencyShuttleEnabled={pair.Server.CfgMan.GetCVar(CCVars.EmergencyShuttleEnabled)}, " +
            $"GameDummyTicker={pair.Server.CfgMan.GetCVar(CCVars.GameDummyTicker)}, " +
            $"GameMap={pair.Server.CfgMan.GetCVar(CCVars.GameMap)} (previously {gameMap})");

        TestContext.Progress.WriteLine("Calling RestartRound().");
        await server.WaitPost(() => ticker.RestartRound());
        TestContext.Progress.WriteLine("RestartRound() returned. Running startup ticks.");

        await pair.RunTicksSync(25);
        TestContext.Progress.WriteLine($"After startup ticks: RunLevel={ticker.RunLevel}, EntityCount={entMan.EntityCount}");
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        // Find the station, centcomm, and shuttle, and ftl map.
        var centcommStationCount = entMan.Count<StationCentcommComponent>();
        var stationEmergencyShuttleCount = entMan.Count<StationEmergencyShuttleComponent>();
        var stationDataCount = entMan.Count<StationDataComponent>();
        var emergencyShuttleCount = entMan.Count<EmergencyShuttleComponent>();
        var ftlMapCount = entMan.Count<FTLMapComponent>();

        TestContext.Progress.WriteLine(
            "Post-start component counts: " +
            $"StationCentcomm={centcommStationCount}, " +
            $"StationEmergencyShuttle={stationEmergencyShuttleCount}, " +
            $"StationData={stationDataCount}, " +
            $"EmergencyShuttle={emergencyShuttleCount}, " +
            $"FTLMap={ftlMapCount}");

        Assert.That(centcommStationCount, Is.EqualTo(1));
        Assert.That(stationEmergencyShuttleCount, Is.EqualTo(1));
        Assert.That(stationDataCount, Is.EqualTo(1));
        Assert.That(emergencyShuttleCount, Is.EqualTo(1));
        Assert.That(ftlMapCount, Is.EqualTo(0));

        var station = (Entity<StationCentcommComponent>) entMan.AllComponentsList<StationCentcommComponent>().Single();
        var data = entMan.GetComponent<StationDataComponent>(station);
        var shuttleData = entMan.GetComponent<StationEmergencyShuttleComponent>(station);

        TestContext.Progress.WriteLine($"Station entity: {entMan.ToPrettyString(station)}");
        TestContext.Progress.WriteLine($"Station grid count: {data.Grids.Count}");

        var saltern = data.Grids.Single();
        TestContext.Progress.WriteLine($"Station grid: {entMan.ToPrettyString(saltern)}");
        Assert.That(entMan.HasComponent<MapGridComponent>(saltern));

        var shuttle = shuttleData.EmergencyShuttle!.Value;
        TestContext.Progress.WriteLine($"Emergency shuttle entity: {entMan.ToPrettyString(shuttle)}");
        Assert.That(entMan.HasComponent<EmergencyShuttleComponent>(shuttle));
        Assert.That(entMan.HasComponent<MapGridComponent>(shuttle));

        var centcomm = station.Comp.Entity!.Value;
        TestContext.Progress.WriteLine($"Centcomm grid entity: {entMan.ToPrettyString(centcomm)}");
        Assert.That(entMan.HasComponent<MapGridComponent>(centcomm));

        var centcommMap = station.Comp.MapEntity!.Value;
        TestContext.Progress.WriteLine($"Centcomm map entity: {entMan.ToPrettyString(centcommMap)}");
        Assert.That(entMan.HasComponent<MapComponent>(centcommMap));
        Assert.That(server.Transform(centcomm).MapUid, Is.EqualTo(centcommMap));

        var salternXform = server.Transform(saltern);
        TestContext.Progress.WriteLine($"Station grid map: {salternXform.MapUid}");
        Assert.That(salternXform.MapUid, Is.Not.Null);
        Assert.That(salternXform.MapUid, Is.Not.EqualTo(centcommMap));

        var shuttleXform = server.Transform(shuttle);
        TestContext.Progress.WriteLine($"Initial shuttle map: {shuttleXform.MapUid}");
        Assert.That(shuttleXform.MapUid, Is.Not.Null);
        Assert.That(shuttleXform.MapUid, Is.EqualTo(centcommMap));

        // All of these should have been map-initialized.
        var mapSys = entMan.System<SharedMapSystem>();
        TestContext.Progress.WriteLine(
            $"Map state before evac: centcomm initialized={mapSys.IsInitialized(centcommMap)}, " +
            $"station initialized={mapSys.IsInitialized(salternXform.MapUid)}, " +
            $"centcomm paused={mapSys.IsPaused(centcommMap)}, " +
            $"station paused={mapSys.IsPaused(salternXform.MapUid!.Value)}");

        Assert.That(mapSys.IsInitialized(centcommMap), Is.True);
        Assert.That(mapSys.IsInitialized(salternXform.MapUid), Is.True);
        Assert.That(mapSys.IsPaused(centcommMap), Is.False);
        Assert.That(mapSys.IsPaused(salternXform.MapUid!.Value), Is.False);

        EntityLifeStage LifeStage(EntityUid uid) => entMan.GetComponent<MetaDataComponent>(uid).EntityLifeStage;
        TestContext.Progress.WriteLine(
            "Life stages: " +
            $"stationGrid={LifeStage(saltern)}, " +
            $"shuttle={LifeStage(shuttle)}, " +
            $"centcommGrid={LifeStage(centcomm)}, " +
            $"centcommMap={LifeStage(centcommMap)}, " +
            $"stationMap={LifeStage(salternXform.MapUid.Value)}");

        Assert.That(LifeStage(saltern), Is.EqualTo(EntityLifeStage.MapInitialized));
        Assert.That(LifeStage(shuttle), Is.EqualTo(EntityLifeStage.MapInitialized));
        Assert.That(LifeStage(centcomm), Is.EqualTo(EntityLifeStage.MapInitialized));
        Assert.That(LifeStage(centcommMap), Is.EqualTo(EntityLifeStage.MapInitialized));
        Assert.That(LifeStage(salternXform.MapUid.Value), Is.EqualTo(EntityLifeStage.MapInitialized));

        // Set up shuttle timing
        var shuttleSys = server.System<ShuttleSystem>();
        var evacSys = server.System<EmergencyShuttleSystem>();
        evacSys.TransitTime = shuttleSys.DefaultTravelTime; // Absolute minimum transit time, so the test has to run for at least this long
        // TODO SHUTTLE fix spaghetti

        var dockTime = server.CfgMan.GetCVar(CCVars.EmergencyShuttleDockTime);
        server.CfgMan.SetCVar(CCVars.EmergencyShuttleDockTime, 2);

        TestContext.Progress.WriteLine(
            $"Shuttle timings configured: DefaultTravelTime={shuttleSys.DefaultTravelTime}, " +
            $"EvacTransitTime={evacSys.TransitTime}, " +
            $"DockTime={server.CfgMan.GetCVar(CCVars.EmergencyShuttleDockTime)} (previously {dockTime})");

        // Call evac shuttle.
        TestContext.Progress.WriteLine("Calling emergency shuttle with command: callshuttle 0:02");
        await pair.WaitCommand("callshuttle 0:02");
        TestContext.Progress.WriteLine("callshuttle command completed. Waiting 3 seconds for station arrival.");

        await pair.RunSeconds(3);
        shuttleXform = server.Transform(shuttle);
        salternXform = server.Transform(saltern);
        TestContext.Progress.WriteLine(
            $"After 3s arrival wait: shuttle map={shuttleXform.MapUid}, station map={salternXform.MapUid}, centcomm map={centcommMap}");

        // Shuttle should have arrived on the station
        Assert.That(shuttleXform.MapUid, Is.EqualTo(salternXform.MapUid));

        TestContext.Progress.WriteLine("Waiting 2 seconds for shuttle to leave station / enter FTL.");
        await pair.RunSeconds(2);

        shuttleXform = server.Transform(shuttle);
        var ftlCountAfterDeparture = entMan.Count<FTLMapComponent>();
        TestContext.Progress.WriteLine(
            $"After 2s departure wait: FTLMap count={ftlCountAfterDeparture}, shuttle map={shuttleXform.MapUid}");

        // Shuttle should be FTLing back to centcomm
        Assert.That(ftlCountAfterDeparture, Is.EqualTo(1));
        var ftl = (Entity<FTLMapComponent>) entMan.AllComponentsList<FTLMapComponent>().Single();
        TestContext.Progress.WriteLine($"FTL map entity: {entMan.ToPrettyString(ftl)}");

        Assert.That(entMan.HasComponent<MapComponent>(ftl));
        Assert.That(ftl.Owner, Is.Not.EqualTo(centcommMap));
        Assert.That(ftl.Owner, Is.Not.EqualTo(salternXform.MapUid));
        Assert.That(shuttleXform.MapUid, Is.EqualTo(ftl.Owner));

        // Shuttle should have arrived at centcomm
        TestContext.Progress.WriteLine($"Waiting {shuttleSys.DefaultTravelTime} seconds for shuttle to finish FTL and reach centcomm.");
        await pair.RunSeconds(shuttleSys.DefaultTravelTime);

        shuttleXform = server.Transform(shuttle);
        TestContext.Progress.WriteLine(
            $"After FTL wait: shuttle map={shuttleXform.MapUid}, centcomm map={centcommMap}, RunLevel={ticker.RunLevel}");
        Assert.That(shuttleXform.MapUid, Is.EqualTo(centcommMap));

        // Round should be ending now
        TestContext.Progress.WriteLine($"Checking final RunLevel. Current RunLevel={ticker.RunLevel}");
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));

        TestContext.Progress.WriteLine("Restoring CVars and cleaning pair.");
        server.CfgMan.SetCVar(CCVars.EmergencyShuttleDockTime, dockTime);
        pair.Server.CfgMan.SetCVar(CCVars.EmergencyShuttleEnabled, false);
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, gameMap);
        await pair.CleanReturnAsync();

        TestContext.Progress.WriteLine("=== EmergencyEvacTest finished successfully ===");
    }
}
