using UnityEngine;
using TMPro;
using YubiSoccer.Game;

namespace YubiSoccer.UI
{
    /// <summary>
    /// リザルト画面と再戦UIの表示と入力を管理
    /// </summary>
    public class ResultUI : MonoBehaviour
    {
        [Header("Result UI References")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private TMP_Text redScoreText;
        [SerializeField] private TMP_Text blueScoreText;

        [Header("Rematch UI References")]
        [SerializeField] private GameObject rematchButton;
        [SerializeField] private GameObject titleButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Hide on Result")]
        [Tooltip("リザルト表示時に非表示にするGameObjectのリスト")]
        [SerializeField] private GameObject[] objectsToHide;

        [Header("Display Settings")]
        [SerializeField] private string redWinMessage = "TEAM A WIN!";
        [SerializeField] private string blueWinMessage = "TEAM B WIN!";
        [SerializeField] private string drawMessage = "DRAW!";
        [SerializeField] private Color redTeamColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color blueTeamColor = new Color(0.2f, 0.5f, 1f);

        public event System.Action OnRematchRequested;
        public event System.Action OnTitleRequested;

        [Header("Sound Effects")]
        [Tooltip("リザルト表示時に順番に再生する SE (3つ)。隙間なく連続再生されます。")]
        [SerializeField] private AudioClip[] resultSoundEffects = new AudioClip[3];

        private AudioSource audioSource;

        // private void OnEnable()
        // {
        //     // MatchTimer の試合終了イベントを購読
        //     MatchTimer.OnMatchFinished += ShowResult;
        // }

        // private void OnDisable()
        // {
        //     // イベント購読解除
        //     MatchTimer.OnMatchFinished -= ShowResult;
        // }

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

            ShowButtons(false);
            ResetTexts();
        }

        /// <summary>
        /// リザルト画面を表示
        /// </summary>
        public void ShowResult(int redScore, int blueScore)
        {
            if (resultPanel == null)
            {
                Debug.LogError("[ResultRematchUI] resultPanel is not assigned!");
                return;
            }

            // 勝敗判定
            string winnerMessage = GetWinnerMessage(redScore, blueScore);

            // UI更新
            UpdateResultTexts(winnerMessage, redScore, blueScore);

            // 指定されたオブジェクトを非表示にする
            HideObjects();

            // パネルとボタンを表示
            resultPanel.SetActive(true);
            ShowButtons(true);
            YubiSoccer.Environment.BreakableProximityGlass.StartShutterForAll();

            // ステータステキストをクリア
            if (statusText != null)
            {
                statusText.text = "";
            }
            // SE を順次再生
            PlayResultSoundEffects();
        }

        /// <summary>
        /// 他のプレイヤーを待っている状態を表示
        /// </summary>
        public void ShowWaitingForPlayers()
        {
            if (statusText != null)
            {
                statusText.text = "他のプレイヤーの同意を待っています...";
            }
            SetButtonsInteractable(false);
        }

        /// <summary>
        /// プレイヤーが退出した通知を表示
        /// </summary>
        public void ShowPlayerLeft()
        {
            if (statusText != null)
            {
                statusText.text = "プレイヤーが退出しました";
            }
            SetButtonsInteractable(true);
        }

        /// <summary>
        /// UIをリセット
        /// </summary>
        public void ResetUI()
        {
            if (statusText != null)
            {
                statusText.text = "";
            }
            SetButtonsInteractable(true);
        }

        /// <summary>
        /// 再戦ボタンのクリックイベントを登録
        /// </summary>
        public void RegisterRematchButton(UnityEngine.UI.Button button)
        {
            if (button != null)
            {
                button.onClick.AddListener(() => OnRematchRequested?.Invoke());
            }
        }

        /// <summary>
        /// タイトルボタンのクリックイベントを登録
        /// </summary>
        public void RegisterTitleButton(UnityEngine.UI.Button button)
        {
            if (button != null)
            {
                button.onClick.AddListener(() => OnTitleRequested?.Invoke());
            }
        }

        private string GetWinnerMessage(int redScore, int blueScore)
        {
            if (redScore > blueScore)
                return redWinMessage;
            else if (blueScore > redScore)
                return blueWinMessage;
            else
                return drawMessage;
        }

        private void UpdateResultTexts(string winner, int redScore, int blueScore)
        {
            if (winnerText != null)
            {
                winnerText.text = winner;
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
        }

        private void ResetTexts()
        {
            if (winnerText != null)
                winnerText.text = "";
            if (redScoreText != null)
                redScoreText.text = "";
            if (blueScoreText != null)
                blueScoreText.text = "";
            if (statusText != null)
                statusText.text = "";
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

        private void ShowButtons(bool show)
        {
            if (rematchButton != null)
                rematchButton.SetActive(show);
            if (titleButton != null)
                titleButton.SetActive(show);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (rematchButton != null)
            {
                var btn = rematchButton.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.interactable = interactable;
            }

            if (titleButton != null)
            {
                var btn = titleButton.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.interactable = interactable;
            }
        }
    }
}