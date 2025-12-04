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

        [Header("Sound Effects")]
        [Tooltip("リザルト表示時に順番に再生する SE (3つ)。隙間なく連続再生されます。")]
        [SerializeField] private AudioClip[] resultSoundEffects = new AudioClip[3];

        private AudioSource audioSource;

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
            // AudioSource を取得または追加
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.loop = false;

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
                // 色を勝者に合わせて変更（赤勝ち=>赤、青勝ち=>青、引き分け=>白）
                if (redScore > blueScore)
                {
                    winnerText.color = redTeamColor;
                }
                else if (blueScore > redScore)
                {
                    winnerText.color = blueTeamColor;
                }
                else
                {
                    winnerText.color = Color.white;
                }
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
            // 同時にシャッター演出を有効化して、シーン遷移まで破片を保持する
            YubiSoccer.Environment.BreakableProximityGlass.StartShutterForAll();
            resultPanel.SetActive(true);

            // リザルトUIと同じタイミングでボタン表示
            if (backToTitleButton != null)
            {
                backToTitleButton.SetActive(true);
            }

            // SE を順次再生
            PlayResultSoundEffects();
        }

        /// <summary>
        /// リザルト SE を順番に隙間なく再生する
        /// </summary>
        private void PlayResultSoundEffects()
        {
            if (resultSoundEffects == null || resultSoundEffects.Length == 0)
            {
                return;
            }
            StartCoroutine(PlaySoundEffectsSequentially());
        }

        private System.Collections.IEnumerator PlaySoundEffectsSequentially()
        {
            for (int i = 0; i < resultSoundEffects.Length; i++)
            {
                AudioClip clip = resultSoundEffects[i];
                if (clip == null)
                {
                    continue;
                }
                audioSource.clip = clip;
                audioSource.Play();
                // クリップの長さだけ待機（隙間なく次へ）
                yield return new WaitForSeconds(clip.length);
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
