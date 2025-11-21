using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace YubiSoccer.UI
{
    /// <summary>
    /// チュートリアル説明画面のシーケンス制御。
    /// - Inspector でページ（任意の GameObject）を配列にセットします。
    /// - Next/Prev/Close ボタンに公開メソッドを割り当ててください。
    /// - Open() で表示、Close() で非表示になります。
    /// - ← / → / Esc のキー入力をサポートします。
    /// </summary>
    public class TutorialSequence : MonoBehaviour
    {
        [Header("Pages (assign in order)")]
        [Tooltip("説明ページを順番に割り当ててください。各ページは GameObject（UI Panel など）です。")]
        public List<GameObject> pages = new List<GameObject>();

        [Header("UI Buttons (optional)")]
        public Button nextButton;
        public Button prevButton;
        public Button closeButton;

        [Header("Options")]
        [Tooltip("最初に表示するページのインデックス（0-based）")]
        public int startIndex = 0;
        [Header("Mission UI")]
        [Tooltip("チュートリアル終了後に表示するミッション用 UI パネル（任意）。")]
        public GameObject missionPanel;
        [Tooltip("ミッションパネル内の『もう一度読む』ボタン（任意）。割当があれば自動でチュートリアルを再表示します。")]
        public Button missionReadAgainButton;
        [Tooltip("チュートリアル終了時にミッションパネルを自動表示するか")]
        public bool showMissionOnClose = true;
        [Tooltip("キー入力での操作を有効化するか（Esc/←/→）")]
        public bool enableKeyboardControls = true;

        [Header("Auto Open / Fade")]
        [Tooltip("シーン起動時に自動でチュートリアルを開くか")]
        public bool autoOpenOnStart = false;
        [Tooltip("自動オープン時の遅延(秒)")]
        public float autoOpenDelay = 0.5f;
        [Tooltip("チュートリアル開閉でゲームを一時停止するか（Time.timeScale = 0）")]
        public bool pauseGameWhileOpen = true;
        [Tooltip("フェードを使用するか（CanvasGroup が必要）")]
        public bool useFade = true;
        [Tooltip("フェード時間（秒）")]
        public float fadeDuration = 0.25f;

        [Header("Optional CanvasGroup")]
        [Tooltip("フェード用の CanvasGroup を割り当てます。未設定時はこのコンポーネントの GameObject に追加します。")]
        public CanvasGroup canvasGroup;

        [Header("Page Video (optional)")]
        [Tooltip("このインデックスのページ表示時に再生するビデオのページインデックス（-1 = 無効）")]
        public int videoPageIndex = -1;
        [Tooltip("再生する VideoClip（VideoClip を使う場合）")]
        public VideoClip videoClip;
        [Tooltip("(WebGL/mobile) Per-page video URL. If set, this URL will be used instead of VideoClip when running in WebGL or on mobile browser builds.")]
        public string[] pageVideoUrls;
        [Tooltip("(StreamingAssets) Per-page file name to be appended to Application.streamingAssetsPath (eg. \"videos/tutorial3.webm\"). Used when pageVideoUrls entry is empty.")]
        public string[] pageVideoFileNames;
        [Tooltip("VideoPlayer を割り当ててください（未設定時は自動で探しません）")]
        public VideoPlayer videoPlayer;
        [Tooltip("ビデオ出力先の RawImage（UI に表示するため）")]
        public RawImage videoRawImage;
        [Tooltip("ループ再生するかどうか（任意）")]
        public bool loopVideo = false;

        public event Action OnOpened;
        public event Action OnClosed;

        private int currentIndex = -1;
        private bool isOpen = false;
        private UnityEngine.Events.UnityAction missionOpenAction;

        private void Awake()
        {
            // Hide all pages at start
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] != null) pages[i].SetActive(false);
            }

            // Hook up button callbacks if provided
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (prevButton != null) prevButton.onClick.AddListener(Prev);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            // Hook up mission panel read-again button if provided (store action so we can remove it later)
            if (missionReadAgainButton != null)
            {
                missionOpenAction = new UnityEngine.Events.UnityAction(() => Open());
                missionReadAgainButton.onClick.AddListener(missionOpenAction);
            }

            // Ensure CanvasGroup exists if fade is enabled
            if (useFade && canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            // Initialize video UI if assigned
            if (videoRawImage != null)
            {
                try { videoRawImage.gameObject.SetActive(false); } catch { }
            }
            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.isLooping = loopVideo;
                // On mobile / WebGL, browsers or OS may block autoplay with audio.
                // Mute audio output so autoplay is allowed.
                if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    try
                    {
                        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                    }
                    catch { }
                }
                // Register prepare-completed to ensure playback starts after preparation
                try { videoPlayer.prepareCompleted += OnVideoPrepared; } catch { }
                // If targetTexture not set but RawImage is provided, create a RenderTexture
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
        }

        private void OnVideoPrepared(VideoPlayer vp)
        {
            try { vp.Play(); }
            catch { }
        }

        private string GetVideoUrlForIndex(int index)
        {
            try
            {
                if (pageVideoUrls != null && index >= 0 && index < pageVideoUrls.Length)
                {
                    var s = pageVideoUrls[index]; if (!string.IsNullOrWhiteSpace(s)) return s;
                }
                if (pageVideoFileNames != null && index >= 0 && index < pageVideoFileNames.Length)
                {
                    var fn = pageVideoFileNames[index]; if (!string.IsNullOrWhiteSpace(fn)) return Application.streamingAssetsPath.TrimEnd('/') + "/" + fn.TrimStart('/');
                }
            }
            catch { }
            return null;
        }

        private void Start()
        {
            if (autoOpenOnStart)
            {
                StartCoroutine(AutoOpenCoroutine());
            }
        }

        private System.Collections.IEnumerator AutoOpenCoroutine()
        {
            if (autoOpenDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(autoOpenDelay);
            }
            Open();
        }

        private void OnDestroy()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
            if (prevButton != null) prevButton.onClick.RemoveListener(Prev);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (missionReadAgainButton != null && missionOpenAction != null) missionReadAgainButton.onClick.RemoveListener(missionOpenAction);
            if (videoPlayer != null)
            {
                try { videoPlayer.prepareCompleted -= OnVideoPrepared; } catch { }
            }
        }

        private void Update()
        {
            if (!isOpen || !enableKeyboardControls) return;

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Next();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Prev();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        /// <summary>
        /// Open the tutorial UI at the configured startIndex (or provided index).
        /// </summary>
        public void Open(int index = -1)
        {
            if (pages == null || pages.Count == 0) return;
            int idx = (index >= 0) ? index : startIndex;
            idx = Mathf.Clamp(idx, 0, pages.Count - 1);
            isOpen = true;
            // Hide mission panel when opening tutorial
            if (missionPanel != null) missionPanel.SetActive(false);
            ShowPage(idx);
            // Pause game if option set
            if (pauseGameWhileOpen) Time.timeScale = 0f;

            // Handle fade-in
            if (useFade && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                StartCoroutine(CoFade(canvasGroup, 0f, 1f, fadeDuration));
            }

            OnOpened?.Invoke();
        }

        /// <summary>
        /// Close the tutorial UI and deactivate all pages.
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            // Fade out if requested, then deactivate pages
            if (useFade && canvasGroup != null)
            {
                StartCoroutine(CoFadeAndClose(canvasGroup, 1f, 0f, fadeDuration));
            }
            else
            {
                // Ensure any playing video is stopped when closing
                StopVideo();
                for (int i = 0; i < pages.Count; i++)
                {
                    if (pages[i] != null) pages[i].SetActive(false);
                }
                if (pauseGameWhileOpen) Time.timeScale = 1f;
                OnClosed?.Invoke();
                // Show mission panel if configured
                if (showMissionOnClose && missionPanel != null)
                {
                    try
                    {
                        // If missionPanel is a child of the fading CanvasGroup, move it out so parent alpha doesn't hide it
                        if (canvasGroup != null && missionPanel.transform.IsChildOf(canvasGroup.transform))
                        {
                            var newParent = canvasGroup.transform.parent;
                            missionPanel.transform.SetParent(newParent, false);
                        }

                        // Ensure missionPanel has its own CanvasGroup visible
                        var mpCg = missionPanel.GetComponent<CanvasGroup>();
                        if (mpCg == null) mpCg = missionPanel.AddComponent<CanvasGroup>();
                        mpCg.alpha = 1f;
                        mpCg.interactable = true;
                        mpCg.blocksRaycasts = true;

                        // If missionPanel has a Canvas, bring it to front by increasing sorting order
                        var mpCanvas = missionPanel.GetComponent<Canvas>();
                        if (mpCanvas == null) mpCanvas = missionPanel.AddComponent<Canvas>();
                        mpCanvas.overrideSorting = true;
                        mpCanvas.sortingOrder = 1000;

                        Debug.Log($"TutorialSequence: Attempting to activate missionPanel='{GetGameObjectPath(missionPanel)}' (activeInHierarchy={missionPanel.activeInHierarchy})");
                        missionPanel.SetActive(true);
                    }
                    catch { missionPanel.SetActive(true); }
                }
            }
        }

        private System.Collections.IEnumerator CoFade(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null)
            {
                yield break;
            }
            float t = 0f;
            cg.alpha = from;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // unscaled so it works while timeScale=0
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            cg.alpha = to;
        }

        private System.Collections.IEnumerator CoFadeAndClose(CanvasGroup cg, float from, float to, float duration)
        {
            yield return StartCoroutine(CoFade(cg, from, to, duration));
            // deactivate pages
            // Ensure any playing video is stopped when closing
            StopVideo();
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] != null) pages[i].SetActive(false);
            }
            cg.interactable = false;
            cg.blocksRaycasts = false;
            if (pauseGameWhileOpen) Time.timeScale = 1f;
            OnClosed?.Invoke();
            // Show mission panel if configured
            if (showMissionOnClose && missionPanel != null)
            {
                try
                {
                    if (canvasGroup != null && missionPanel.transform.IsChildOf(canvasGroup.transform))
                    {
                        var newParent = canvasGroup.transform.parent;
                        missionPanel.transform.SetParent(newParent, false);
                    }
                    var mpCg = missionPanel.GetComponent<CanvasGroup>();
                    if (mpCg == null) mpCg = missionPanel.AddComponent<CanvasGroup>();
                    mpCg.alpha = 1f;
                    mpCg.interactable = true;
                    mpCg.blocksRaycasts = true;
                    var mpCanvas = missionPanel.GetComponent<Canvas>();
                    if (mpCanvas == null) mpCanvas = missionPanel.AddComponent<Canvas>();
                    mpCanvas.overrideSorting = true;
                    mpCanvas.sortingOrder = 1000;
                    Debug.Log($"TutorialSequence: Attempting to activate missionPanel='{GetGameObjectPath(missionPanel)}' (activeInHierarchy={missionPanel.activeInHierarchy})");
                    missionPanel.SetActive(true);
                }
                catch { missionPanel.SetActive(true); }
            }
        }

        public void Next()
        {
            if (!isOpen) return;
            int next = Mathf.Min(currentIndex + 1, pages.Count - 1);
            if (next != currentIndex) ShowPage(next);
        }

        public void Prev()
        {
            if (!isOpen) return;
            int prev = Mathf.Max(currentIndex - 1, 0);
            if (prev != currentIndex) ShowPage(prev);
        }

        private void ShowPage(int index)
        {
            if (pages == null || pages.Count == 0) return;
            index = Mathf.Clamp(index, 0, pages.Count - 1);

            // deactivate previous
            if (currentIndex >= 0 && currentIndex < pages.Count && pages[currentIndex] != null)
                pages[currentIndex].SetActive(false);

            // activate new
            currentIndex = index;
            if (pages[currentIndex] != null)
                pages[currentIndex].SetActive(true);

            // update button states if bound
            if (prevButton != null) prevButton.interactable = (currentIndex > 0);
            if (nextButton != null) nextButton.interactable = (currentIndex < pages.Count - 1);
            // Handle page-specific video playback
            if (videoPageIndex >= 0 && videoPlayer != null && videoRawImage != null)
            {
                if (currentIndex == videoPageIndex)
                {
                    PlayVideo();
                }
                else
                {
                    StopVideo();
                }
            }
        }

        private void PlayVideo()
        {
            try
            {
                if (videoRawImage != null) videoRawImage.gameObject.SetActive(true);
                if (videoPlayer == null) return;
                // Prefer per-page URL (or StreamingAssets file) when available for current page index
                try
                {
                    string urlToUse = null;
                    if (videoPageIndex >= 0)
                    {
                        urlToUse = GetVideoUrlForIndex(videoPageIndex);
                    }
                    if (!string.IsNullOrWhiteSpace(urlToUse))
                    {
                        try { videoPlayer.source = VideoSource.Url; videoPlayer.url = urlToUse; } catch { }
                    }
                    else if (videoClip != null)
                    {
                        try { videoPlayer.source = VideoSource.VideoClip; videoPlayer.clip = videoClip; } catch { }
                    }
                }
                catch { if (videoClip != null) try { videoPlayer.clip = videoClip; } catch { } }

                videoPlayer.isLooping = loopVideo;
                try { videoPlayer.enabled = true; } catch { }
                // Prepare and let OnVideoPrepared callback call Play()
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
    }
}
