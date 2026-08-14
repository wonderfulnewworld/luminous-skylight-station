using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Lobby;

public interface ISharedLobbyManager
{
    void Init();
    void Shutdown();

    event Action? OnJobPicksUpdated;

    Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)> GetJobPicks(); // Starlight, no factions
}
