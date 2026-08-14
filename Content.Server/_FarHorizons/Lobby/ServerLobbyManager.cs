using System.Linq;
using Content.Server._NullLink.PlayerData;
using Content.Server.Preferences.Managers;
using Content.Shared._FarHorizons.Lobby;
using Content.Shared._Starlight.Lobby;
using Content.Shared._NullLink;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.Lobby;

public sealed partial class ServerLobbyManager : SharedLobbyManager, IServerLobbyManager
{
    //[Dependency] private readonly IServerFactionManager _faction = default!; // Starlight, no factions.
    [Dependency] private IServerNetManager _netMan = default!;
    [Dependency] private IServerPreferencesManager _prefMan = default!;
    [Dependency] private INullLinkPlayerManager _nullLinkPlayerMan = default!; // Starlight
    [Dependency] private IPlayerManager _playerMan = default!; // Starlight

    public override void Init()
    {
        base.Init();

        _netMan.RegisterNetMessage<MsgJobPicksUpdated>();
        _netMan.RegisterNetMessage<MsgOnlinePlayersUpdated>(); // Starlight
        _netMan.Connected += Connected;
        _nullLinkPlayerMan.PlayerDataChanged += PlayerDataChanged; // Starlight
    }

    public override void Shutdown()
    {
        #region Starlight
        _netMan.Connected -= Connected;
        _nullLinkPlayerMan.PlayerDataChanged -= PlayerDataChanged;
        base.Shutdown();
    }

    private void Connected(object? sender, NetChannelArgs args)
    {
        SyncCurrentJobPicks(args.Channel);
        SyncCurrentOnlinePlayers(args.Channel);
    }

    private void PlayerDataChanged() => SyncCurrentOnlinePlayers();
    #endregion

    private void SyncCurrentJobPicks(INetChannel? target = null)
    {
        var msg = new MsgJobPicksUpdated
        {
            JobPicks = JobPicks
        };

        if (target == null)
            _netMan.ServerSendToAll(msg);
        else
            _netMan.ServerSendMessage(msg, target);
    }

    #region Starlight
    private void SyncCurrentOnlinePlayers(INetChannel? target = null)
    {
        var players = new List<OnlinePlayerInfo>(_playerMan.Sessions.Length);

        foreach (var session in _playerMan.Sessions)
        {
            if (session.Status != SessionStatus.Connected && session.Status != SessionStatus.InGame)
                continue;

            if (_nullLinkPlayerMan.TryGetPlayerData(session.UserId, out var playerData))
            {
                players.Add(new OnlinePlayerInfo(
                    session.Name,
                    playerData.TitleCategory,
                    playerData.Title));
            }
            else
            {
                players.Add(new OnlinePlayerInfo(
                    session.Name,
                    PlayerTitleCategory.Player,
                    null));
            }
        }

        players.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
        OnlinePlayers = players;

        var msg = new MsgOnlinePlayersUpdated
        {
            Players = players
        };

        if (target == null)
            _netMan.ServerSendToAll(msg);
        else
            _netMan.ServerSendMessage(msg, target);
    }
    #endregion

    private void SetJobPicks(Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)> jobPicks) // Starlight, no factions
    {
        if (JobPicks == jobPicks) return;

        JobPicks = jobPicks;
        SyncCurrentJobPicks();
    }

    public void RefreshJobPicks(Dictionary<NetUserId, PlayerGameStatus> players)
    {
        Dictionary<ProtoId<JobPrototype>, (int Low, int Medium, int High)> result = new(); // Starlight, no factions

        var readyPlayers = players.Where(p => p.Value == PlayerGameStatus.ReadyToPlay)
            .Select(p => p.Key).ToList();

        foreach (var pref in readyPlayers.Select(player => _prefMan.GetPreferencesOrNull(player)).Where(p => p != null))
        foreach (var (job, priority) in pref!.JobPrioritiesFiltered()) // Starlight, no factions
        {
            if (priority == JobPriority.Never) continue;

            #region Starlight
            //var assignment = _faction.ListFactionJobs().Where(p => p.Faction == faction && p.Job == job)
            //    .Select(p => (ProtoId<FactionJobAssignmentPrototype>)p.ID).FirstOrNull();

            //if (assignment == null) continue;

            //if (!result.ContainsKey(assignment.Value))
            //    result[assignment.Value] = (0, 0, 0);

            if (!result.TryGetValue(job, out var picks))
                picks = (0, 0, 0);
            #endregion

            switch (priority)
            {
                case JobPriority.Low:
                    picks.Low++; // Starlight
                    break;
                case JobPriority.Medium:
                    picks.Medium++; // Starlight
                    break;
                case JobPriority.High:
                    picks.High++; // Starlight
                    break;
                default:
                    continue;
            }

            result[job] = picks; // Starlight
        }

        SetJobPicks(result);
    }

    public void PreRoundStarted() => RefreshJobPicks(new Dictionary<NetUserId, PlayerGameStatus>());
    public void RoundStarted() => RefreshJobPicks(new Dictionary<NetUserId, PlayerGameStatus>());
}
