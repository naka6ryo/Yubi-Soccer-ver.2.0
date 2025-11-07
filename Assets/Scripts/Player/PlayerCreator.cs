using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// ゲームシーンでローカルプレイヤーを生成するクラス。
/// Multi Player シーンに配置してください（NetworkManager は GameTitle シーンで DontDestroyOnLoad）。
/// </summary>
public class PlayerCreator : MonoBehaviourPunCallbacks
{
    [Header("Spawn Settings")]
    [Tooltip("プレイヤープレハブ名（Resources フォルダ直下）")]
    public string playerPrefabName = "Player";

    [Tooltip("スポーン位置のランダム範囲（X/Z）")]
    public float spawnRadius = 2f;

    [Tooltip("スポーン高さ（Y）")]
    public float spawnHeight = 4.0f;
    [Header("Audio Settings")]
    [Tooltip("ローカルプレイヤーに AudioListener を追加する（カメラに付ける場合は true）")]
    public bool addAudioListenerToCamera = true;

    private GameObject localPlayerInstance;

    void Start()
    {
        // 既にルームにいる場合は即生成（従来の動作を残す）
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (localPlayerInstance == null)
            {
                SpawnLocalPlayer();
            }
            return;
        }

        // ルームに入っていなければ OnJoinedRoom() を待つ
        Debug.Log("PlayerCreator: Not in room yet. Waiting for OnJoinedRoom...");
    }

    /// <summary>
    /// Photon のルーム参加完了コールバック。ここでプレイヤーを生成する。
    /// </summary>
    public override void OnJoinedRoom()
    {
        Debug.Log($"PlayerCreator: OnJoinedRoom called. PlayerCount={PhotonNetwork.CurrentRoom?.PlayerCount}");
        if (localPlayerInstance == null)
        {
            SpawnLocalPlayer();
        }
    }

    void SpawnLocalPlayer()
    {
        // プレハブ存在確認
        var prefab = Resources.Load<GameObject>(playerPrefabName);
        if (prefab == null)
        {
            Debug.LogError($"PlayerSpawner: Prefab '{playerPrefabName}' not found in Resources folder.");
            return;
        }

        // ランダムスポーン位置
        var spawnPos = new Vector3(
            1826.69f,
            8.0f,
            1821.66f
        );

        Debug.Log($"PlayerSpawner: Instantiating '{playerPrefabName}' at {spawnPos}");

        try
        {
            localPlayerInstance = PhotonNetwork.Instantiate(playerPrefabName, spawnPos, Quaternion.identity);
            SetupAudioListener(localPlayerInstance);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"PlayerSpawner: Failed to instantiate player: {ex}");
        }
    }

    /// <summary>
    /// ローカルプレイヤーに AudioListener を追加・有効化する
    /// </summary>
    void SetupAudioListener(GameObject playerInstance)
    {
        if (playerInstance == null)
        {
            Debug.LogWarning("[PlayerCreator] playerInstance is null - skipping AudioListener setup");
            return;
        }

        // PhotonView の確認（念のため）
        var pv = playerInstance.GetComponent<PhotonView>();
        if (pv == null || !pv.IsMine)
        {
            Debug.LogWarning("[PlayerCreator] Player is not mine - skipping AudioListener setup");
            return;
        }

        GameObject targetObject = playerInstance;

        // カメラに AudioListener を付ける場合
        if (addAudioListenerToCamera)
        {
            // PlayerController から PlayerCamera を取得
            var controller = playerInstance.GetComponent<PlayerController>();
            if (controller != null && controller.playerCamera != null)
            {
                targetObject = controller.playerCamera.gameObject;
                Debug.Log("[PlayerCreator] AudioListener will be added to PlayerCamera");
            }
            else
            {
                // フォールバック: "PlayerCamera" という名前の子オブジェクトを探す
                var cameraTransform = playerInstance.transform.Find("PlayerCamera");
                if (cameraTransform != null)
                {
                    targetObject = cameraTransform.gameObject;
                    Debug.Log("[PlayerCreator] AudioListener will be added to PlayerCamera (found by name)");
                }
                else
                {
                    Debug.LogWarning("[PlayerCreator] PlayerCamera not found - adding AudioListener to player root");
                }
            }
        }

        // 既存の AudioListener を確認
        var existingListener = targetObject.GetComponent<AudioListener>();
        if (existingListener != null)
        {
            // 既に存在する場合は有効化のみ
            existingListener.enabled = true;
            Debug.Log($"[PlayerCreator] AudioListener already exists on {targetObject.name} - enabled it");
        }
        else
        {
            // 存在しない場合は追加
            var listener = targetObject.AddComponent<AudioListener>();
            listener.enabled = true;
            Debug.Log($"[PlayerCreator] Added AudioListener to {targetObject.name}");
        }

        // シーン内の他の AudioListener を無効化（安全のため）
        DisableOtherAudioListeners(targetObject);
    }

    /// <summary>
    /// シーン内の他の AudioListener を無効化する（複数存在を防ぐ）
    /// </summary>
    void DisableOtherAudioListeners(GameObject excludeTarget)
    {
        var allListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var listener in allListeners)
        {
            if (listener.gameObject != excludeTarget)
            {
                listener.enabled = false;
                Debug.Log($"[PlayerCreator] Disabled AudioListener on {listener.gameObject.name}");
            }
        }
    }

    void OnDestroy()
    {
        // シーン切替時にローカルプレイヤーを破棄（オプション：必要に応じて有効化）
        // CleanupLocalPlayer();
    }

    /// <summary>
    /// ローカルプレイヤーを手動で破棄するメソッド（必要に応じて呼び出し）
    /// </summary>
    public void CleanupLocalPlayer()
    {
        if (localPlayerInstance == null) return;

        var pv = localPlayerInstance.GetComponent<PhotonView>();
        if (pv != null && PhotonNetwork.InRoom)
        {
            try
            {
                PhotonNetwork.Destroy(localPlayerInstance);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"PlayerSpawner: PhotonNetwork.Destroy failed: {ex}. Falling back to Object.Destroy.");
                Destroy(localPlayerInstance);
            }
        }
        else
        {
            Destroy(localPlayerInstance);
        }

        localPlayerInstance = null;
    }
}