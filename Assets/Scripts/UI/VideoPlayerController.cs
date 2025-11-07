using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// VideoPlayerを制御するスクリプト
    /// 動画再生、一時停止、停止、音量調整などを行う
    /// </summary>
    public class VideoPlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private AudioSource audioSource;

        [Header("Video Settings")]
        [SerializeField] private VideoClip videoClip;
        [SerializeField] private bool playOnAwake = false;
        [SerializeField] private bool loop = false;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        [Header("WebGL Video Settings")]
        [Tooltip("WebGL用: StreamingAssetsフォルダ内の動画ファイル名（例: opening.webm）")]
        [SerializeField] private string webGLVideoFileName = "opening.webm";

        [Header("Fade Settings")]
        [SerializeField] private bool fadeInOnStart = true;
        [SerializeField] private Color fadeStartColor = Color.white;
        [SerializeField] private float fadeInDuration = 1f;
        [SerializeField] private Image fadeImage; // フェード用のUI Image

        [Header("Loading Panel Settings")]
        [Tooltip("動画終了後に表示するローディングパネル")]
        [SerializeField] private GameObject loadingPanel;
        [Tooltip("動画終了後、ローディングパネル表示までの待機時間")]
        [SerializeField] private float loadingDelay = 0.5f;

        [Header("Scene Transition")]
        [Tooltip("動画終了時に通知する NetworkSceneTransition（シーン遷移制御）")]
        [SerializeField] private NetworkSceneTransition networkSceneTransition;

        [Header("Render Settings")]
        [Tooltip("Camera Far Plane: カメラに直接描画、Render Texture: テクスチャに描画、Material Override: マテリアルに適用")]
        [SerializeField] private VideoRenderMode renderMode = VideoRenderMode.CameraFarPlane;
        [SerializeField] private Camera targetCamera; // Camera Far Plane用
        [SerializeField] private RenderTexture renderTexture; // Render Texture用

        [Header("WebGL Settings")]
        [Tooltip("WebGLビルド時に動画をスキップしてすぐにローディングパネルを表示")]
        [SerializeField] private bool skipVideoOnWebGL = false;

        [Header("Tap to Start Button")]
        [Tooltip("タップして開始ボタン（CanvasGroup）")]
        [SerializeField] private CanvasGroup tapToStartButton;
        [Tooltip("タップボタンのフェードイン時間")]
        [SerializeField] private float buttonFadeInDuration = 0.5f;
        [Tooltip("タップボタンのフェードアウト時間")]
        [SerializeField] private float buttonFadeOutDuration = 0.3f;
        [Tooltip("ボタン点滅のサイクル時間（秒）")]
        [SerializeField] private float buttonBlinkCycleDuration = 2f;
        [Tooltip("ボタン点滅時の最小透明度")]
        [SerializeField, Range(0f, 1f)] private float buttonBlinkMinAlpha = 0.5f;
        [Tooltip("ボタン点滅時の最大透明度")]
        [SerializeField, Range(0f, 1f)] private float buttonBlinkMaxAlpha = 1f;

        private bool waitingForTapToStart = false;
        private Coroutine blinkCoroutine = null; // 点滅コルーチンの参照

        private bool isReady = false;
        private bool videoStarted = false;

        private void Awake()
        {
            Debug.Log("[VideoPlayerController] Awake called");

            // VideoPlayerの初期化
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
                if (videoPlayer == null)
                {
                    videoPlayer = gameObject.AddComponent<VideoPlayer>();
                    Debug.Log("[VideoPlayerController] Created VideoPlayer component");
                }
                else
                {
                    Debug.Log("[VideoPlayerController] Found existing VideoPlayer component");
                }
            }

            // AudioSourceの初期化
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    Debug.Log("[VideoPlayerController] Created AudioSource component");
                }
                else
                {
                    Debug.Log("[VideoPlayerController] Found existing AudioSource component");
                }
            }

            // フェード用Imageの初期化
            if (fadeInOnStart && fadeImage != null)
            {
                fadeImage.color = fadeStartColor;
                fadeImage.gameObject.SetActive(true);
                Debug.Log("[VideoPlayerController] Fade image initialized");
            }
            else if (fadeInOnStart && fadeImage == null)
            {
                Debug.LogWarning("[VideoPlayerController] Fade In On Start is enabled but Fade Image is not assigned!");
            }

            // ローディングパネルを初期状態で非表示にする
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
                Debug.Log("[VideoPlayerController] Loading panel hidden");
            }

            // タップして開始ボタンを初期状態で非表示（透明）にする
            if (tapToStartButton != null)
            {
                tapToStartButton.alpha = 0f;
                tapToStartButton.gameObject.SetActive(false);
                Debug.Log("[VideoPlayerController] Tap to start button hidden");
            }

            SetupVideoPlayer();
        }

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[VideoPlayerController] Start called (WebGL) - playOnAwake: {playOnAwake}, webGLVideoFileName: {webGLVideoFileName}");
#else
            Debug.Log($"[VideoPlayerController] Start called - playOnAwake: {playOnAwake}, videoClip: {(videoClip != null ? videoClip.name : "NULL")}");
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGLビルドの場合
            if (skipVideoOnWebGL)
            {
                Debug.Log("[VideoPlayerController] WebGL: Skipping video, showing loading panel directly");
                SkipToLoading();
                return;
            }

            // WebGLでは常にURLから読み込む
            if (playOnAwake && !string.IsNullOrEmpty(webGLVideoFileName))
            {
                Debug.Log("[VideoPlayerController] WebGL: Attempting to play video from URL...");
                
                // タップして開始ボタンがあれば表示して待機
                if (tapToStartButton != null)
                {
                    Debug.Log("[VideoPlayerController] WebGL: Showing tap to start button");
                    waitingForTapToStart = true;
                    StartCoroutine(ShowTapToStartButton());
                }
                else
                {
                    // ボタンなし: 従来通り自動再生
                    Debug.Log("[VideoPlayerController] WebGL: Auto-playing video");
                    if (fadeInOnStart)
                    {
                        Debug.Log("[VideoPlayerController] WebGL: Preparing video with fade-in...");
                        videoPlayer.Prepare();
                    }
                    else
                    {
                        Debug.Log("[VideoPlayerController] WebGL: Playing video without fade...");
                        Play();
                    }
                }
            }
            else
            {
                if (!playOnAwake)
                {
                    Debug.LogWarning("[VideoPlayerController] WebGL: Play On Awake is disabled!");
                }
                if (string.IsNullOrEmpty(webGLVideoFileName))
                {
                    Debug.LogError("[VideoPlayerController] WebGL: Video file name is not set!");
                }
                SkipToLoading();
            }
#else
            // その他のプラットフォーム
            if (playOnAwake && videoClip != null)
            {
                Debug.Log("[VideoPlayerController] Attempting to play video...");

                // タップして開始ボタンがあれば表示して待機
                if (tapToStartButton != null)
                {
                    Debug.Log("[VideoPlayerController] Showing tap to start button");
                    waitingForTapToStart = true;
                    StartCoroutine(ShowTapToStartButton());
                }
                else
                {
                    // ボタンなし: 従来通り自動再生
                    if (fadeInOnStart)
                    {
                        Debug.Log("[VideoPlayerController] Preparing video with fade-in...");
                        videoPlayer.Prepare();
                    }
                    else
                    {
                        Debug.Log("[VideoPlayerController] Playing video without fade...");
                        Play();
                    }
                }
            }
            else
            {
                if (!playOnAwake)
                {
                    Debug.LogWarning("[VideoPlayerController] Play On Awake is disabled!");
                }
                if (videoClip == null)
                {
                    Debug.LogError("[VideoPlayerController] Video Clip is not assigned!");
                }
            }
#endif
        }

        private void SetupVideoPlayer()
        {
            if (videoPlayer == null) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGLではURLから読み込む
            videoPlayer.source = VideoSource.Url;
            string videoUrl = System.IO.Path.Combine(Application.streamingAssetsPath, webGLVideoFileName);
            videoPlayer.url = videoUrl;
            Debug.Log($"[VideoPlayerController] WebGL: Loading video from URL: {videoUrl}");
#else
            // その他のプラットフォームではVideoClipを使用
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = videoClip;
            Debug.Log($"[VideoPlayerController] Loading video clip: {(videoClip != null ? videoClip.name : "NULL")}");
#endif

            videoPlayer.isLooping = loop;
            videoPlayer.playOnAwake = false; // 手動で制御

            // 音声設定
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, audioSource);
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }

            // デフォルトではオーディオをミュートしない（必要なら MuteIfMobileWebGL で制御）
            try
            {
                videoPlayer.SetDirectAudioMute(0, false);
            }
            catch { }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL用の追加設定
            videoPlayer.skipOnDrop = true; // フレームドロップ時にスキップ
#endif

            // レンダリング設定
            videoPlayer.renderMode = renderMode;
            switch (renderMode)
            {
                case VideoRenderMode.CameraFarPlane:
                case VideoRenderMode.CameraNearPlane:
                    if (targetCamera != null)
                    {
                        videoPlayer.targetCamera = targetCamera;
                    }
                    else
                    {
                        videoPlayer.targetCamera = Camera.main;
                    }
                    break;

                case VideoRenderMode.RenderTexture:
                    videoPlayer.targetTexture = renderTexture;
                    break;

                case VideoRenderMode.MaterialOverride:
                    // マテリアルは別途設定が必要
                    break;
            }

            // イベント登録
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.errorReceived += OnVideoError;
            videoPlayer.started += OnVideoStarted;
        }

        /// <summary>
        /// タップして開始ボタンをフェードインで表示
        /// </summary>
        private System.Collections.IEnumerator ShowTapToStartButton()
        {
            if (tapToStartButton == null) yield break;

            tapToStartButton.gameObject.SetActive(true);
            tapToStartButton.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < buttonFadeInDuration)
            {
                elapsed += Time.deltaTime;
                tapToStartButton.alpha = Mathf.Clamp01(elapsed / buttonFadeInDuration);
                yield return null;
            }

            tapToStartButton.alpha = 1f;
            Debug.Log("[VideoPlayerController] Tap to start button faded in");

            // フェードイン完了後、点滅アニメーションを開始
            blinkCoroutine = StartCoroutine(BlinkTapToStartButton());
        }

        /// <summary>
        /// タップして開始ボタンをゆっくり点滅させる
        /// </summary>
        private System.Collections.IEnumerator BlinkTapToStartButton()
        {
            if (tapToStartButton == null) yield break;

            Debug.Log("[VideoPlayerController] Button blink animation started");

            while (true)
            {
                // フェードアウト（Max → Min）
                float elapsed = 0f;
                float halfCycle = buttonBlinkCycleDuration / 2f;

                while (elapsed < halfCycle)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / halfCycle;
                    tapToStartButton.alpha = Mathf.Lerp(buttonBlinkMaxAlpha, buttonBlinkMinAlpha, t);
                    yield return null;
                }

                // フェードイン（Min → Max）
                elapsed = 0f;
                while (elapsed < halfCycle)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / halfCycle;
                    tapToStartButton.alpha = Mathf.Lerp(buttonBlinkMinAlpha, buttonBlinkMaxAlpha, t);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// タップして開始ボタンをフェードアウトして非表示
        /// </summary>
        private System.Collections.IEnumerator HideTapToStartButton()
        {
            if (tapToStartButton == null) yield break;

            float elapsed = 0f;
            float startAlpha = tapToStartButton.alpha;

            while (elapsed < buttonFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                tapToStartButton.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / buttonFadeOutDuration);
                yield return null;
            }

            tapToStartButton.alpha = 0f;
            tapToStartButton.gameObject.SetActive(false);
            Debug.Log("[VideoPlayerController] Tap to start button faded out");
        }

        /// <summary>
        /// タップボタンがクリック/タップされたときに呼ぶ public メソッド（Button の OnClick から呼ぶ）
        /// </summary>
        public void OnTapToStartClicked()
        {
            if (!waitingForTapToStart) return;

            Debug.Log("[VideoPlayerController] Tap to start button clicked!");
            waitingForTapToStart = false;

            // 点滅アニメーションを停止
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
                Debug.Log("[VideoPlayerController] Button blink animation stopped");
            }

            // ボタンをフェードアウト
            StartCoroutine(HideTapToStartButton());

            // 動画を音ありで再生
            if (audioSource != null)
            {
                audioSource.mute = false;
            }
            try
            {
                videoPlayer.SetDirectAudioMute(0, false);
            }
            catch { }

            // 動画準備＆再生開始
            if (fadeInOnStart)
            {
                Debug.Log("[VideoPlayerController] Preparing video with fade-in and audio...");
                videoPlayer.Prepare();
            }
            else
            {
                Debug.Log("[VideoPlayerController] Playing video with audio...");
                Play();
            }
        }

        /// <summary>
        /// 動画が開始されたときのコールバック
        /// </summary>
        private void OnVideoStarted(VideoPlayer vp)
        {
            videoStarted = true;
            Debug.Log("[VideoPlayerController] Video started successfully");
        }

        /// <summary>
        /// 動画エラー時のコールバック
        /// </summary>
        private void OnVideoError(VideoPlayer vp, string message)
        {
            Debug.LogError($"[VideoPlayerController] Video error: {message}");

            // エラー時はローディングパネルを表示
            SkipToLoading();
        }

        /// <summary>
        /// 動画をスキップしてローディングパネルを表示
        /// </summary>
        private void SkipToLoading()
        {
            // フェードImageを非表示
            if (fadeImage != null)
            {
                fadeImage.gameObject.SetActive(false);
            }

            // ローディングパネルを表示
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
            }
        }

        /// <summary>
        /// 動画を再生
        /// </summary>
        public void Play()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Play();
            }
            else
            {
                Debug.LogWarning("[VideoPlayerController] VideoPlayer is not set!");
            }
        }

        /// <summary>
        /// 動画を一時停止
        /// </summary>
        public void Pause()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Pause();
            }
        }

        /// <summary>
        /// 動画を停止（最初に戻る）
        /// </summary>
        public void Stop()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
            }
        }

        /// <summary>
        /// 音量を設定（0.0 ~ 1.0）
        /// </summary>
        public void SetVolume(float vol)
        {
            volume = Mathf.Clamp01(vol);
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }
        }

        /// <summary>
        /// 動画を変更
        /// </summary>
        public void SetVideoClip(VideoClip clip)
        {
            videoClip = clip;
            if (videoPlayer != null)
            {
                videoPlayer.clip = clip;
            }
        }

        /// <summary>
        /// 動画が終了したときのコールバック
        /// </summary>
        private void OnVideoFinished(VideoPlayer vp)
        {
            Debug.Log("[VideoPlayerController] Video finished!");

            // NetworkSceneTransition に動画終了を通知
            if (networkSceneTransition != null)
            {
                networkSceneTransition.OnVideoFinished();
            }

            // 動画終了後、少し待ってからローディングパネルを表示
            if (loadingPanel != null)
            {
                StartCoroutine(ShowLoadingPanelAfterDelay());
            }
        }

        /// <summary>
        /// 遅延後にローディングパネルを表示
        /// </summary>
        private System.Collections.IEnumerator ShowLoadingPanelAfterDelay()
        {
            if (loadingDelay > 0f)
            {
                yield return new WaitForSeconds(loadingDelay);
            }

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
            }
        }

        /// <summary>
        /// 動画の準備が完了したときのコールバック
        /// </summary>
        private void OnVideoPrepared(VideoPlayer vp)
        {
            Debug.Log("[VideoPlayerController] Video prepared and ready to play!");
            isReady = true;

            // タップ待機中でない場合のみ自動再生
            if (!waitingForTapToStart)
            {
                // フェードイン設定が有効な場合、動画再生とフェードインを開始
                if (fadeInOnStart && playOnAwake)
                {
                    videoPlayer.Play();
                    StartCoroutine(FadeIn());
                }

#if UNITY_WEBGL && !UNITY_EDITOR
                // WebGLでは準備完了後に再生開始されない場合があるため、タイムアウト設定
                StartCoroutine(CheckVideoPlayback());
#endif
            }
            else
            {
                Debug.Log("[VideoPlayerController] Video prepared, waiting for tap to start...");
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// WebGL: 動画が正しく再生されているかチェック
        /// </summary>
        private System.Collections.IEnumerator CheckVideoPlayback()
        {
            yield return new WaitForSeconds(2f);

            if (!videoStarted && videoPlayer != null && videoPlayer.isPlaying == false)
            {
                Debug.LogWarning("[VideoPlayerController] WebGL: Video failed to start, skipping to loading");
                SkipToLoading();
            }
        }
#endif

        /// <summary>
        /// 白からフェードインするコルーチン
        /// </summary>
        private System.Collections.IEnumerator FadeIn()
        {
            if (fadeImage == null)
            {
                Debug.LogWarning("[VideoPlayerController] Fade Image is not assigned!");
                yield break;
            }

            float elapsed = 0f;
            Color startColor = fadeStartColor;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f); // 透明に

            fadeImage.gameObject.SetActive(true);

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                fadeImage.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }

            fadeImage.color = endColor;
            fadeImage.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // 点滅コルーチンを停止
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
            }

            // イベント解除
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoFinished;
                videoPlayer.prepareCompleted -= OnVideoPrepared;
                videoPlayer.errorReceived -= OnVideoError;
                videoPlayer.started -= OnVideoStarted;
            }
        }
    }
}
