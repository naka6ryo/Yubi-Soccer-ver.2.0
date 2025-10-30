using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

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
    public TMP_Text statusText;

    bool joinAfterConnect = false;

    void Start()
    {
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
        // Auto-instantiate player prefab (requires a "Player" prefab under Assets/Resources)
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            // Before instantiating, ensure the Player prefab exists under Resources (required by DefaultPool)
            var prefab = Resources.Load<GameObject>("Player");
            if (prefab == null)
            {
                Debug.LogError("Player prefab not found in Resources. Ensure 'Assets/Resources/Player.prefab' exists (name must be exactly 'Player'). Skipping instantiate.");
                return;
            }

            // extra diagnostics: log what's in the prefab to aid debugging (Photon DefaultPool errors often stem from prefab internals)
            try
            {
                var comps = prefab.GetComponents<Component>();
                string compList = string.Join(", ", comps.Select(c => c == null ? "<null>" : c.GetType().Name));
                var hasPV = prefab.GetComponent<PhotonView>() != null;
                Debug.Log($"Player prefab info: components=[{compList}] PhotonView={(hasPV ? "yes" : "no")}");

                // simple spawn: random nearby position to avoid exact overlap
                var spawn = new Vector3(UnityEngine.Random.Range(-2f, 2f), 1f, UnityEngine.Random.Range(-2f, 2f));
                Log($"Instantiating Player at {spawn}");

                // wrap instantiate to capture any exceptions coming from Photon/DefaultPool or prefab Awake/Start
                try
                {
                    PhotonNetwork.Instantiate("Player", spawn, Quaternion.identity);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"PhotonNetwork.Instantiate threw exception: {ex}\nPrefab path: Assets/Resources/Player.prefab\nCheck Console for earlier errors from prefab Awake/Start.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while inspecting Player prefab: {ex}");
            }
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Log($"Player entered: {newPlayer.NickName} ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Log($"Disconnected from Photon: {cause}");
    }

    void Log(string text)
    {
        Debug.Log("[NetworkManager] " + text);
        if (statusText != null) statusText.text = text;
    }
}
