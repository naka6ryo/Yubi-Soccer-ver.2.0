using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YubiSoccer.UI
{
    /// <summary>
    /// ミッションパネルの表示状態を制御するシンプルなコントローラ。
    /// - Inspector に未達成表示用と達成表示用のルート GameObject を割り当てます。
    /// - 他スクリプトから `SetCompleted(true)` を呼ぶことで達成表示に切り替わります。
    /// </summary>
    public class MissionUIController : MonoBehaviour
    {
        [Tooltip("未達成時に表示するルート GameObject (任意)")]
        public GameObject incompleteRoot;

        [Tooltip("達成時に表示するルート GameObject (任意)")]
        public GameObject completedRoot;

        [Tooltip("状態テキストがあれば割り当ててください (任意)")]
        public Text statusText;

        [Tooltip("達成時に表示するテキスト (省略可)")]
        public string completedText = "MISSION COMPLETE";

        [Tooltip("未達成時に表示するテキスト (省略可)")]
        public string incompleteText = "MISSION";

        [Header("Announcement / Animation")]
        [Tooltip("達成時に表示してアニメーションする RectTransform（任意）")]
        public RectTransform announcementRect;
        [Tooltip("Announcement 内の TextMeshPro テキスト（任意）。未設定ならテキストは変更されません。）")]
        public TMP_Text announcementText;
        [Tooltip("Announcement 表示時の文言（省略可）")]
        public string announcementString = "MISSION COMPLETE";
        [Tooltip("フェードモードを使う（true）か、スライドイン/アウトする（false）か")]
        public bool useFadeInsteadOfSlide = false;
        [Tooltip("スライド/フェードのイン時間（秒）")]
        public float inDuration = 0.35f;
        [Tooltip("表示ホールド時間（秒）")]
        public float holdDuration = 0.8f;
        [Tooltip("スライド/フェードのアウト時間（秒）")]
        public float outDuration = 0.35f;
        [Tooltip("位置イージング（スライド時に使用）")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Sound (play once, no SoundManager)")]
        [Tooltip("任意の AudioSource を割り当てるとそのソースで再生します。未設定ならこの GameObject に追加されます。")]
        public AudioSource seSource;
        [Tooltip("再生するクリップ（seSource.clip が設定されていない場合に使用）")]
        public AudioClip seClip;

        private Coroutine playingAnnouncement;
        private bool isCompleted = false;

        private void Start()
        {
            // 初期表示を設定
            ApplyState(isCompleted);

            // announcement 初期化
            if (announcementRect != null)
            {
                var cg = announcementRect.GetComponent<CanvasGroup>();
                if (cg == null) cg = announcementRect.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                announcementRect.gameObject.SetActive(false);
            }

            if (seSource == null && seClip != null)
            {
                // AudioSource がなければアタッチしておく（再生は1回のみ）
                seSource = gameObject.AddComponent<AudioSource>();
                seSource.playOnAwake = false;
            }
        }

        /// <summary>
        /// ミッションを達成済みに切り替える。
        /// </summary>
        public void SetCompleted(bool completed)
        {
            if (isCompleted == completed) return;
            isCompleted = completed;
            ApplyState(isCompleted);
            if (isCompleted)
            {
                // 再生演出
                if (playingAnnouncement != null) StopCoroutine(playingAnnouncement);
                playingAnnouncement = StartCoroutine(CoPlayAnnouncement());
                // 音を一度だけ再生
                try
                {
                    if (seSource != null)
                    {
                        if (seClip != null) seSource.PlayOneShot(seClip);
                        else seSource.Play();
                    }
                }
                catch { }
            }
        }

        private void ApplyState(bool completed)
        {
            if (incompleteRoot != null) incompleteRoot.SetActive(!completed);
            if (completedRoot != null) completedRoot.SetActive(completed);
            if (statusText != null)
            {
                statusText.text = completed ? completedText : incompleteText;
            }
        }

        private System.Collections.IEnumerator CoPlayAnnouncement()
        {
            if (announcementRect == null)
            {
                yield break;
            }

            announcementRect.gameObject.SetActive(true);
            var cg = announcementRect.GetComponent<CanvasGroup>();
            if (cg == null) cg = announcementRect.gameObject.AddComponent<CanvasGroup>();

            if (announcementText != null && !string.IsNullOrEmpty(announcementString))
            {
                announcementText.text = announcementString;
            }

            if (useFadeInsteadOfSlide)
            {
                cg.alpha = 0f;
                float t = 0f;
                while (t < inDuration)
                {
                    t += Time.unscaledDeltaTime;
                    cg.alpha = Mathf.Clamp01(t / Mathf.Max(0.0001f, inDuration));
                    yield return null;
                }
                cg.alpha = 1f;
            }
            else
            {
                // スライドイン: 右から中央
                Vector2 center = Vector2.zero;
                float startOffset = 800f;
                var parentCanvas = announcementRect.GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    var cr = parentCanvas.GetComponent<RectTransform>();
                    if (cr != null) startOffset = (cr.rect.width / 2f) + (announcementRect.rect.width / 2f) + 50f;
                }
                Vector2 startPos = center + Vector2.right * startOffset;
                Vector2 endPos = center;
                announcementRect.anchoredPosition = startPos;
                cg.alpha = 1f;
                float t = 0f;
                while (t < inDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, inDuration));
                    float e = ease != null ? ease.Evaluate(u) : u;
                    announcementRect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, e);
                    yield return null;
                }
                announcementRect.anchoredPosition = endPos;
            }

            if (holdDuration > 0f) yield return new WaitForSecondsRealtime(holdDuration);

            if (useFadeInsteadOfSlide)
            {
                float t = 0f;
                while (t < outDuration)
                {
                    t += Time.unscaledDeltaTime;
                    cg.alpha = 1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, outDuration));
                    yield return null;
                }
                cg.alpha = 0f;
            }
            else
            {
                // スライドアウト: 中央から左外
                Vector2 center = Vector2.zero;
                float endOffset = 800f;
                var parentCanvas = announcementRect.GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    var cr = parentCanvas.GetComponent<RectTransform>();
                    if (cr != null) endOffset = (cr.rect.width / 2f) + (announcementRect.rect.width / 2f) + 50f;
                }
                Vector2 startPos = announcementRect.anchoredPosition;
                Vector2 endPos = center + Vector2.left * endOffset;
                float t = 0f;
                while (t < outDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, outDuration));
                    float e = ease != null ? ease.Evaluate(u) : u;
                    announcementRect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, e);
                    yield return null;
                }
                announcementRect.anchoredPosition = endPos;
                cg.alpha = 0f;
            }

            announcementRect.gameObject.SetActive(false);
            playingAnnouncement = null;
        }

        public void ResetCompleted()
        {
            if (playingAnnouncement != null) StopCoroutine(playingAnnouncement);
            playingAnnouncement = null;
            isCompleted = false;
            ApplyState(false);
            if (announcementRect != null)
            {
                var cg = announcementRect.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                announcementRect.gameObject.SetActive(false);
            }
        }
    }
}
