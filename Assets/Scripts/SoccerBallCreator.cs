using UnityEngine;
using Photon.Pun;
using YubiSoccer.Environment; // BreakableProximityGlass.RegisterBallForAll
using YubiSoccer.UI; // BallOffScreenIndicator.RegisterBallForAll

public class SoccerBallCreator : MonoBehaviour
{
    public string soccerPrefabName = "Soccer Ball";
    private GameObject localSoccerBallInstance;
    void Start()
    {
        // 部屋に入っていない場合はスキップ
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("PlayerSpawner: Not in a room. Skipping player spawn.");
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("SoccerBallSpawner: I am the Master Client. Spawning soccer ball.");
            SpawnLocalSoccerBall();
        }
    }

    void SpawnLocalSoccerBall()
    {
        // プレハブ存在確認
        var prefab = Resources.Load<GameObject>(soccerPrefabName);
        if (prefab == null)
        {
            Debug.LogError($"SoccerBallSpawner: Prefab '{soccerPrefabName}' not found in Resources folder.");
            return;
        }

        // ランダムスポーン位置
        var spawnPos = new Vector3(
            1826.69f,
            12.95f,
            1821.66f
        );

        Debug.Log($"SoccerBallSpawner: Instantiating '{soccerPrefabName}' at {spawnPos}");

        try
        {
            localSoccerBallInstance = PhotonNetwork.Instantiate(soccerPrefabName, spawnPos, Quaternion.identity);
            if (localSoccerBallInstance != null)
            {
                // 生成したボールTransformを全BreakableProximityGlassへ配布（タグ検索不要で安全）
                BreakableProximityGlass.RegisterBallForAll(localSoccerBallInstance.transform);
                // 生成したボールTransformを全BallOffScreenIndicatorへ配布
                BallOffScreenIndicator.RegisterBallForAll(localSoccerBallInstance.transform);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"SoccerBallSpawner: Failed to instantiate soccer ball: {ex}");
        }
    }
}
