using System.Diagnostics;
using UnityEngine;
using YubiSoccer.Game;

namespace YubiSoccer.Field
{
    /// <summary>
    /// ゴール領域のトリガーに付与し、ボール侵入でスコアを加算します。
    /// このゴールに入った時に「どちらのチームに加点するか」を Inspector で指定してください。
    /// 例: TeamB のゴールに付与し、awardToTeam=TeamA とする。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GoalTrigger : MonoBehaviour
    {
        public static System.Action<YubiSoccer.Game.Team> OnGoalScored; // ゴール通知(加点先チーム)
        [Header("Scoring")]
        [Tooltip("このゴールに入ったとき加点されるチーム")]
        [SerializeField] private Team awardToTeam = Team.TeamA;
        [Tooltip("ボール判定に使うタグ名")]
        [SerializeField] private string ballTag = "Ball";
        [Tooltip("同一侵入での多重カウント防止のための再武装ディレイ(秒)")]
        [SerializeField, Min(0f)] private float rearmDelay = 1.0f;

        private Collider col;
        private bool armed = true;

        private SoundManager soundManager;

        private void Reset()
        {
            col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void Awake()
        {
            col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                UnityEngine.Debug.LogWarning($"[GoalTrigger] {name}: Collider.isTrigger を true に設定します。");
                col.isTrigger = true;
            }
            soundManager = SoundManager.Instance;
        }

        private void Update()
        {
            // 1キーを押したらSEを再生
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                soundManager.PlaySE("決定");
                UnityEngine.Debug.Log("[GoalTrigger] Played SE: 決定");
            }

            // 2キーを押したらBGMを再生
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SoundManager.Instance.PlayBGM("タイトル");
            }

            // 3キーを押したらSEを再生
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SoundManager.Instance.PlayBGM("試合中");
            }

            // 4キーを押したらBGMを停止
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SoundManager.Instance.StopBGM();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!armed) return;

            // ボール判定
            bool isBall = !string.IsNullOrEmpty(ballTag) && other.CompareTag(ballTag);
            if (!isBall)
            {
                // タグ未設定のケースのフォールバック: 名称に Ball を含む場合
                var rb = other.attachedRigidbody;
                if (rb != null && rb.gameObject.name.ToLowerInvariant().Contains("ball"))
                {
                    isBall = true;
                }
            }
            if (!isBall) return;

            // ゴールイベント通知（スコア加算前に通知して、UIアニメーションを先に開始）
            OnGoalScored?.Invoke(awardToTeam);
            // soundManager.PlaySE("ゴール");
            SoundManager.Instance.PlaySE("ゴール");
            if(soundManager != null)
            {
                UnityEngine.Debug.Log("[GoalTrigger] Played SE: ゴール");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[GoalTrigger] soundManager が見つかりません。");
            }

            // スコア加算
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(awardToTeam, 1);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[GoalTrigger] ScoreManager.Instance が見つかりません。シーンに ScoreManager を配置してください。");
            }

            // ゴールイベント通知
            try { OnGoalScored?.Invoke(awardToTeam); } catch (System.Exception e) { UnityEngine.Debug.LogException(e); }

            // 再武装までのディレイ
            if (rearmDelay > 0f)
            {
                armed = false;
                Invoke(nameof(Rearm), rearmDelay);
            }
        }

        private void Rearm()
        {
            armed = true;
        }
    }
}
