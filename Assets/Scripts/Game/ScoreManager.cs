using System;
using TMPro;
using UnityEngine;

namespace YubiSoccer.Game
{
    public enum Team
    {
        TeamA = 0,
        TeamB = 1,
    }

    /// <summary>
    /// スコアの集計とTextMeshPro表示を行うマネージャ。
    /// シーンに1つ配置し、A/BそれぞれのTMP_Textを割り当ててください。
    /// </summary>
    public class ScoreManager : MonoBehaviour
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

        [Header("Initial Scores")]
        [SerializeField] private int teamAScore = 0;
        [SerializeField] private int teamBScore = 0;

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
            UpdateUI();
        }

        public void ResetScores(int a = 0, int b = 0)
        {
            teamAScore = Mathf.Max(0, a);
            teamBScore = Mathf.Max(0, b);
            UpdateUI();
        }

        public void AddScore(Team team, int delta = 1)
        {
            delta = Mathf.Max(0, delta);
            soundManager.PlaySE("スコア増加");
            switch (team)
            {
                case Team.TeamA:
                    teamAScore += delta;
                    OnScoreChanged?.Invoke(Team.TeamA, teamAScore);
                    if (enableBlinkOnGoal) StartBlink(Team.TeamA);
                    else UpdateTeamText(Team.TeamA);
                    break;
                case Team.TeamB:
                    teamBScore += delta;
                    OnScoreChanged?.Invoke(Team.TeamB, teamBScore);
                    if (enableBlinkOnGoal) StartBlink(Team.TeamB);
                    else UpdateTeamText(Team.TeamB);
                    break;
            }
            // もう片方は変更なしなので触らない
        }

        public int GetScore(Team team)
        {
            return team == Team.TeamA ? teamAScore : teamBScore;
        }

        private void UpdateUI()
        {
            if (teamAScoreText != null)
                teamAScoreText.text = teamAScore.ToString();
            if (teamBScoreText != null)
                teamBScoreText.text = teamBScore.ToString();
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
