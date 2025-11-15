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
        public bool loopVideo = false;

        [Header("After Close")]
        [Tooltip("閉じたときに表示する別のミッション用パネル（任意）")]
        public GameObject onClosedShowMission;

        [Header("Behavior")]
        [Tooltip("チュートリアル表示中にゲームを一時停止するか（Time.timeScale = 0）")]
        public bool pauseGameWhileOpen = true;

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

        public void Show()
        {
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
        }

        public void Close()
        {
            StopVideo();
            try { if (root != null) root.SetActive(false); } catch { }
            // Restore timeScale
            try
            {
                if (pauseGameWhileOpen)
                {
                    Time.timeScale = previousTimeScale;
                    Debug.Log($"SinglePageTutorial: Restored timeScale={previousTimeScale} on Close().");
                }
            }
            catch { }
            if (onClosedShowMission != null)
            {
                try { onClosedShowMission.SetActive(true); } catch { }
            }
            // notify close listeners
            try { OnClosed?.Invoke(); } catch { }
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
                if (videoClip != null) videoPlayer.clip = videoClip;
                videoPlayer.isLooping = loopVideo;
                // mute for autoplay compatibility on some platforms
                if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    try { videoPlayer.audioOutputMode = VideoAudioOutputMode.None; } catch { }
                }
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
