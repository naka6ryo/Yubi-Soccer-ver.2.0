using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using YubiSoccer.Field;
using YubiSoccer.Environment;
using Photon.Pun;

namespace YubiSoccer.Game
{
    /// <summary>
    /// ゴール後のリセットを担当。
    /// 既定では3秒後にボールを初期位置へ移動して元に戻す（スコアは保持）。
    /// マルチプレイヤー対応: MasterClient のみがボール位置をリセットし、Photon で同期される。
    /// </summary>
    public class GoalResetManager : MonoBehaviour
    {
        [Header("Reset Policy")]
        [Tooltip("true: シーン再読込で全て初期化。false: 再生成で初期化(スコア保持)")]
        [SerializeField] private bool reloadScene = false;
        [SerializeField, Min(0f)] private float delaySeconds = 3f;

        [Header("Ball Reset (reloadScene=false時)")]
        [Tooltip("ボールのRigidbody。未割当の場合、生成時に自動登録されます")]
        [SerializeField] private Rigidbody ballRigidbody; // Transformでも可
        [Tooltip("リセット先を明示的に指定する Transform。割当がある場合はシーンデフォルトより優先して使われます。")]
        [SerializeField] private Transform explicitInitialTransform;
        [Tooltip("マルチプレイヤー時、MasterClient のみがボールをリセットする")]
        [SerializeField] private bool onlyMasterClientResets = true;
        [Tooltip("true の場合、ゴール後にボールを初期位置にリセットします。Inspector でオフにするとボールはリセットされません（既定: true）")]
        [SerializeField] private bool enableBallReset = true;

        [Header("Breakable Glass Respawn")]
        [Tooltip("自動でシーン内の BreakableProximityGlassSpawner を収集する")]
        [SerializeField] private bool autoFindSpawners = true;
        [SerializeField] private BreakableProximityGlassSpawner[] spawners;
        [Tooltip("ボールリセット後、ガラス再生成までの遅延時間(秒)")]
        [SerializeField, Min(0f)] private float glassRespawnDelay = 0.5f;

        // 試合タイマー連携は Countdown UI 側に移譲（本クラスでは管理しない）

        private Vector3 initialPos;
        private Quaternion initialRot;
        private bool initialPositionSet = false;

        // Scene-level fallback: the position/rotation of the first ball object placed in the scene
        // (used for tutorial/local balls when we want to reset to the scene's original placement)
        private Vector3 sceneDefaultBallPos;
        private Quaternion sceneDefaultBallRot;
        private bool sceneDefaultBallSet = false;

        private SoundManager soundManager;

        private void Awake()
        {
            if (autoFindSpawners)
            {
                spawners = FindObjectsOfType<BreakableProximityGlassSpawner>(true);
            }
            // Try to capture the scene's first-placed ball's transform (hierarchy order)
            TryCaptureSceneDefaultBall();
        }

        void Start()
        {
            soundManager = SoundManager.Instance;
        }

        /// <summary>
        /// ボール生成後に外部から呼び出して、初期位置を登録する
        /// </summary>
        public void RegisterBall(Rigidbody rb)
        {
            if (rb == null)
            {
                Debug.LogWarning("[GoalResetManager] RegisterBall: Rigidbody is null!");
                return;
            }

            ballRigidbody = rb;
            // If this ball is a locally-managed (non-networked) ball and we have a scene-default
            // placement, prefer using that as the recorded initial position so tutorial-local
            // balls return to the scene's original placement.
            bool hasPhotonView = (rb.GetComponent<Photon.Pun.PhotonView>() != null);
            if (!hasPhotonView)
            {
                // 1) Explicitly assigned transform in Inspector takes priority
                if (explicitInitialTransform != null)
                {
                    initialPos = explicitInitialTransform.position;
                    initialRot = explicitInitialTransform.rotation;
                }
                // 2) Scene-captured default fallback
                else if (sceneDefaultBallSet)
                {
                    initialPos = sceneDefaultBallPos;
                    initialRot = sceneDefaultBallRot;
                }
                else
                {
                    initialPos = rb.transform.position;
                    initialRot = rb.transform.rotation;
                }
            }
            else
            {
                initialPos = rb.transform.position;
                initialRot = rb.transform.rotation;
            }
            initialPositionSet = true;
        }

        private void TryCaptureSceneDefaultBall()
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    var found = FindFirstBallInHierarchy(root);
                    if (found != null)
                    {
                        sceneDefaultBallPos = found.transform.position;
                        sceneDefaultBallRot = found.transform.rotation;
                        sceneDefaultBallSet = true;
                        Debug.Log($"[GoalResetManager] Captured scene default ball from '{found.name}' at {sceneDefaultBallPos}");
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[GoalResetManager] TryCaptureSceneDefaultBall failed: " + ex);
            }
        }

        private GameObject FindFirstBallInHierarchy(GameObject go)
        {
            if (go == null) return null;
            // Candidate heuristics: tag == "Ball", name contains "ball", or has a Rigidbody+SphereCollider
            if (IsBallCandidate(go)) return go;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                var f = FindFirstBallInHierarchy(child);
                if (f != null) return f;
            }
            return null;
        }

        private bool IsBallCandidate(GameObject go)
        {
            if (go == null) return false;
            try
            {
                if (!string.IsNullOrEmpty(go.tag) && go.CompareTag("Ball")) return true;
            }
            catch { }
            var name = go.name.ToLowerInvariant();
            if (name.Contains("ball")) return true;
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                var col = go.GetComponent<Collider>();
                if (col is SphereCollider) return true;
            }
            return false;
        }

        private void OnEnable()
        {
            GoalTrigger.OnGoalScored += HandleGoal;
        }

        private void OnDisable()
        {
            GoalTrigger.OnGoalScored -= HandleGoal;
        }

        private void HandleGoal(Team scoredFor)
        {
            StopAllCoroutines();

            // ボールリセットは MasterClient のみ、ガラス再生成は全クライアント
            bool isMaster = !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;
            StartCoroutine(CoResetAfterDelay(isMaster));
        }

        private IEnumerator CoResetAfterDelay(bool resetBall)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            if (reloadScene)
            {
                var scene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(scene.buildIndex);
            }
            else
            {
                // ボールを初期位置へ移動（MasterClient のみが通常は実行） — Inspector で無効化可能
                // ただし「ボールに PhotonView が付与されていない（ローカル／チュートリアル用）場合」は
                // そのクライアントでローカルにリセットを実行できるようにする。
                if (enableBallReset)
                {
                    bool ballHasPhotonView = (ballRigidbody != null && ballRigidbody.GetComponent<Photon.Pun.PhotonView>() != null);
                    // resetBall == true は MasterClient または Photon 未接続時を意味する。
                    if (resetBall || !ballHasPhotonView)
                    {
                        ResetBall();

                        // ボールリセット後、追加の遅延を待つ
                        if (glassRespawnDelay > 0f)
                        {
                            yield return new WaitForSeconds(glassRespawnDelay);
                        }
                    }
                    else
                    {
                        // 非ホストも同じタイミングでガラス再生成できるよう、同じだけ待機
                        float totalWait = delaySeconds + glassRespawnDelay;
                        float alreadyWaited = delaySeconds;
                        float remaining = totalWait - alreadyWaited;
                        if (remaining > 0f)
                        {
                            yield return new WaitForSeconds(remaining);
                        }
                    }
                }
                else
                {
                    // Ball reset が無効なら、他クライアントと同じタイミングで待機
                    float totalWait = delaySeconds + glassRespawnDelay;
                    float alreadyWaited = delaySeconds;
                    float remaining = totalWait - alreadyWaited;
                    if (remaining > 0f)
                    {
                        yield return new WaitForSeconds(remaining);
                    }
                }

                // BreakableProximityGlass の復元(全クライアントで実行)
                ResetExistingGlasses();

                // Spawnerがあれば不足分を再生成(全クライアントで実行)
                RespawnGlasses();

                // マッチタイマーの開始は PreGameCountdown 等の UI 側で行います
            }
        }

        /// <summary>
        /// ボールを初期位置へリセット（Photon 連携時も同期される）
        /// </summary>
        private void ResetBall()
        {
            if (ballRigidbody == null)
            {
                Debug.LogError("[GoalResetManager] ballRigidbody is null! Cannot reset ball.");
                return;
            }
            if (!initialPositionSet)
            {
                Debug.LogWarning("[GoalResetManager] Initial position not set! Attempting to find ball...");

                // 1) まず BallNetworkSync を探す
                var ballSync = FindObjectOfType<YubiSoccer.Network.BallNetworkSync>();
                if (ballSync != null)
                {
                    RegisterBall(ballSync.GetComponent<Rigidbody>());
                }
                else
                {
                    // 2) タグによる検索 (一般的なタグ名を試す)
                    string[] tagsToTry = new[] { "Ball", "SoccerBall" };
                    Rigidbody found = null;
                    foreach (var t in tagsToTry)
                    {
                        try
                        {
                            var gos = GameObject.FindGameObjectsWithTag(t);
                            if (gos != null && gos.Length > 0)
                            {
                                foreach (var g in gos)
                                {
                                    if (g == null) continue;
                                    var rb = g.GetComponent<Rigidbody>();
                                    if (rb != null)
                                    {
                                        found = rb;
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }
                        if (found != null) break;
                    }

                    // 3) 名前・コライダ・Rigidbody ヒューリスティック検索 (最終手段)
                    if (found == null)
                    {
                        var allRbs = Object.FindObjectsOfType<Rigidbody>(true);
                        float bestScore = float.MinValue;
                        foreach (var rb in allRbs)
                        {
                            if (rb == null) continue;
                            float score = 0f;
                            var name = rb.gameObject.name.ToLowerInvariant();
                            if (name.Contains("ball")) score += 10f;
                            // SphereCollider を持つとボールっぽい
                            var sc = rb.GetComponent<Collider>();
                            if (sc != null && sc is SphereCollider) score += 5f;
                            // 物理が有効そうなものを好む
                            if (!rb.isKinematic) score += 2f;
                            // 質量がそこそこなら優先
                            if (rb.mass > 0.1f && rb.mass < 50f) score += 1f;

                            if (score > bestScore)
                            {
                                bestScore = score;
                                found = rb;
                            }
                        }
                    }

                    if (found != null)
                    {
                        RegisterBall(found);
                        Debug.Log("[GoalResetManager] Found ball via fallback search: " + found.name);
                    }
                    else
                    {
                        Debug.LogError("[GoalResetManager] Cannot find ball to reset!");
                        return;
                    }
                }
            }

            // 速度をゼロにする
            ballRigidbody.linearVelocity = Vector3.zero;
            ballRigidbody.angularVelocity = Vector3.zero;

            soundManager.SetSEVolume(10.0f);
            soundManager.PlaySE("ホイッスル01");
            soundManager.SetSEVolume(1.0f);

            // 位置・回転を初期値に戻す（PhotonTransformView/BallNetworkSync があれば自動で同期される）
            ballRigidbody.transform.SetPositionAndRotation(initialPos, initialRot);
        }

        private void ResetExistingGlasses()
        {
            var glasses = FindObjectsOfType<YubiSoccer.Environment.BreakableProximityGlass>(true);
            for (int i = 0; i < glasses.Length; i++)
            {
                var g = glasses[i];
                if (g == null) continue;
                // 可能なら復元
                try { g.ResetIntact(); } catch { }
            }
        }

        private void RespawnGlasses()
        {
            if (spawners == null) return;
            for (int i = 0; i < spawners.Length; i++)
            {
                var s = spawners[i];
                if (s == null) continue;
                s.RespawnAll();
            }
        }

    }
}
