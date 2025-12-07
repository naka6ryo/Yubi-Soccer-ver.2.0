using UnityEngine;
using YubiSoccer.Game;
using Photon.Pun;

namespace YubiSoccer.Field
{
    /// <summary>
    /// チュートリアル/オフライン専用のゴールトリガー。Photon に接続していない場合でもゴール処理を実行する。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TutorialGoalTrigger : MonoBehaviour
    {
        [Header("Scoring")]
        [SerializeField] private Team awardToTeam = Team.TeamA;
        [SerializeField] private string ballTag = "Ball";
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
                Debug.LogWarning($"[TutorialGoalTrigger] {name}: Collider.isTrigger を true に設定します。");
                col.isTrigger = true;
            }
        }

        private void Start()
        {
            soundManager = SoundManager.Instance;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!armed) return;

            if (!IsBall(other)) return;

            ProcessGoal();

            if (rearmDelay > 0f)
            {
                armed = false;
                Invoke(nameof(Rearm), rearmDelay);
            }
        }

        private bool IsBall(Collider other)
        {
            if (other == null) return false;

            bool isBall = !string.IsNullOrEmpty(ballTag) && other.CompareTag(ballTag);
            if (isBall) return true;

            var rb = other.attachedRigidbody;
            if (rb != null && rb.gameObject.name.ToLowerInvariant().Contains("ball"))
            {
                return true;
            }

            return false;
        }

        private void ProcessGoal()
        {
            GoalTrigger.OnGoalScored?.Invoke(awardToTeam);

            if (soundManager != null)
            {
                soundManager.PlaySE("ゴール");
                soundManager.SetSEVolume(10.0f);
                soundManager.PlaySE("歓声01");
                soundManager.PlaySE("歓声02");
                soundManager.SetSEVolume(1.0f);
            }
            else
            {
                Debug.LogWarning("[TutorialGoalTrigger] SoundManager が見つかりません。");
            }

            var scoreManager = ScoreManager.Instance;
            if (scoreManager == null)
            {
                Debug.LogWarning("[TutorialGoalTrigger] ScoreManager.Instance が見つかりません。シーンに ScoreManager を配置してください。");
                return;
            }

            if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
            {
                scoreManager.AddScore(awardToTeam, 1);
            }
            else
            {
                scoreManager.AddScoreLocal(awardToTeam, 1);
            }
        }

        private void Rearm()
        {
            armed = true;
        }
    }
}
