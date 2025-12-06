using System;
using TMPro;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

namespace YubiSoccer.Game
{
    public enum Team
    {
        TeamA = 0,
        TeamB = 1,
    }

    /// <summary>
    /// スコアの集計とTextMeshPro表示を行うマネージャ。
    /// Photonのルームプロパティを使用してスコアを同期します。
    /// </summary>
    public class ScoreManager : MonoBehaviourPunCallbacks
    {
        public static ScoreManager Instance { get; private set; }

        [Header("UI (TextMeshPro)")]
        [SerializeField] private TMP_Text teamAScoreText;
        [SerializeField] private TMP_Text teamBScoreText;

        [Header("Goal Blink Effect")]
        [Tooltip("ゴール時、スコア表示を点滅させてから更新する")]
        [SerializeField] private bool enableBlinkOnGoal = true;
        [Tooltip("点滅回数（この回数分、非表示→表示を繰り返し、その後更新を表示）")]
        [SerializeField, Min(1)] private int blinkTimes = 2;
        [Tooltip("1回の非表示/表示それぞれの待ち秒数。合計で times*2*interval 秒")]
        [SerializeField, Min(0f)] private float blinkInterval = 0.12f;

        // ローカルキャッシュ（表示用）
        private int teamAScore = 0;
        private int teamBScore = 0;

        // ルームプロパティのキー
        private const string SCORE_A_KEY = "ScoreA";
        private const string SCORE_B_KEY = "ScoreB";

        public event Action<Team, int> OnScoreChanged; // (team, newScore)

        private Coroutine blinkCoA;
        private Coroutine blinkCoB;

        private SoundManager soundManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            soundManager = SoundManager.Instance;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            // 初期化時に現在のルームプロパティからスコアを読み込む
            if (PhotonNetwork.InRoom)
            {
                UpdateScoresFromProperties(PhotonNetwork.CurrentRoom.CustomProperties);
            }
        }

        /// <summary>
        /// スコアをリセット（マスタークライアントのみ実行可能）
        /// </summary>
        public void ResetScores(int a = 0, int b = 0)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var props = new Hashtable
            {
                { SCORE_A_KEY, a },
                { SCORE_B_KEY, b }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        /// <summary>
        /// スコアを加算（マスタークライアントのみ実行可能）
        /// </summary>
        public void AddScore(Team team, int delta = 1)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            delta = Mathf.Max(0, delta);
            int currentA = teamAScore;
            int currentB = teamBScore;

            // プロパティから最新値を取得して加算するのが安全だが、
            // ここではローカルキャッシュ（同期済み）をベースにする
            if (team == Team.TeamA)
            {
                int newScore = currentA + delta;
                var props = new Hashtable { { SCORE_A_KEY, newScore } };
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }
            else
            {
                int newScore = currentB + delta;
                var props = new Hashtable { { SCORE_B_KEY, newScore } };
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }
        }

        public int GetScore(Team team)
        {
            return team == Team.TeamA ? teamAScore : teamBScore;
        }

        /// <summary>
        /// ルームプロパティが更新されたときに呼ばれる（全クライアント）
        /// </summary>
        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            bool changed = false;
            if (propertiesThatChanged.ContainsKey(SCORE_A_KEY))
            {
                int newScore = (int)propertiesThatChanged[SCORE_A_KEY];
                if (newScore != teamAScore)
                {
                    teamAScore = newScore;
                    OnScoreChanged?.Invoke(Team.TeamA, teamAScore);
                    HandleScoreUpdateEffect(Team.TeamA);
                    changed = true;
                }
            }

            if (propertiesThatChanged.ContainsKey(SCORE_B_KEY))
            {
                int newScore = (int)propertiesThatChanged[SCORE_B_KEY];
                if (newScore != teamBScore)
                {
                    teamBScore = newScore;
                    OnScoreChanged?.Invoke(Team.TeamB, teamBScore);
                    HandleScoreUpdateEffect(Team.TeamB);
                    changed = true;
                }
            }
            
            // 初回同期などでエフェクトなしで更新したい場合もあるが、
            // 基本的には UpdateUI は HandleScoreUpdateEffect 内で呼ばれるか、
            // 点滅なしなら即更新される。
        }

        private void UpdateScoresFromProperties(Hashtable props)
        {
            if (props.TryGetValue(SCORE_A_KEY, out object valA))
            {
                teamAScore = (int)valA;
                UpdateTeamText(Team.TeamA);
            }
            if (props.TryGetValue(SCORE_B_KEY, out object valB))
            {
                teamBScore = (int)valB;
                UpdateTeamText(Team.TeamB);
            }
        }

        private void HandleScoreUpdateEffect(Team team)
        {
            // SE再生
            if (soundManager == null) soundManager = SoundManager.Instance;
            if (soundManager != null)
            {
                soundManager.PlaySE("スコア増加");
            }

            // 点滅演出
            if (enableBlinkOnGoal) StartBlink(team);
            else UpdateTeamText(team);
        }

        private void UpdateTeamText(Team team)
        {
            if (team == Team.TeamA)
            {
                if (teamAScoreText != null) teamAScoreText.text = teamAScore.ToString();
            }
            else
            {
                if (teamBScoreText != null) teamBScoreText.text = teamBScore.ToString();
            }
        }

        private void StartBlink(Team team)
        {
            if (blinkTimes < 1 || blinkInterval <= 0f)
            {
                UpdateTeamText(team);
                return;
            }
            if (team == Team.TeamA)
            {
                if (blinkCoA != null) StopCoroutine(blinkCoA);
                blinkCoA = StartCoroutine(CoUpdateThenBlink(teamAScoreText, () => teamAScore));
            }
            else
            {
                if (blinkCoB != null) StopCoroutine(blinkCoB);
                blinkCoB = StartCoroutine(CoUpdateThenBlink(teamBScoreText, () => teamBScore));
            }
        }

        private System.Collections.IEnumerator CoUpdateThenBlink(TMP_Text text, Func<int> getScore)
        {
            if (text == null)
            {
                yield break;
            }
            var baseColor = text.color;
            // 先に新スコアへ更新し、その後に点滅
            text.text = getScore().ToString();
            text.color = baseColor; // 表示状態から開始
            for (int k = 0; k < blinkTimes; k++)
            {
                var c0 = baseColor; c0.a = 0f; text.color = c0; // 非表示
                yield return new WaitForSeconds(blinkInterval);
                text.color = baseColor; // 表示
                yield return new WaitForSeconds(blinkInterval);
            }
        }
    }
}
