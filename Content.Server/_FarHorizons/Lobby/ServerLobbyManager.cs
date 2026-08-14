using System.Linq;
using Content.Server.Preferences.Managers;
using Content.Shared._FarHorizons.Lobby;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.Lobby;

public sealed partial class ServerLobbyManager : SharedLobbyManager, IServerLobbyManager
{
    //[Dependency] private readonly IServerFactionManager _faction = default!; // Strlight, no factions.
    [Dependency] private IServerNetManager _netMan = default!;
    [Dependency] private IServerPreferencesManager _prefMan = default!;

    public new void Init()
    {
        base.Init();

        _netMan.RegisterNetMessage<MsgJobPicksUpdated>();
        _netMan.Connected += Connected;
    }

    private void Connected(object? sender, NetChannelArgs args) => SyncCurrentJobPicks(args.Channel);

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
