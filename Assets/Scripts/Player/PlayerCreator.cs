using UnityEngine;
using Photon.Pun;

/// <summary>
/// ゲームシーンでローカルプレイヤーを生成するクラス。
/// Multi Player シーンに配置してください（NetworkManager は GameTitle シーンで DontDestroyOnLoad）。
/// </summary>
public class PlayerCreator : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("プレイヤープレハブ名（Resources フォルダ直下）")]
    public string playerPrefabName = "Player";

    [Tooltip("スポーン位置のランダム範囲（X/Z）")]
    public float spawnRadius = 2f;

    [Tooltip("スポーン高さ（Y）")]
    public float spawnHeight = 1f;

    private GameObject localPlayerInstance;

    void Start()
    {
        // 部屋に入っていない場合はスキップ
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("PlayerSpawner: Not in a room. Skipping player spawn.");
            return;
        }

        // 既にローカルプレイヤーが存在する場合はスキップ（二重生成防止）
        if (localPlayerInstance != null)
        {
            Debug.LogWarning("PlayerSpawner: Local player already exists.");
            return;
        }

        SpawnLocalPlayer();
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
            Random.Range(-spawnRadius, spawnRadius),
            spawnHeight,
            Random.Range(-spawnRadius, spawnRadius)
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