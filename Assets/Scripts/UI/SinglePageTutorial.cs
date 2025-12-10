using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 単一ページのチュートリアルUI（動画付き）。
    /// - `root` を有効にして表示、`closeButton` で閉じる。
    /// - `readAgainButton` で再表示できる。
    /// - `onClosedShowMission` に第二のミッションパネルを設定すると閉じた時にそれを表示する。
    /// </summary>
    public class SinglePageTutorial : MonoBehaviour
    {
        [Tooltip("表示する UI のルート GameObject（1ページ分）")]
        public GameObject root;
        [Tooltip("閉じるボタン（×）")]
        public Button closeButton;
        [Tooltip("もう一度読むボタン（任意）")]
        public Button readAgainButton;

        [Header("Video")]
        public VideoPlayer videoPlayer;
        public RawImage videoRawImage;
        public VideoClip videoClip;
        [Tooltip("(WebGL/mobile) 動画の直接 URL を指定します（優先）。")]
        public string videoUrl;
        [Tooltip("(StreamingAssets) 動画ファイル名（例: videos/tutorial.webm）。URL 未指定時は Application.streamingAssetsPath + \"/\" + fileName を使います。")]
        public string videoFileName;
        public bool loopVideo = false;

        [Header("After Close")]
        [Tooltip("閉じたときに表示する別のミッション用パネル（任意）")]
        public GameObject onClosedShowMission;
        [Tooltip("閉じたときに onClosedShowMission を自動で有効化するか（デフォルト true）")]
        public bool activateOnClosedShowMission = true;

        [Header("Behavior")]
        [Tooltip("チュートリアル表示中にゲームを一時停止するか（Time.timeScale = 0）")]
        public bool pauseGameWhileOpen = true;

        [Header("Show / Fade")]
        [Tooltip("表示前の遅延(秒)")]
        public float showDelay = 0f;
        [Tooltip("フェードを使用するか（CanvasGroup が必要）")]
        public bool useFade = true;
        [Tooltip("フェード時間（秒）")]
        public float fadeDuration = 0.25f;
        [Tooltip("フェード用の CanvasGroup を割り当てます。未設定時は root に追加します。")]
        public CanvasGroup canvasGroup;

        private float previousTimeScale = 1f;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (readAgainButton != null) readAgainButton.onClick.AddListener(Show);

            if (videoRawImage != null)
            {
                try { videoRawImage.gameObject.SetActive(false); } catch { }
            }

            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.isLooping = loopVideo;
                try { videoPlayer.prepareCompleted += OnVideoPrepared; } catch { }
                if (videoPlayer.targetTexture == null && videoRawImage != null)
                {
                    int w = 1280, h = 720;
                    if (videoClip != null)
                    {
                        int clipW = (int)videoClip.width;
                        int clipH = (int)videoClip.height;
                        if (clipW > 0) w = clipW;
                        if (clipH > 0) h = clipH;
                    }
                    var rt = new RenderTexture(w, h, 0);
                    videoPlayer.targetTexture = rt;
                    videoRawImage.texture = rt;
                }
            }

            // Ensure CanvasGroup exists if fade is enabled
            if (useFade)
            {
                try
                {
                    if (canvasGroup == null && root != null)
                    {
                        canvasGroup = root.GetComponent<CanvasGroup>();
                        if (canvasGroup == null)
                        {
                            canvasGroup = root.AddComponent<CanvasGroup>();
                        }
                    }
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 0f;
                        canvasGroup.interactable = false;
                        canvasGroup.blocksRaycasts = false;
                    }
                }
                catch { }
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (readAgainButton != null) readAgainButton.onClick.RemoveListener(Show);
            if (videoPlayer != null)
            {
                try { videoPlayer.prepareCompleted -= OnVideoPrepared; } catch { }
            }
        }

        private string GetVideoUrl()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(videoUrl)) return videoUrl;
                if (!string.IsNullOrWhiteSpace(videoFileName))
                {
                    return Application.streamingAssetsPath.TrimEnd('/') + "/" + videoFileName.TrimStart('/');
                }
            }
            catch { }
            return null;
        }

        public void Show()
        {
            // Start coroutine to handle optional delay and fade
            try { StartCoroutine(ShowCoroutine()); } catch { }
        }

        private System.Collections.IEnumerator ShowCoroutine()
        {
            if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);

            try { if (root != null) root.SetActive(true); } catch { }
            PlayVideo();
            // Pause game if configured
            try
            {
                if (pauseGameWhileOpen)
                {
                    previousTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                    Debug.Log("SinglePageTutorial: Game paused (timeScale=0) on Show().");
                }
            }
            catch { }

            // Handle fade-in
            if (useFade && canvasGroup != null)
            {
                bool doFade = false;
                try
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    doFade = true;
                }
                catch { }
                if (doFade)
                {
                    yield return StartCoroutine(CoFade(canvasGroup, 0f, 1f, fadeDuration));
                }
            }
        }

        public void Close()
        {
            // If fade-out is enabled and canvasGroup is available, perform fade then close
            try
            {
                if (useFade && canvasGroup != null && root != null && root.activeSelf)
                {
                    StartCoroutine(CloseWithFadeCoroutine());
                    return;
                }
            }
            catch { }

            // Immediate close fallback
            DoCloseImmediate();
        }

        private System.Collections.IEnumerator CloseWithFadeCoroutine()
        {
            Debug.Log("SinglePageTutorial: Close() called (with fade)");
            // fade out
            if (canvasGroup != null)
            {
                yield return StartCoroutine(CoFade(canvasGroup, canvasGroup.alpha, 0f, fadeDuration));
            }
            DoCloseImmediate();
        }

        private void DoCloseImmediate()
        {
            Debug.Log("SinglePageTutorial: Close() called");
            StopVideo();
            try { if (root != null) root.SetActive(false); } catch { }
            // Restore timeScale: always resume gameplay when this tutorial is closed
            try
            {
                if (pauseGameWhileOpen)
                {
                    Time.timeScale = 1f;
                    Debug.Log("SinglePageTutorial: Restored timeScale=1 on Close().");
                }
            }
            catch { }

            // notify close listeners first so subscribers can react before mission UI is shown
            try
            {
                Debug.Log("SinglePageTutorial: Invoking OnClosed listeners...");
                OnClosed?.Invoke();
                Debug.Log("SinglePageTutorial: OnClosed listeners invoked.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("SinglePageTutorial: Exception while invoking OnClosed: " + ex);
            }

            // Show assigned mission UI after notifying listeners
            if (onClosedShowMission != null)
            {
                try
                {
                    Debug.Log($"SinglePageTutorial: Attempting to activate onClosedShowMission='{GetGameObjectPath(onClosedShowMission)}' (activeInHierarchy={onClosedShowMission.activeInHierarchy})");
                    if (activateOnClosedShowMission)
                    {
                        onClosedShowMission.SetActive(true);
                        Debug.Log("SinglePageTutorial: onClosedShowMission activated.");
                    }
                    else
                    {
                        Debug.Log("SinglePageTutorial: activateOnClosedShowMission is false; skipping activation.");
                    }
                }
                catch (System.Exception ex) { Debug.LogWarning("SinglePageTutorial: Failed to activate onClosedShowMission: " + ex); }
            }
        }

        private string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "(null)";
            try
            {
                string path = go.name;
                var t = go.transform.parent;
                while (t != null)
                {
                    path = t.name + "/" + path;
                    t = t.parent;
                }
                return path;
            }
            catch { return go.name; }
        }

        private System.Collections.IEnumerator CoFade(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;
            float t = 0f;
            cg.alpha = from;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            cg.alpha = to;
        }

        /// <summary>
        /// イベント: ユーザー/コードで Close() が呼ばれたときに発火します。
        /// </summary>
        public event System.Action OnClosed;

        private void PlayVideo()
        {
            try
            {
                if (videoRawImage != null) videoRawImage.gameObject.SetActive(true);
                if (videoPlayer == null) return;
                // Prefer URL (explicit or StreamingAssets filename) first, otherwise use VideoClip
                try
                {
                    var url = GetVideoUrl();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        try { videoPlayer.source = VideoSource.Url; videoPlayer.url = url; } catch { }
                    }
                    else if (videoClip != null)
                    {
                        videoPlayer.source = VideoSource.VideoClip;
                        videoPlayer.clip = videoClip;
                    }
                }
                catch { if (videoClip != null) { videoPlayer.clip = videoClip; } }
                videoPlayer.isLooping = loopVideo;
                // mute for autoplay compatibility on some platforms
                if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    try { videoPlayer.audioOutputMode = VideoAudioOutputMode.None; } catch { }
                }
                try { videoPlayer.enabled = true; } catch { }
                videoPlayer.Prepare();
            }
            catch { }
        }

        private void StopVideo()
        {
            try
            {
                if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
                if (videoRawImage != null) videoRawImage.gameObject.SetActive(false);
            }
            catch { }
        }

        private void OnVideoPrepared(VideoPlayer vp)
        {
            try { vp.Play(); } catch { }
        }
    }
}
