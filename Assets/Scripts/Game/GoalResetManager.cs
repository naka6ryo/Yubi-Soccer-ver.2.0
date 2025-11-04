using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using YubiSoccer.Field;
using YubiSoccer.Environment;

namespace YubiSoccer.Game
{
    /// <summary>
    /// ゴール後のリセットを担当。
    /// 既定では3秒後に再生成で元に戻す（スコアは保持）。必要ならシーン再読込も選択可。
    /// ネットワーク未対応。ローカル用。
    /// </summary>
    public class GoalResetManager : MonoBehaviour
    {
        [Header("Reset Policy")]
        [Tooltip("true: シーン再読込で全て初期化。false: 再生成で初期化(スコア保持)")]
        [SerializeField] private bool reloadScene = false;
        [SerializeField, Min(0f)] private float delaySeconds = 3f;

        [Header("Ball Reset (reloadScene=false時)")]
        [SerializeField] private Rigidbody ballRigidbody; // Transformでも可

        [Header("Breakable Glass Respawn")]
        [Tooltip("自動でシーン内の BreakableProximityGlassSpawner を収集する")]
        [SerializeField] private bool autoFindSpawners = true;
        [SerializeField] private BreakableProximityGlassSpawner[] spawners;

        private Vector3 initialPos;
        private Quaternion initialRot;

        private void Awake()
        {
            if (!reloadScene && ballRigidbody != null)
            {
                initialPos = ballRigidbody.transform.position;
                initialRot = ballRigidbody.transform.rotation;
            }
            if (autoFindSpawners)
            {
                spawners = FindObjectsOfType<BreakableProximityGlassSpawner>(true);
            }
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
            StartCoroutine(CoResetAfterDelay());
        }

        private IEnumerator CoResetAfterDelay()
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
                // ボールを初期位置へ戻す
                if (ballRigidbody != null)
                {
                    ballRigidbody.linearVelocity = Vector3.zero;
                    ballRigidbody.angularVelocity = Vector3.zero;
                    ballRigidbody.transform.SetPositionAndRotation(initialPos, initialRot);
                }
                // BreakableProximityGlass の復元（オブジェクトを保持している場合）
                ResetExistingGlasses();
                // Spawnerがあれば不足分を再生成
                RespawnGlasses();
            }
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
