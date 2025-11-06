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
        [Tooltip("マルチプレイヤー時、MasterClient のみがボールをリセットする")]
        [SerializeField] private bool onlyMasterClientResets = true;

        [Header("Breakable Glass Respawn")]
        [Tooltip("自動でシーン内の BreakableProximityGlassSpawner を収集する")]
        [SerializeField] private bool autoFindSpawners = true;
        [SerializeField] private BreakableProximityGlassSpawner[] spawners;

        // 試合タイマー連携は Countdown UI 側に移譲（本クラスでは管理しない）

        private Vector3 initialPos;
        private Quaternion initialRot;
        private bool initialPositionSet = false;

        private void Awake()
        {
            if (autoFindSpawners)
            {
                spawners = FindObjectsOfType<BreakableProximityGlassSpawner>(true);
            }
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
            initialPos = rb.transform.position;
            initialRot = rb.transform.rotation;
            initialPositionSet = true;
            Debug.Log($"[GoalResetManager] Ball registered. Initial position: {initialPos}, rotation: {initialRot.eulerAngles}");
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
            Debug.Log($"[GoalResetManager] Goal detected. Starting reset countdown... (IsMasterClient={PhotonNetwork.IsMasterClient}, IsConnected={PhotonNetwork.IsConnected})");
            StopAllCoroutines();

            // ボールリセットは MasterClient のみ、ガラス再生成は全クライアント
            bool isMaster = !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;
            StartCoroutine(CoResetAfterDelay(isMaster));
        }

        private IEnumerator CoResetAfterDelay(bool resetBall)
        {
            Debug.Log($"[GoalResetManager] Waiting {delaySeconds} seconds before reset... (resetBall={resetBall})");
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            Debug.Log($"[GoalResetManager] Delay complete. Starting reset... (reloadScene={reloadScene}, resetBall={resetBall})");

            if (reloadScene)
            {
                var scene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(scene.buildIndex);
            }
            else
            {
                // ボールを初期位置へ移動（MasterClient のみ）
                if (resetBall)
                {
                    ResetBall();
                }

                // BreakableProximityGlass の復元（全クライアントで実行）
                ResetExistingGlasses();

                // Spawnerがあれば不足分を再生成（全クライアントで実行）
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
                // フォールバック: BallNetworkSync を探す
                var ballSync = FindObjectOfType<YubiSoccer.Network.BallNetworkSync>();
                if (ballSync != null)
                {
                    RegisterBall(ballSync.GetComponent<Rigidbody>());
                }
                else
                {
                    Debug.LogError("[GoalResetManager] Cannot find ball to reset!");
                    return;
                }
            }

            Debug.Log($"[GoalResetManager] Resetting ball from {ballRigidbody.transform.position} to {initialPos}");
            Debug.Log($"[GoalResetManager] Current velocity: {ballRigidbody.linearVelocity}, angularVelocity: {ballRigidbody.angularVelocity}");

            // 速度をゼロにする
            ballRigidbody.linearVelocity = Vector3.zero;
            ballRigidbody.angularVelocity = Vector3.zero;

            // 位置・回転を初期値に戻す（PhotonTransformView/BallNetworkSync があれば自動で同期される）
            ballRigidbody.transform.SetPositionAndRotation(initialPos, initialRot);

            Debug.Log($"[GoalResetManager] Ball reset complete. New position: {ballRigidbody.transform.position}");
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
