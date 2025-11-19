using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

// Simple NetworkManager using Photon PUN 2.
// - QuickMatch(): join random room (MaxPlayers=2) or create one if none available.
// - Optional 'appId' field to set Photon App ID at runtime (useful if you don't edit PhotonServerSettings).
// Note: Photon PUN 2 package must be imported into the project before using this script.
public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("Photon Settings")]
    [Tooltip("Optional AppId; if set it will override PhotonServerSettings during Play.")]
    public string appId = "";

    [Tooltip("Maximum players per room (use 2 for 1 vs 1).")]
    public byte maxPlayers = 2;

    [Tooltip("If true, connect to Photon on Start().")]
    public bool autoConnectOnStart = true;

    [Header("UI (optional)")]
    
    [Tooltip("ゲーム開始ボタン（マッチングシーンに配置）")]
    public StartGameButton startGameButton;
    [Tooltip("ステータス表示クラス（オプション）")]
    public StatusDisplay statusDisplay;

    [Header("GameScene")]
    [Tooltip("部屋が満員になったらマスターがロードするシーン名。Build Settings に登録してください。")]
    public string gameSceneName = "Multi Player Stadium";

    [Header("MatchingScene")]
    [Tooltip("マッチングシーンの名前")]
    public string matchingSceneName = "Matching";

    [Header("TitleScene")]
    [Tooltip("タイトル（ホーム）シーン名")]
    public string titleSceneName = "GameTitleEdition";

    bool joinAfterConnect = false;
    string pendingRoomName = null; // オリジナルルーム作成/参加用のルーム名を保持
    byte pendingRoomMaxPlayers = 0; // オリジナルルーム作成時の最大人数を保持
    bool isCreatingCustomRoom = false; // ルーム作成中かどうかのフラグ

    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        // タイトルシーンが Build Settings に登録されているか事前検証（再発防止）
        if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            Debug.LogWarning($"[NetworkManager] タイトルシーン '{titleSceneName}' が Build Settings に存在しません。File -> Build Settings で追加してください。");
        }
        else
        {
            Debug.Log($"[NetworkManager] Title scene configured: '{titleSceneName}'");
        }
    }

    void Start()
    {
        PhotonNetwork.GameVersion = "1"; 
        if (PhotonNetwork.InRoom)
        {
            Log($"Already in room: {PhotonNetwork.CurrentRoom.Name}");
            return;
        }

        // If user provided an AppId in the inspector, override the project's PhotonServerSettings at runtime.
        if (!string.IsNullOrEmpty(appId))
        {
            if (PhotonNetwork.PhotonServerSettings != null && PhotonNetwork.PhotonServerSettings.AppSettings != null)
            {
                PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = appId;
                Debug.Log("NetworkManager: Applied AppId from inspector to PhotonServerSettings (runtime only).");
            }
            else
            {
                Debug.LogWarning("NetworkManager: PhotonServerSettings or AppSettings is null. Make sure Photon PUN is imported.");
            }
        }

        if (autoConnectOnStart && !PhotonNetwork.IsConnected)
        {
            Log("Connecting to Photon...");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void QuickMatch2Players()
    {
        maxPlayers = 2;
        QuickMatch();
    }

    public void QuickMatch4Players()
    {
        maxPlayers = 4;
        QuickMatch();
    }

    public void QuickMatch6Players()
    {
        maxPlayers = 6;
        QuickMatch();
    }

    // Quick match entry point (call from UI)
    public void QuickMatch()
    {
        // Ensure we are fully connected to the Master server before attempting matchmaking.
        // PhotonNetwork.IsConnected may be true while still authenticating against the NameServer,
        // so check IsConnectedAndReady which indicates we're ready for operations like JoinRandomRoom.
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.IsConnectedAndReady)
        {
            joinAfterConnect = true;
            Log("Not fully connected yet - connecting to Photon and will join a random room when ready...");
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        Log("Joining random room...");
        PhotonNetwork.JoinRandomRoom(null, maxPlayers);
    }

    /// <summary>
    /// オリジナルルームを作成（UI の InputField から呼ぶ）
    /// オリジナルルームを作成（UI の InputField から呼ぶ）
    /// </summary>
    /// <param name="roomName">作成するルーム名（英数字推奨）</param>
    /// <param name="maxPlayersForRoom">最大人数（2, 4, 6 など）</param>
    public void CreateCustomRoom(string roomName, byte maxPlayersForRoom)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("CreateCustomRoom: Room name cannot be empty!");
            Log("エラー: ルーム名を入力してください");
            return;
        }

        pendingRoomName = roomName;
        pendingRoomMaxPlayers = maxPlayersForRoom;
        isCreatingCustomRoom = true;

        if (!PhotonNetwork.IsConnected || !PhotonNetwork.IsConnectedAndReady)
        {
            joinAfterConnect = true;
            Log($"接続中... ルーム '{roomName}' を作成します（{maxPlayersForRoom}人部屋）");
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        Log($"オリジナルルーム '{roomName}' を作成中...（{maxPlayersForRoom}人部屋）");
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersForRoom,
            IsVisible = false, // ★ ロビーに表示しない（ルーム名を知っている人のみ参加可能）
            IsOpen = true
        };
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }
    
    /// <summary>
    /// オリジナルルームに参加
    /// </summary>
    /// <param name="roomName">参加するルーム名</param>
    public void JoinCustomRoom(string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("JoinCustomRoom: Room name cannot be empty!");
            Log("エラー: ルーム名を入力してください");
            return;
        }

        pendingRoomName = roomName;
        pendingRoomMaxPlayers = 0;
        isCreatingCustomRoom = false;

        if (!PhotonNetwork.IsConnected || !PhotonNetwork.IsConnectedAndReady)
        {
            joinAfterConnect = true;
            Log($"接続中... ルーム '{roomName}' に参加します");
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        Log($"オリジナルルーム '{roomName}' に参加中...");
        PhotonNetwork.JoinRoom(roomName);
    }

    // Photon callbacks
    public override void OnConnectedToMaster()
    {
        Log("Connected to Master server.");
        if (joinAfterConnect)
        {
            joinAfterConnect = false;
            PhotonNetwork.JoinRandomRoom();
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Log($"JoinRandom failed ({returnCode}): {message} - creating a room...");
        var opt = new RoomOptions { MaxPlayers = maxPlayers, IsVisible = true, IsOpen = true };
        PhotonNetwork.CreateRoom(null, opt);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Log($"オリジナルルームへの参加失敗 ({returnCode}): {message}");
        Debug.LogError($"ルーム '{pendingRoomName}' に参加できませんでした。ルーム名が間違っているか、ルームが存在しません。");
        pendingRoomName = null;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Log($"オリジナルルームの作成失敗 ({returnCode}): {message}");
        Debug.LogError($"ルーム '{pendingRoomName}' を作成できませんでした。同じ名前のルームが既に存在する可能性があります。");
        pendingRoomName = null;
    }

    public override void OnCreatedRoom()
    {
        Log($"Room created: {PhotonNetwork.CurrentRoom.Name} ({PhotonNetwork.CurrentRoom.MaxPlayers} max players)");
        pendingRoomName = null;
        pendingRoomMaxPlayers = 0;

        // ルームタイプを設定
        if (TeamManager.Instance != null)
        {
            TeamManager.Instance.SetRoomType("quick");
        }
    }

    public override void OnJoinedRoom()
    {
        Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}. Players: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}");
        pendingRoomName = null;
        pendingRoomMaxPlayers = 0;

        // チーム自動割り当て
        if (TeamManager.Instance != null)
        {
            TeamManager.Instance.AssignTeamOnJoinRoom();
        }

        // マスタークライアントのみがシーン遷移を実行（AutomaticallySyncScene = true により全クライアントが同期）
        if (PhotonNetwork.IsMasterClient)
        {
            Log($"MasterClient loading scene '{matchingSceneName}' (current: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
            try
            {
                PhotonNetwork.LoadLevel(matchingSceneName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to LoadLevel('{matchingSceneName}'): {ex}");
            }
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Log($"Player entered: {newPlayer.NickName} ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");

        if (PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers && PhotonNetwork.IsMasterClient)
        {
            startGameButton.SetVisible(true);
            Log("Room is now full. Master client can start the game.");
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Log($"Disconnected from Photon: {cause}");
    }

    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("StartGame can only be called by the Master Client.");
            return;
        }

        if (PhotonNetwork.CurrentRoom.PlayerCount < PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            Debug.LogWarning("Cannot start game - room is not full yet.");
            return;
        }

        // ルームを閉じて新規参加を防止
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        Log("Room closed - game starting");

        try
        {
            Log($"Master starting game... Loading '{gameSceneName}'");
            var props = new ExitGames.Client.Photon.Hashtable { { "gameStarted", true } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            PhotonNetwork.LoadLevel(gameSceneName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to LoadLevel('{gameSceneName}'): {ex}");
        }
    }

    // タイトルへ戻る公開API（ボタンから呼ぶ）
    public void ReturnToTitleAndDisconnect()
    {
        StartCoroutine(CoReturnToTitle());
    }

    // MOD: 切断＋シーン戻りコルーチン
    private IEnumerator CoReturnToTitle()
    {
        // 以後自動再接続を防ぐ
        joinAfterConnect = false;

        // ルーム離脱
        if (PhotonNetwork.InRoom)
        {
            Log("Leaving room...");
            PhotonNetwork.LeaveRoom();
            float t = 0f;
            while (PhotonNetwork.InRoom && t < 5f)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        // タイトルへ遷移
        Log("Loading title scene: " + titleSceneName);
        SceneManager.LoadScene(titleSceneName);
    }

    void Log(string text)
    {
        Debug.Log("[NetworkManager] " + text);
        if (statusDisplay != null) statusDisplay.UpdateStatus(text);
    }
}
