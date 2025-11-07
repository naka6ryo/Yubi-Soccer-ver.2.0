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
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"PlayerSpawner: Failed to instantiate player: {ex}");
        }

        // Instantiate の直後にローカルプレイヤーの詳細ログをオンにする (エディタ/デバッグ用)
        if (localPlayerInstance != null)
        {
            var pc = localPlayerInstance.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.SetVerboseLogging(true);
                Debug.Log("PlayerSpawner: Enabled verbose logging on local player instance.");
            }
            else
            {
                Debug.LogWarning("PlayerSpawner: Instantiated player has no PlayerController component.");
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