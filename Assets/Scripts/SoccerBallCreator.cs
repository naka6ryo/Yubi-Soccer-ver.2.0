using UnityEngine;
using Photon.Pun;
using YubiSoccer.Environment; // BreakableProximityGlass.RegisterBallForAll
using YubiSoccer.UI; // BallOffScreenIndicator.RegisterBallForAll
using YubiSoccer.Game; // GoalResetManager

public class SoccerBallCreator : MonoBehaviour
{
    public string soccerPrefabName = "Soccer Ball";
    private GameObject localSoccerBallInstance;

    private SoundManager soundManager;
    void Start()
    {
        soundManager = SoundManager.Instance;
        // オフラインまたはチュートリアルの場合も実行（ローカルボール生成）
        // マルチプレイ時はMasterClientのみが実行
        bool shouldSpawn = !PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
        if (!shouldSpawn)
        {
            return;
        }

        SpawnLocalSoccerBall();
    }

    public void SpawnLocalSoccerBall()
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

        try
        {
            // オフライン時は通常の Instantiate、オンライン時は PhotonNetwork.Instantiate
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            {
                localSoccerBallInstance = PhotonNetwork.Instantiate(soccerPrefabName, spawnPos, Quaternion.identity);
            }
            else
            {
                // オフライン・チュートリアル時はローカルボール生成
                localSoccerBallInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
            }

            if (localSoccerBallInstance != null)
            {
                // 生成したボールTransformを全BreakableProximityGlassへ配布（タグ検索不要で安全）
                BreakableProximityGlass.RegisterBallForAll(localSoccerBallInstance.transform);
                // 生成したボールTransformを全BallOffScreenIndicatorへ配布
                BallOffScreenIndicator.RegisterBallForAll(localSoccerBallInstance.transform);

                // GoalResetManager へボールを登録（初期位置として記録）
                var goalResetManager = FindObjectOfType<GoalResetManager>();
                if (goalResetManager != null)
                {
                    var rb = localSoccerBallInstance.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        goalResetManager.RegisterBall(rb);
                        Debug.Log("[SoccerBallCreator] Registered ball with GoalResetManager.");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"SoccerBallSpawner: Failed to instantiate soccer ball: {ex}");
        }
    }
}
