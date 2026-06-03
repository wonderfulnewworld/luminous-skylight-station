#nullable enable
using System.Linq;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Players;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Minds;

[TestFixture]
public sealed class GhostRoleTests
{
    private const string GhostRoleProtoId = "GhostRoleTestEntity";
    private const string TestMobProtoId = "GhostRoleTestMob";

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: entity
          id: {GhostRoleProtoId}
          components:
          - type: MindContainer
          - type: GhostRole
          - type: GhostTakeoverAvailable
          - type: MobState

        - type: entity
          id: {TestMobProtoId}
          components:
          - type: MobState # MobState is required for correct determination of if the player can return to body or not
        """;

    /// <summary>
    /// This is a simple test that just checks if a player can take a ghost role and then regain control of their
    /// original entity without encountering errors.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task TakeRoleAndReturn(bool adminGhost)
    {
        var ghostCommand = adminGhost ? "aghost" : "ghost";

        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true
        });
        var server = pair.Server;
        var client = pair.Client;

        var mapData = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var sPlayerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var conHost = client.ResolveDependency<IConsoleHost>();
        var mindSystem = entMan.System<SharedMindSystem>();
        var session = sPlayerMan.Sessions.Single();
        var originalPlayerMindId = session.ContentData()!.Mind!.Value;

        TestContext.Progress.WriteLine($"=== TakeRoleAndReturn(adminGhost={adminGhost}, command={ghostCommand}) starting ===");
        TestContext.Progress.WriteLine($"Session: {session.Name} / {session.UserId}");
        TestContext.Progress.WriteLine($"Original player mind id: {originalPlayerMindId}");
        TestContext.Progress.WriteLine($"Initial attached entity: {FormatEntity(entMan, session.AttachedEntity)}");
        LogGhosts(entMan, "Initial ghost state");
        AssertGhostCount(entMan, 0, "initial state before spawning player mob");

        // Spawn player entity & attach
        EntityUid originalPlayerMob = default;
        TestContext.Progress.WriteLine($"Spawning original player mob prototype {TestMobProtoId} at {mapData.GridCoords}.");
        await server.WaitPost(() =>
        {
            originalPlayerMob = entMan.SpawnEntity(TestMobProtoId, mapData.GridCoords);
            TestContext.Progress.WriteLine($"Spawned original player mob: {FormatEntity(entMan, originalPlayerMob)}");
            mindSystem.TransferTo(originalPlayerMindId, originalPlayerMob, true);
            TestContext.Progress.WriteLine($"Transferred original mind {originalPlayerMindId} to {FormatEntity(entMan, originalPlayerMob)}.");
        });

        await pair.RunTicksSync(10);
        var originalPlayerMind = entMan.GetComponent<MindComponent>(originalPlayerMindId);
        TestContext.Progress.WriteLine("After spawning and attaching original player mob:");
        TestContext.Progress.WriteLine($"Session attached entity: {FormatEntity(entMan, session.AttachedEntity)}");
        LogMind("Original player mind", originalPlayerMind);
        LogGhosts(entMan, "Ghost state after original mob attach");

        Assert.Multiple(() =>
        {
            // Check player got attached.
            Assert.That(session.AttachedEntity, Is.EqualTo(originalPlayerMob));
            Assert.That(originalPlayerMind.OwnedEntity, Is.EqualTo(originalPlayerMob));
            Assert.That(originalPlayerMind.VisitingEntity, Is.Null);
            Assert.That(originalPlayerMind.OriginalOwnerUserId, Is.EqualTo(session.UserId));

            // Check that there are still no ghosts
            AssertGhostCount(entMan, 0, "after spawning and attaching original player mob");
        });

        // Use the ghost command
        TestContext.Progress.WriteLine($"Executing ghost command: {ghostCommand}");
        conHost.ExecuteCommand(ghostCommand);
        await pair.RunTicksSync(10);
        var ghostOne = session.AttachedEntity;
        TestContext.Progress.WriteLine("After first ghost command:");
        TestContext.Progress.WriteLine($"ghostOne: {FormatEntity(entMan, ghostOne)}");
        TestContext.Progress.WriteLine($"Session attached entity: {FormatEntity(entMan, session.AttachedEntity)}");
        LogMind("Original player mind", originalPlayerMind);
        LogGhosts(entMan, "Ghost state after first ghost command");

        Assert.Multiple(() =>
        {
            // Assert that the ghost is a new entity with a new mind
            Assert.That(entMan.HasComponent<GhostComponent>(ghostOne));
            Assert.That(ghostOne, Is.Not.EqualTo(originalPlayerMob));
            Assert.That(session.ContentData()?.Mind, Is.EqualTo(originalPlayerMindId));
            if (adminGhost)
            {
                // aghost, so the player mob should still own the mind, but the mind is visiting the ghost.
                Assert.That(originalPlayerMind.OwnedEntity, Is.EqualTo(originalPlayerMob));
                Assert.That(originalPlayerMind.VisitingEntity, Is.EqualTo(ghostOne));
                Assert.That(originalPlayerMind.UserId, Is.EqualTo(session.UserId));
            }
            else
            {
                // player ghost, can't return. The mind is owned by the ghost, and is not visiting.
                Assert.That(originalPlayerMind.OwnedEntity, Is.EqualTo(ghostOne));
                Assert.That(originalPlayerMind.VisitingEntity, Is.Null);
            }

            // Check that we're tracking the original owner for round end screen
            Assert.That(originalPlayerMind.OriginalOwnerUserId, Is.EqualTo(session.UserId));

            // Check that there is only one ghost
            AssertGhostCount(entMan, 1, "after first ghost command");
        });

        // Spawn ghost takeover entity.
        EntityUid ghostRole = default;
        TestContext.Progress.WriteLine($"Spawning ghost role prototype {GhostRoleProtoId}.");
        await server.WaitPost(() =>
        {
            ghostRole = entMan.SpawnEntity(GhostRoleProtoId, mapData.GridCoords);
            TestContext.Progress.WriteLine($"Spawned ghost role entity: {FormatEntity(entMan, ghostRole)}");
        });

        // Take the ghost role
        TestContext.Progress.WriteLine("Taking ghost role.");
        await server.WaitPost(() =>
        {
            var id = entMan.GetComponent<GhostRoleComponent>(ghostRole).Identifier;
            TestContext.Progress.WriteLine($"Ghost role identifier: {id}");
            entMan.EntitySysManager.GetEntitySystem<GhostRoleSystem>().Takeover(session, id);
        });

        // Check player got attached to ghost role.
        await pair.RunTicksSync(10);
        var ghostRoleMindId = session.ContentData()!.Mind!.Value;
        var ghostRoleMind = entMan.GetComponent<MindComponent>(ghostRoleMindId);
        TestContext.Progress.WriteLine("After taking ghost role:");
        TestContext.Progress.WriteLine($"ghostRole: {FormatEntity(entMan, ghostRole)}");
        TestContext.Progress.WriteLine($"ghostOne deleted: {entMan.Deleted(ghostOne)}");
        TestContext.Progress.WriteLine($"Ghost role mind id: {ghostRoleMindId}");
        TestContext.Progress.WriteLine($"Session attached entity: {FormatEntity(entMan, session.AttachedEntity)}");
        LogMind("Original player mind", originalPlayerMind);
        LogMind("Ghost role mind", ghostRoleMind);
        LogGhosts(entMan, "Ghost state after taking ghost role");

        Assert.Multiple(() =>
        {
            // Check that the ghost role mind is new
            Assert.That(ghostRoleMindId, Is.Not.EqualTo(originalPlayerMindId));

            // Check that the session and mind are properly attached to the ghost role
            Assert.That(session.AttachedEntity, Is.EqualTo(ghostRole));
            Assert.That(ghostRoleMind.OwnedEntity, Is.EqualTo(ghostRole));
            Assert.That(ghostRoleMind.VisitingEntity, Is.Null);

            // Original mind should be unaffected, but the ghost will have deleted itself.
            if (adminGhost)
            {
                // aghost case, the original player mob should still own the mind, and that mind is not visiting.
                Assert.That(originalPlayerMind.OwnedEntity, Is.EqualTo(originalPlayerMob));
            }
            else
            {
                // player ghost case, the original mind is disconnected and not owned by an entity.
                // This mind cannot be returned to
                Assert.That(originalPlayerMind.OwnedEntity, Is.Null);
            }

            // In either case the original player mind is not visiting anything, not connected to any user.
            Assert.That(originalPlayerMind.VisitingEntity, Is.Null);
            Assert.That(originalPlayerMind.UserId, Is.Null);

            // Now the original owner of both minds should permanently be set to this session.
            Assert.That(originalPlayerMind.OriginalOwnerUserId, Is.EqualTo(session.UserId));
            Assert.That(ghostRoleMind.OriginalOwnerUserId, Is.EqualTo(session.UserId));

            // Make sure that the ghost was deleted
            Assert.That(entMan.Deleted(ghostOne));

            // Check that there is are no lingereing ghosts
            AssertGhostCount(entMan, 0, "after taking ghost role");
        });

        // Ghost again.
        TestContext.Progress.WriteLine($"Executing second ghost command: {ghostCommand}");
        conHost.ExecuteCommand(ghostCommand);
        await pair.RunTicksSync(10);
        var ghostTwo = session.AttachedEntity;
        TestContext.Progress.WriteLine("After second ghost command:");
        TestContext.Progress.WriteLine($"ghostTwo: {FormatEntity(entMan, ghostTwo)}");
        TestContext.Progress.WriteLine($"ghostOne deleted: {entMan.Deleted(ghostOne)}");
        TestContext.Progress.WriteLine($"Session attached entity: {FormatEntity(entMan, session.AttachedEntity)}");
        LogMind("Original player mind", originalPlayerMind);
        LogMind("Ghost role mind", ghostRoleMind);
        LogGhosts(entMan, "Ghost state after second ghost command");

        Assert.Multiple(() =>
        {
            // Check that the new ghost is a new entity
            Assert.That(entMan.HasComponent<GhostComponent>(ghostTwo));
            Assert.That(ghostTwo, Is.Not.EqualTo(originalPlayerMob));
            Assert.That(ghostTwo, Is.Not.EqualTo(ghostRole));
            Assert.That(session.ContentData()?.Mind, Is.EqualTo(ghostRoleMindId));

            if(adminGhost)
            {
                // aghost case, the ghost role mind should be owned by the ghost role entity,
                // the ghost role mind is visiting the new ghost
                Assert.That(ghostRoleMind.OwnedEntity, Is.EqualTo(ghostRole));
                Assert.That(ghostRoleMind.VisitingEntity, Is.EqualTo(ghostTwo));
            }
            else
            {
                // player ghost, can't return. The mind is owned by the ghost, and is not visiting.
                Assert.That(ghostRoleMind.OwnedEntity, Is.EqualTo(ghostTwo));
                Assert.That(ghostRoleMind.VisitingEntity, Is.Null);
            }

            // Check that the original mind is still not attached to a user
            Assert.That(originalPlayerMind.UserId, Is.Null);

            // Check that original owners of other minds are still tracked
            Assert.That(originalPlayerMind.OriginalOwnerUserId, Is.EqualTo(session.UserId));
            Assert.That(ghostRoleMind.OriginalOwnerUserId, Is.EqualTo(session.UserId));

            // Check that there is exactly one ghost
            AssertGhostCount(entMan, 1, "after second ghost command");
        });

        if (!adminGhost)
        {
            // End of the normal player ghost role test
            TestContext.Progress.WriteLine("Non-admin ghost case complete. Returning pair.");
            await pair.CleanReturnAsync();
            TestContext.Progress.WriteLine("=== TakeRoleAndReturn(adminGhost=False) finished successfully ===");
            return;
        }

        // Next, control the original entity again:
        TestContext.Progress.WriteLine($"Returning to original player mind {originalPlayerMindId} by setting UserId to {session.UserId}.");
        await server.WaitPost(() => mindSystem.SetUserId(originalPlayerMindId, session.UserId));
        await pair.RunTicksSync(10);

        TestContext.Progress.WriteLine("After returning to original player mind:");
        TestContext.Progress.WriteLine($"Session attached entity: {FormatEntity(entMan, session.AttachedEntity)}");
        TestContext.Progress.WriteLine($"ghostTwo deleted: {entMan.Deleted(ghostTwo)}");
        LogMind("Original player mind", originalPlayerMind);
        LogMind("Ghost role mind", ghostRoleMind);
        LogGhosts(entMan, "Ghost state after returning to original player mind");

        Assert.Multiple(() =>
        {
            // Check that we are attached
            Assert.That(session.AttachedEntity, Is.EqualTo(originalPlayerMob));

            // Check the ownership of the original mind
            Assert.That(originalPlayerMind.OwnedEntity, Is.EqualTo(originalPlayerMob));
            Assert.That(originalPlayerMind.VisitingEntity, Is.Null);
            Assert.That(originalPlayerMind.UserId, Is.EqualTo(session.UserId));

            // Check that the ghost-role mind is unaffected
            Assert.That(ghostRoleMind.OwnedEntity, Is.EqualTo(ghostRole));
            Assert.That(ghostRoleMind.VisitingEntity, Is.Null);

            // Check that the second ghost is deleted
            Assert.That(entMan.Deleted(ghostTwo));

            // Check that the original owners of the previous minds are still tracked
            Assert.That(originalPlayerMind.OriginalOwnerUserId, Is.EqualTo(session.UserId));
            Assert.That(ghostRoleMind.OriginalOwnerUserId, Is.EqualTo(session.UserId));

            // Check that there is are no lingereing ghosts
            AssertGhostCount(entMan, 0, "after returning to original player mind");
        });

        TestContext.Progress.WriteLine("Admin ghost case complete. Returning pair.");
        await pair.CleanReturnAsync();
        TestContext.Progress.WriteLine("=== TakeRoleAndReturn(adminGhost=True) finished successfully ===");
    }

    private static void AssertGhostCount(IEntityManager entMan, int expected, string phase)
    {
        var ghosts = entMan.AllEntities<GhostComponent>().ToList();
        TestContext.Progress.WriteLine($"Ghost count during {phase}: expected {expected}, actual {ghosts.Count}");

        if (ghosts.Count != expected)
            TestContext.Progress.WriteLine($"Ghost mismatch during {phase}:\n{DumpGhosts(entMan)}");

        Assert.That(ghosts.Count, Is.EqualTo(expected), $"Ghost count mismatch during {phase}.\n{DumpGhosts(entMan)}");
    }

    private static void LogGhosts(IEntityManager entMan, string phase)
    {
        TestContext.Progress.WriteLine($"{phase}:\n{DumpGhosts(entMan)}");
    }

    private static string DumpGhosts(IEntityManager entMan)
    {
        var ghosts = entMan.AllEntities<GhostComponent>().ToList();

        if (ghosts.Count == 0)
            return "  No ghosts";

        return string.Join("\n", ghosts.Select(ghost =>
        {
            var owner = ghost.Owner;
            var mindText = "no MindContainer";
            if (entMan.TryGetComponent<MindContainerComponent>(owner, out var mindContainer))
                mindText = mindContainer.Mind?.ToString() ?? "null mind";

            return $"  Ghost {FormatEntity(entMan, owner)} | MindContainer.Mind={mindText} | Deleted={entMan.Deleted(owner)}";
        }));
    }

    private static void LogMind(string label, MindComponent mind)
    {
        TestContext.Progress.WriteLine($"{label}: {DumpMind(mind)}");
    }

    private static string DumpMind(MindComponent mind)
    {
        return
            $"OwnedEntity={mind.OwnedEntity}, " +
            $"VisitingEntity={mind.VisitingEntity}, " +
            $"UserId={mind.UserId}, " +
            $"OriginalOwnerUserId={mind.OriginalOwnerUserId}";
    }

    private static string FormatEntity(IEntityManager entMan, EntityUid? uid)
    {
        if (uid == null)
            return "<null>";

        return entMan.Deleted(uid.Value)
            ? $"{uid.Value} <deleted>"
            : entMan.ToPrettyString(uid.Value);
    }
}
