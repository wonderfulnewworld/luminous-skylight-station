using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Lobby;
namespace Content.Shared._FarHorizons.Lobby;

public interface ISharedLobbyManager
{
    void Init();
    void Shutdown();

    event Action? OnJobPicksUpdated; // Starlight
    event Action? OnOnlinePlayersUpdated;

    Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)> GetJobPicks(); // Starlight, no factions
    IReadOnlyList<OnlinePlayerInfo> GetOnlinePlayers(); // Starlight
}
