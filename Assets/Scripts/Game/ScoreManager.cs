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

        [Header("Initial Scores")]
        [SerializeField] private int teamAScore = 0;
        [SerializeField] private int teamBScore = 0;

        public event Action<Team, int> OnScoreChanged; // (team, newScore)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
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
            switch (team)
            {
                case Team.TeamA:
                    teamAScore += delta;
                    OnScoreChanged?.Invoke(Team.TeamA, teamAScore);
                    break;
                case Team.TeamB:
                    teamBScore += delta;
                    OnScoreChanged?.Invoke(Team.TeamB, teamBScore);
                    break;
            }
            UpdateUI();
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
    }
}
