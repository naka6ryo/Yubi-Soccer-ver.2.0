using System.Collections;
using TMPro;
using UnityEngine.Events;
using YubiSoccer.Game;
using UnityEngine;

namespace YubiSoccer.UI
{
    /// <summary>
    /// シーン起動時に 3,2,1 のカウントダウンを GoalAnnouncementUI と同じアニメで表示。
    /// 右からスライドイン → ホールド → 左へスライドアウト を要素ごとに順番に行う。
    /// </summary>
    public class CountdownUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private TMP_Text targetText;

        [Header("Sequence Texts")]
        [Tooltip("表示するカウントダウン文字列の配列。既定は 3,2,1")]
        [SerializeField] private string[] sequence = new[] { "3", "2", "1" };
        [SerializeField] private Color textColor = Color.white;
        [Tooltip("カウントダウン終了後に 'START!' を表示する")]
        [SerializeField] private bool showStartAfter = true;
        [SerializeField] private string startText = "START!";
        [SerializeField] private Color startTextColor = new Color(0.2f, 1f, 0.4f);

        [Header("Animation (sec)")]
        [SerializeField, Min(0f)] private float slideInDuration = 0.35f;
        [SerializeField, Min(0f)] private float holdDuration = 0.6f;
        [SerializeField, Min(0f)] private float slideOutDuration = 0.35f;
        [Tooltip("位置イージング")]
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Positions (px)")]
        [SerializeField] private float startOffsetRight = 800f;
        [SerializeField] private float endOffsetLeft = 800f;

        [Header("Visibility/Playback")]
        [Tooltip("Start時に自動再生")]
        [SerializeField] private bool autoPlayOnStart = true;
        [Tooltip("再生時にGOAL風UIのように自動で有効/無効を切替")]
        [SerializeField] private bool autoToggleActive = true;

        [Header("Optional: Hide STATE during countdown")]
        [SerializeField] private CanvasGroup stateCanvasGroup;
        [SerializeField] private GameObject stateRootGameObject;
        [SerializeField] private bool hideStateDuringCountdown = false;

        [Header("On Finished")]
        [Tooltip("カウントダウン完了時に呼ばれるイベント")]
        public UnityEvent onFinished;
        [Tooltip("完了時に開始する試合タイマー（任意）")]
        [SerializeField] private MatchTimer matchTimer;
        [Tooltip("完了時に上の MatchTimer を自動で StartTimer() する")]
        [SerializeField] private bool startMatchTimerOnFinish = true;

        private Vector2 centerPos;
        private CanvasGroup cg;
        private Coroutine playing;

        private void Awake()
        {
            if (targetRect == null)
            {
                Debug.LogWarning("[CountdownUI] targetRect 未割当のため無効化します");
                enabled = false;
                return;
            }
            centerPos = targetRect.anchoredPosition;
            cg = targetRect.GetComponent<CanvasGroup>();
            if (cg == null) cg = targetRect.gameObject.AddComponent<CanvasGroup>();
            // 初期配置は右外&非表示
            targetRect.anchoredPosition = centerPos + Vector2.right * startOffsetRight;
            cg.alpha = 0f;
            if (autoToggleActive) targetRect.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (autoPlayOnStart) Play();
        }

        public void Play()
        {
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(CoPlay());
        }

        private IEnumerator CoPlay()
        {
            if (hideStateDuringCountdown) SetStateVisible(false);
            if (autoToggleActive) targetRect.gameObject.SetActive(true);

            for (int i = 0; i < (sequence?.Length ?? 0); i++)
            {
                if (targetText != null)
                {
                    targetText.text = sequence[i];
                    targetText.color = textColor;
                }
                // スライドイン
                cg.alpha = 1f;
                yield return Slide(targetRect, centerPos + Vector2.right * startOffsetRight, centerPos, slideInDuration);
                // ホールド
                if (holdDuration > 0f) yield return new WaitForSecondsRealtime(holdDuration);
                // スライドアウト
                yield return Slide(targetRect, centerPos, centerPos + Vector2.left * endOffsetLeft, slideOutDuration);
                // 次の要素へ向けて右側に戻す（非表示のまま待機）
                targetRect.anchoredPosition = centerPos + Vector2.right * startOffsetRight;
            }

            // 終了後に START! を同じアニメで表示
            if (showStartAfter && !string.IsNullOrEmpty(startText))
            {
                if (targetText != null)
                {
                    targetText.text = startText;
                    targetText.color = startTextColor;
                }
                cg.alpha = 1f;
                yield return Slide(targetRect, centerPos + Vector2.right * startOffsetRight, centerPos, slideInDuration);
                if (holdDuration > 0f) yield return new WaitForSecondsRealtime(holdDuration);
                yield return Slide(targetRect, centerPos, centerPos + Vector2.left * endOffsetLeft, slideOutDuration);
                targetRect.anchoredPosition = centerPos + Vector2.right * startOffsetRight;
            }

            cg.alpha = 0f;
            if (autoToggleActive) targetRect.gameObject.SetActive(false);
            if (hideStateDuringCountdown) SetStateVisible(true);

            // 完了イベントを発火
            try { onFinished?.Invoke(); } catch { }

            // 試合タイマーを自動開始（任意）
            if (startMatchTimerOnFinish && matchTimer != null)
            {
                try { Debug.Log("[CountdownUI] Countdown finished. Starting MatchTimer."); } catch { }
                matchTimer.StartTimer();
            }

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
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            rt.anchoredPosition = to;
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
    }
}
