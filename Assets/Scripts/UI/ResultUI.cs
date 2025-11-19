using UnityEngine;
using TMPro;
using YubiSoccer.Game;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 試合終了時のリザルト画面を表示
    /// </summary>
    public class ResultUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private TMP_Text redScoreText;  // TEAM A (赤)
        [SerializeField] private TMP_Text blueScoreText; // TEAM B (青)

        [Header("Hide on Result")]
        [Tooltip("リザルト表示時に非表示にするGameObjectのリスト")]
        [SerializeField] private GameObject[] objectsToHide;

        [Header("Display Settings")]
        [SerializeField] private string redWinMessage = "TEAM A WIN!";
        [SerializeField] private string blueWinMessage = "TEAM B WIN!";
        [SerializeField] private string drawMessage = "DRAW!";
        [SerializeField] private Color redTeamColor = new Color(1f, 0.2f, 0.2f); // 赤
        [SerializeField] private Color blueTeamColor = new Color(0.2f, 0.5f, 1f); // 青

        [Header("Back To Title Button")]
        [Tooltip("リザルト表示時にだけ表示したい戻るボタンの GameObject")]
        [SerializeField] private GameObject backToTitleButton;

        private void OnEnable()
        {
            // MatchTimer の試合終了イベントを購読
            MatchTimer.OnMatchFinished += ShowResult;
        }

        private void OnDisable()
        {
            // イベント購読解除
            MatchTimer.OnMatchFinished -= ShowResult;
        }

        private void Awake()
        {
            // 初期状態では非表示
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            // テキストも初期化（Panelが非表示でも念のため）
            if (winnerText != null)
            {
                winnerText.text = "";
            }
            if (redScoreText != null)
            {
                redScoreText.text = "";
            }
            if (blueScoreText != null)
            {
                blueScoreText.text = "";
            }

            if (backToTitleButton != null)
            {
                backToTitleButton.SetActive(false);
            }
        }

        /// <summary>
        /// リザルト画面を表示
        /// </summary>
        private void ShowResult()
        {
            if (resultPanel == null)
            {
                Debug.LogError("[ResultUI] resultPanel is not assigned!");
                return;
            }

            // ScoreManager からスコア情報を取得
            var scoreManager = ScoreManager.Instance;
            if (scoreManager == null)
            {
                Debug.LogError("[ResultUI] ScoreManager not found!");
                return;
            }

            int redScore = scoreManager.GetScore(Team.TeamA);
            int blueScore = scoreManager.GetScore(Team.TeamB);

            // 勝敗判定
            string winnerMessage;
            if (redScore > blueScore)
            {
                winnerMessage = redWinMessage;
            }
            else if (blueScore > redScore)
            {
                winnerMessage = blueWinMessage;
            }
            else
            {
                winnerMessage = drawMessage;
            }

            // UI更新
            if (winnerText != null)
            {
                winnerText.text = winnerMessage;
            }

            if (redScoreText != null)
            {
                redScoreText.text = redScore.ToString();
                redScoreText.color = redTeamColor;
            }

            if (blueScoreText != null)
            {
                blueScoreText.text = blueScore.ToString();
                blueScoreText.color = blueTeamColor;
            }

            // 指定されたオブジェクトを非表示にする
            HideObjects();

            // パネルを表示
            resultPanel.SetActive(true);

            // リザルトUIと同じタイミングでボタン表示
            if (backToTitleButton != null)
            {
                backToTitleButton.SetActive(true);
            }
        }

        /// <summary>
        /// リストに登録されたオブジェクトを非表示にする
        /// </summary>
        private void HideObjects()
        {
            if (objectsToHide == null || objectsToHide.Length == 0)
                return;

            foreach (var obj in objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }

        /// <summary>
        /// リトライボタン用（任意）
        /// </summary>
        public void OnRetryButtonClicked()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }

        /// <summary>
        /// タイトルに戻るボタン用（任意）
        /// </summary>
        public void OnBackToTitleButtonClicked()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameTitleEdition");
        }
    }
}
