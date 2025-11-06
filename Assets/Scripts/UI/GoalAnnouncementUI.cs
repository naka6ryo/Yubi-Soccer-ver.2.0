using System.Collections;
using TMPro;
using UnityEngine;
using YubiSoccer.Field;
using YubiSoccer.Game;

namespace YubiSoccer.UI
{
    /// <summary>
    /// ゴール時にSTATE表示を一時的に隠し、"GOAL" テキストを
    /// 右→中央→左 へスライドさせるアナウンスUI。
    /// </summary>
    public class GoalAnnouncementUI : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("GOALテキストを表示するRectTransform（アンカー中央想定）")]
        [SerializeField] private RectTransform goalRect;
        [Tooltip("表示するGOALテキスト（任意）。未割当なら文字列のみ変更しない")]
        [SerializeField] private TMP_Text goalText;
        [Tooltip("ゴール時に隠すSTATE UIルート（任意）。CanvasGroupでもGameObjectでも良い（両方指定可）")]
        [SerializeField] private CanvasGroup stateCanvasGroup;
        [SerializeField] private GameObject stateRootGameObject;

        [Header("Text & Color")]
        [SerializeField] private string goalString = "GOAL";
        [SerializeField] private Color teamAColor = Color.cyan;
        [SerializeField] private Color teamBColor = new Color(1f, 0.6f, 0.1f);

        [Header("Animation (sec)")]
        [SerializeField, Min(0f)] private float slideInDuration = 0.35f;
        [SerializeField, Min(0f)] private float holdDuration = 0.6f;
        [SerializeField, Min(0f)] private float slideOutDuration = 0.35f;
        [Tooltip("位置イージング")]
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Animation Style")]
        [Tooltip("true: 中央でフェードイン、false: 右からスライドイン")]
        [SerializeField] private bool useFadeInsteadOfSlide = false;

        [Header("Positions (relative)")]
        [Tooltip("true: Canvasの幅を自動取得して画面外に配置、false: 固定オフセット値を使用")]
        [SerializeField] private bool useCanvasEdge = true;
        [Tooltip("固定オフセット使用時: 中央基準で右側(>0)へどれだけオフセットして開始するか（px）")]
        [SerializeField] private float startOffsetRight = 800f;
        [Tooltip("固定オフセット使用時: 中央基準で左側(>0)へどれだけオフセットして終了するか（px）")]
        [SerializeField] private float endOffsetLeft = 800f;

        [Header("Visibility")]
        [Tooltip("ゴール時にGOAL UIを自動で有効化/無効化する")]
        [SerializeField] private bool autoToggleActive = true;

        private Vector2 centerAnchoredPos;
        private Coroutine playing;
        private CanvasGroup goalCg;
        private Canvas parentCanvas;

        private void Awake()
        {
            if (goalRect == null)
            {
                Debug.LogWarning("[GoalAnnouncementUI] goalRect が未割当です。動作しません。");
                enabled = false;
                return;
            }

            // 親Canvasを取得
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                Debug.LogWarning("[GoalAnnouncementUI] 親Canvasが見つかりません。useCanvasEdge は機能しません。");
            }

            // Animator コンポーネントが干渉する可能性があるため無効化
            var animator = goalRect.GetComponent<Animator>();
            if (animator != null)
            {
                Debug.LogWarning($"[GoalAnnouncementUI] Animator found on goalRect! Disabling to prevent animation conflicts.");
                animator.enabled = false;
            }

            // Layout Group コンポーネントも干渉する可能性があるため警告
            var layoutGroup = goalRect.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (layoutGroup != null)
            {
                Debug.LogWarning($"[GoalAnnouncementUI] LayoutGroup ({layoutGroup.GetType().Name}) found on goalRect! This may override position settings.");
            }

            // 中央位置は常に (0,0) に統一（アンカーが中央前提）
            centerAnchoredPos = Vector2.zero;

            goalCg = goalRect.GetComponent<CanvasGroup>();
            if (goalText != null && !string.IsNullOrEmpty(goalString))
            {
                goalText.text = goalString;
            }

            // 初期位置設定: フェードインモードなら中央、スライドモードなら右側画面外
            if (useFadeInsteadOfSlide)
            {
                goalRect.anchoredPosition = centerAnchoredPos;
            }
            else
            {
                float rightOffset = GetStartOffsetRight();
                goalRect.anchoredPosition = centerAnchoredPos + Vector2.right * rightOffset;
            }

            if (goalCg == null) goalCg = goalRect.gameObject.AddComponent<CanvasGroup>();
            goalCg.alpha = 0f;
            if (autoToggleActive)
            {
                goalRect.gameObject.SetActive(false);
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
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(CoPlay(scoredFor));
        }

        private IEnumerator CoPlay(Team team)
        {
            // STATE非表示
            SetStateVisible(false);

            // テキストカラーと文言
            if (goalText != null)
            {
                goalText.text = goalString;
                goalText.color = (team == Team.TeamA) ? teamAColor : teamBColor;
            }

            if (autoToggleActive) goalRect.gameObject.SetActive(true);

            if (useFadeInsteadOfSlide)
            {
                // 中央配置 + フェードイン
                goalRect.anchoredPosition = centerAnchoredPos;
                goalCg.alpha = 0f;
                yield return FadeIn(goalCg, slideInDuration);
            }
            else
            {
                // 右からスライドイン
                float rightOffset = GetStartOffsetRight();
                Vector2 startPos = centerAnchoredPos + Vector2.right * rightOffset;
                goalCg.alpha = 1f;
                yield return Slide(goalRect, startPos, centerAnchoredPos, slideInDuration);
            }

            // 少しホールド
            if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

            // 左へスライドアウト
            float leftOffset = GetEndOffsetLeft();
            Vector2 endPos = centerAnchoredPos + Vector2.left * leftOffset;
            yield return Slide(goalRect, centerAnchoredPos, endPos, slideOutDuration);

            // 後処理
            goalCg.alpha = 0f;
            if (autoToggleActive) goalRect.gameObject.SetActive(false);
            // STATE再表示
            SetStateVisible(true);
            playing = null;
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
                t += Time.unscaledDeltaTime; // ゲームが止まっても表示したい場合
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

        /// <summary>
        /// スライドイン開始位置のオフセットを取得（右側）
        /// </summary>
        private float GetStartOffsetRight()
        {
            if (!useCanvasEdge || parentCanvas == null)
            {
                return startOffsetRight;
            }

            // Canvas の RectTransform からスクリーン幅を取得
            var canvasRect = parentCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                Debug.LogWarning("[GoalAnnouncementUI] Canvas に RectTransform がありません。固定オフセットを使用します。");
                return startOffsetRight;
            }

            float canvasWidth = canvasRect.rect.width;
            // GOAL テキストの幅も考慮（半分 + マージン）
            float textWidth = goalRect.rect.width;
            float offset = (canvasWidth / 2f) + (textWidth / 2f) + 50f; // 50px マージン
            return offset;
        }

        /// <summary>
        /// スライドアウト終了位置のオフセットを取得（左側）
        /// </summary>
        private float GetEndOffsetLeft()
        {
            if (!useCanvasEdge || parentCanvas == null)
            {
                return endOffsetLeft;
            }

            var canvasRect = parentCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                Debug.LogWarning("[GoalAnnouncementUI] Canvas に RectTransform がありません。固定オフセットを使用します。");
                return endOffsetLeft;
            }

            float canvasWidth = canvasRect.rect.width;
            float textWidth = goalRect.rect.width;
            float offset = (canvasWidth / 2f) + (textWidth / 2f) + 50f;
            return offset;
        }
    }
}

