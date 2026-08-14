using Content.Shared.Roles;
using Content.Shared._Starlight.Lobby;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Lobby;

public abstract partial class SharedLobbyManager : ISharedLobbyManager
{
    protected Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)> JobPicks = []; // Starlight, no factions
    protected List<OnlinePlayerInfo> OnlinePlayers = []; // Starlight

    public virtual void Init() { }

    public virtual void Shutdown() { }

    public event Action? OnJobPicksUpdated;
    protected void CallOnOnJobPicksUpdated() => OnJobPicksUpdated?.Invoke();

    #region Starlight
    public event Action? OnOnlinePlayersUpdated;
    protected void CallOnOnlinePlayersUpdated() => OnOnlinePlayersUpdated?.Invoke();

    public Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)> GetJobPicks() => JobPicks; // no factions
    public IReadOnlyList<OnlinePlayerInfo> GetOnlinePlayers() => OnlinePlayers;
    #endregion
}
