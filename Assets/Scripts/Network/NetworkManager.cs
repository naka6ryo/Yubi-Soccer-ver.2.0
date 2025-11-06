using System;
using System.Linq;
using System.Collections;
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

    bool joinAfterConnect = false;

    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void Start()
    {
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

        PhotonNetwork.AutomaticallySyncScene = true;

        if (autoConnectOnStart && !PhotonNetwork.IsConnected)
        {
            Log("Connecting to Photon...");
            PhotonNetwork.ConnectUsingSettings();
        }
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
        PhotonNetwork.JoinRandomRoom();
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

    public override void OnCreatedRoom()
    {
        Log("Room created.");
    }

    public override void OnJoinedRoom()
    {
        Log($"Joined room. Players: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}");

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

    void Log(string text)
    {
        Debug.Log("[NetworkManager] " + text);
        if (statusDisplay != null) statusDisplay.UpdateStatus(text);
    }
}
