using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Lobby;

public abstract partial class SharedLobbyManager : ISharedLobbyManager
{
    protected Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)> JobPicks = []; // Starlight, no factions

    public virtual void Init() { }

    public virtual void Shutdown() { }

    public event Action? OnJobPicksUpdated;
    protected void CallOnOnJobPicksUpdated() => OnJobPicksUpdated?.Invoke();

    public Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)> GetJobPicks() => JobPicks; // Starlight, no factions
}
