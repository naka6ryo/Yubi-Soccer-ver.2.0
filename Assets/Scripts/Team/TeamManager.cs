using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using YubiSoccer.Game;

/// <summary>
/// チーム管理システム
/// - クイックマッチ: 自動割り当て
/// - プライベートルーム: 手動選択可能
/// </summary>
public class TeamManager : MonoBehaviourPunCallbacks
{
    public static TeamManager Instance { get; private set; }

    [Header("Team Settings")]
    [Tooltip("チームA（赤）のプレイヤープレハブ名（Resources フォルダ内）")]
    public string teamAPrefabName = "PlayerRed";
    [Tooltip("チームB（青）のプレイヤープレハブ名（Resources フォルダ内）")]
    public string teamBPrefabName = "PlayerBlue";

    [Header("Auto Assignment")]
    [Tooltip("自動割り当てモード: ActorNumber=奇数でチームA、偶数でチームB")]
    public bool useAutoAssignment = true;

    // Photon Custom Properties のキー
    private const string TEAM_PROPERTY_KEY = "team";
    private const string ROOM_TYPE_KEY = "roomType";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ルーム参加時にチームを自動割り当て
    /// </summary>
    public void AssignTeamOnJoinRoom()
    {
        if (!PhotonNetwork.InRoom) return;

        string roomType = GetRoomType();

        if (roomType == "private")
        {
            Debug.Log("[TeamManager] Private room detected. Waiting for manual team selection.");
            return;
        }

        if (useAutoAssignment)
        {
            AssignTeamAutomatically();
        }
    }

    /// <summary>
    /// ActorNumber ベースの自動チーム割り当て
    /// </summary>
    private void AssignTeamAutomatically()
    {
        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        Team team = (actorNumber % 2 == 1) ? Team.TeamA : Team.TeamB;

        SetPlayerTeam(team);

        Debug.Log($"[TeamManager] Auto-assigned to {team} (Actor {actorNumber})");
    }

    /// <summary>
    /// プレイヤーのチームを設定（ローカル & ネットワーク同期）
    /// </summary>
    public void SetPlayerTeam(Team team)
    {
        var props = new Hashtable
        {
            { TEAM_PROPERTY_KEY, team.ToString() }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log($"[TeamManager] Set local player team to {team}");
    }

    /// <summary>
    /// ローカルプレイヤーのチームを取得
    /// </summary>
    public Team GetLocalPlayerTeam()
    {
        return GetPlayerTeam(PhotonNetwork.LocalPlayer);
    }

    /// <summary>
    /// 指定プレイヤーのチームを取得
    /// </summary>
    public Team GetPlayerTeam(Player player)
    {
        if (player == null) return Team.TeamA;

        object teamObj;
        if (player.CustomProperties.TryGetValue(TEAM_PROPERTY_KEY, out teamObj))
        {
            string teamStr = teamObj as string;
            if (System.Enum.TryParse(teamStr, out Team team))
            {
                return team;
            }
        }

        return (player.ActorNumber % 2 == 1) ? Team.TeamA : Team.TeamB;
    }

    /// <summary>
    /// チームに応じたプレハブ名を取得
    /// </summary>
    public string GetTeamPrefabName(Team team)
    {
        return team == Team.TeamA ? teamAPrefabName : teamBPrefabName;
    }

    /// <summary>
    /// ルーム内の全プレイヤーのチーム情報を取得
    /// </summary>
    public Dictionary<Player, Team> GetAllPlayerTeams()
    {
        var teams = new Dictionary<Player, Team>();
        foreach (var player in PhotonNetwork.PlayerList)
        {
            teams[player] = GetPlayerTeam(player);
        }
        return teams;
    }

    /// <summary>
    /// 各チームの人数を取得
    /// </summary>
    public (int teamACount, int teamBCount) GetTeamCounts()
    {
        int teamA = 0;
        int teamB = 0;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            Team team = GetPlayerTeam(player);
            if (team == Team.TeamA) teamA++;
            else teamB++;
        }

        return (teamA, teamB);
    }

    /// <summary>
    /// チームの人数が同じかどうか
    /// </summary>
    public bool IsBalanced()
    {
        var (teamA, teamB) = GetTeamCounts();
        return Mathf.Abs(teamA - teamB) <= 1;
    }

    /// <summary>
    /// ルームタイプを設定（マスタークライアントのみ）
    /// </summary>
    public void SetRoomType(string roomType)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var props = new Hashtable
        {
            { ROOM_TYPE_KEY, roomType }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log($"[TeamManager] Set room type to '{roomType}'");
    }

    /// <summary>
    /// 現在のルームタイプを取得
    /// </summary>
    public string GetRoomType()
    {
        if (!PhotonNetwork.InRoom) return "unknown";

        object roomTypeObj;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_TYPE_KEY, out roomTypeObj))
        {
            return roomTypeObj as string;
        }

        return "quick";
    }
}
