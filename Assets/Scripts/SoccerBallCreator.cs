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
        // 部屋に入っていない場合はスキップ
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("PlayerSpawner: Not in a room. Skipping player spawn.");
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            SpawnLocalSoccerBall();
        }
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
            localSoccerBallInstance = PhotonNetwork.Instantiate(soccerPrefabName, spawnPos, Quaternion.identity);
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
