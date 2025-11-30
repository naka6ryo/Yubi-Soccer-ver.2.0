using System.Collections;
using TMPro;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YubiSoccer.UI
{
    /// <summary>
    /// シーン読み込み時に一度だけ「MATCHING」等の表示を GoalAnnouncementUI と同様の
    /// スライド/フェードアニメで表示するコントローラ。
    /// - goal と同様に State UI を一時的に隠します。
    /// - 自動で表示したくない場合は `autoPlay` を false にしてください。
    /// </summary>
    public class MatchingUIController : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("MATCHING を表示する RectTransform（アンカー中央想定）")]
        [SerializeField] private RectTransform matchingRect;
        [Tooltip("表示するテキスト（任意）。未割当なら文字列のみ変更しない")]
        [SerializeField] private TMP_Text matchingText;
        [Tooltip("表示時に隠す STATE UI の CanvasGroup（任意）")]
        [SerializeField] private CanvasGroup stateCanvasGroup;
        [Tooltip("表示時に隠す STATE UI の GameObject（任意）")]
        [SerializeField] private GameObject stateRootGameObject;

        [Header("Text & Color")]
        [SerializeField] private string matchingString = "MATCHING";
        [SerializeField] private Color matchingColor = Color.white;

        [Header("Animation (sec)")]
        [SerializeField, Min(0f)] private float slideInDuration = 0.35f;
        [SerializeField, Min(0f)] private float holdDuration = 0.8f;
        [SerializeField, Min(0f)] private float slideOutDuration = 0.35f;
        [Tooltip("位置イージング")]
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Animation Style")]
        [Tooltip("true: 中央でフェードイン、false: 右からスライドイン")]
        [SerializeField] private bool useFadeInsteadOfSlide = false;

        [Header("Positions (relative)")]
        [SerializeField] private bool useCanvasEdge = true;
        [SerializeField] private float startOffsetRight = 800f;
        [SerializeField] private float endOffsetLeft = 800f;

        [Header("Visibility")]
        [Tooltip("シーン読み込み時に自動で表示するか")]
        [SerializeField] private bool autoPlay = true;
        [Tooltip("表示前に自動で matchingRect を無効化するか")]
        [SerializeField] private bool autoToggleActive = true;

        [Header("Timing")]
        [Tooltip("シーン開始時に表示を開始するまでの待機時間（秒）")]
        [SerializeField] private float startDelay = 0.2f;

        [Header("Players Display")]
        [Tooltip("マッチング中に表示する人数テキスト (フォーマット: 残り人数/ルーム最大人数) ")]
        [SerializeField] private TMP_Text playersText;
        [Tooltip("プレイヤー表示の更新間隔（秒）")]
        [SerializeField] private float playersUpdateInterval = 0.5f;

        private Vector2 centerAnchoredPos = Vector2.zero;
        private Canvas parentCanvas;
        private CanvasGroup matchingCg;
        private Coroutine playersUpdateCoroutine;

        private void Awake()
        {
            if (matchingRect == null)
            {
                Debug.LogWarning("[MatchingUIController] matchingRect が未割当です。動作しません。");
                enabled = false;
                return;
            }

            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindObjectOfType<Canvas>();
                if (parentCanvas != null)
                {
                    Debug.LogWarning($"[MatchingUIController] 親Canvasが見つからなかったため、シーン内の最初の Canvas をフォールバックとして使用します: {parentCanvas.name}");
                }
                else
                {
                    Debug.LogWarning("[MatchingUIController] 親Canvasが見つかりません。useCanvasEdge は機能しません。");
                }
            }

            centerAnchoredPos = Vector2.zero;
            matchingCg = matchingRect.GetComponent<CanvasGroup>();
            if (matchingCg == null) matchingCg = matchingRect.gameObject.AddComponent<CanvasGroup>();
            matchingCg.alpha = 0f;

            if (matchingText != null && !string.IsNullOrEmpty(matchingString)) matchingText.text = matchingString;
            if (matchingText != null) matchingText.color = matchingColor;

            if (useFadeInsteadOfSlide)
            {
                matchingRect.anchoredPosition = centerAnchoredPos;
            }
            else
            {
                float rightOffset = GetStartOffsetRight();
                matchingRect.anchoredPosition = centerAnchoredPos + Vector2.right * rightOffset;
            }

            if (autoToggleActive) matchingRect.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (autoPlay)
            {
                StartCoroutine(PlayOnceOnStart());
            }
        }

        private IEnumerator PlayOnceOnStart()
        {
            // Wait a frame to ensure scene setup completed
            yield return null;
            if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);
            if (autoPlay) StartCoroutine(CoPlay());
        }

        /// <summary>
        /// 公開: 外部から手動で再生したい場合に呼ぶ
        /// </summary>
        public void PlayMatching()
        {
            StartCoroutine(CoPlay());
        }

        private IEnumerator CoPlay()
        {
            // Hide state UI
            SetStateVisible(false);

            if (matchingText != null)
            {
                matchingText.text = matchingString;
                matchingText.color = matchingColor;
            }

            // Start updating players display while matching UI is visible
            if (playersText != null)
            {
                UpdatePlayersDisplay();
                if (playersUpdateCoroutine == null) playersUpdateCoroutine = StartCoroutine(CoUpdatePlayersLoop());
            }

            if (autoToggleActive) matchingRect.gameObject.SetActive(true);

            if (useFadeInsteadOfSlide)
            {
                matchingCg.alpha = 0f;
                yield return FadeIn(matchingCg, slideInDuration);
            }
            else
            {
                float rightOffset = GetStartOffsetRight();
                Vector2 startPos = centerAnchoredPos + Vector2.right * rightOffset;
                matchingCg.alpha = 1f;
                yield return Slide(matchingRect, startPos, centerAnchoredPos, slideInDuration);
            }

            if (holdDuration > 0f) yield return new WaitForSecondsRealtime(holdDuration);

            // Slide out to left
            float leftOffset = GetEndOffsetLeft();
            Vector2 endPos = centerAnchoredPos + Vector2.left * leftOffset;
            yield return Slide(matchingRect, centerAnchoredPos, endPos, slideOutDuration);

            matchingCg.alpha = 0f;
            if (autoToggleActive) matchingRect.gameObject.SetActive(false);
            // stop updating players display
            if (playersUpdateCoroutine != null)
            {
                try { StopCoroutine(playersUpdateCoroutine); } catch { }
                playersUpdateCoroutine = null;
            }

            SetStateVisible(true);
        }

        private System.Collections.IEnumerator CoUpdatePlayersLoop()
        {
            while (true)
            {
                UpdatePlayersDisplay();
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, playersUpdateInterval));
            }
        }

        private void UpdatePlayersDisplay()
        {
            if (playersText == null) return;
            try
            {
                if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
                {
                    int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
                    int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
                    int remaining = Mathf.Max(0, maxPlayers - playerCount);
                    playersText.text = $"{remaining}/{maxPlayers}";
                }
                else
                {
                    // Not in room: show placeholder
                    playersText.text = $"-/-";
                }
            }
            catch
            {
                playersText.text = "-/-";
            }
        }

        private IEnumerator Slide(RectTransform rt, Vector2 from, Vector2 to, float dur)
        {
            dur = Mathf.Max(0f, dur);
            if (dur == 0f)
            {
                rt.anchoredPosition = to;
                yield break;
            }
            float t = 0f;
            while (t < dur)
            {
                float u = t / dur;
                float e = ease != null ? ease.Evaluate(u) : u;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            rt.anchoredPosition = to;
        }

        private IEnumerator FadeIn(CanvasGroup cg, float dur)
        {
            dur = Mathf.Max(0f, dur);
            if (dur == 0f)
            {
                cg.alpha = 1f;
                yield break;
            }
            float t = 0f;
            while (t < dur)
            {
                float u = t / dur;
                float e = ease != null ? ease.Evaluate(u) : u;
                cg.alpha = Mathf.Lerp(0f, 1f, e);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            cg.alpha = 1f;
        }

        private void SetStateVisible(bool visible)
        {
            if (stateCanvasGroup != null)
            {
                stateCanvasGroup.alpha = visible ? 1f : 0f;
                stateCanvasGroup.interactable = visible;
                stateCanvasGroup.blocksRaycasts = visible;
            }
            if (stateRootGameObject != null)
            {
                stateRootGameObject.SetActive(visible);
            }
        }

        private float GetStartOffsetRight()
        {
            if (!useCanvasEdge || parentCanvas == null) return startOffsetRight;
            var canvasRect = parentCanvas.GetComponent<RectTransform>();
            if (canvasRect == null) return startOffsetRight;
            float canvasWidth = canvasRect.rect.width;
            float textWidth = matchingRect.rect.width;
            float offset = (canvasWidth / 2f) + (textWidth / 2f) + 50f;
            return offset;
        }

        private float GetEndOffsetLeft()
        {
            if (!useCanvasEdge || parentCanvas == null) return endOffsetLeft;
            var canvasRect = parentCanvas.GetComponent<RectTransform>();
            if (canvasRect == null) return endOffsetLeft;
            float canvasWidth = canvasRect.rect.width;
            float textWidth = matchingRect.rect.width;
            float offset = (canvasWidth / 2f) + (textWidth / 2f) + 50f;
            return offset;
        }
    }
}
