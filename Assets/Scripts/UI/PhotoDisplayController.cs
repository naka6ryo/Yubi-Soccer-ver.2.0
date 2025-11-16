using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Video;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 指定のボタンを押すと写真を前面パネルで表示する。
    /// Inspector:
    ///  - openButton: 押すと写真を表示するボタン
    ///  - closeButton: 押すと写真を閉じるボタン（任意）
    ///  - photoPanel: 写真表示用パネル（Canvas内、最前面のCanvas GroupやSortingOrderを推奨）
    ///  - photoImage: RawImage または Image（RaycastTarget を true にしておく）
    ///  - photoSprite: デフォルトで表示する画像（任意）
    /// </summary>
    public class PhotoDisplayController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image photoImage;

        [Header("Paging (optional)")]
        [Tooltip("If pages are assigned, PhotoDisplayController will operate in paging mode.")]
        [SerializeField] private GameObject[] pages;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [Tooltip("Direct page buttons, one per page (optional)")]
        [SerializeField] private Button[] pageButtons;

        [Header("Per-page Video Players (page-specific) - only for page 3 and 4")]
        [Tooltip("Assign a VideoPlayer + RawImage specifically for page 3 (index 2) if you want a dedicated player for that page.")]
        [SerializeField] private VideoPlayer page3VideoPlayer;
        [SerializeField] private UnityEngine.UI.RawImage page3VideoTarget;
        [Tooltip("Assign a VideoPlayer + RawImage specifically for page 4 (index 3) if you want a dedicated player for that page.")]
        [SerializeField] private VideoPlayer page4VideoPlayer;
        [SerializeField] private UnityEngine.UI.RawImage page4VideoTarget;
        [Header("Shared Video (optional, TutorialSequence-style)")]
        [Tooltip("If you prefer to assign a single VideoPlayer and RawImage like TutorialSequence, assign them here and set per-page VideoClips in Page Video Clips.")]
        [SerializeField] private VideoPlayer sharedVideoPlayer;
        [SerializeField] private UnityEngine.UI.RawImage sharedVideoRawImage;
        [Tooltip("Per-page VideoClips used with the shared VideoPlayer. Index corresponds to pages[].")]
        [SerializeField] private VideoClip[] pageVideoClips;
        [Tooltip("If true, shared video will loop when played")]
        [SerializeField] private bool sharedLoopVideo = false;

        private int currentIndex = 0;

        private void Awake()
        {
            if (openButton != null) openButton.gameObject.SetActive(true);
            if (closeButton != null) closeButton.gameObject.SetActive(false);
            if (photoImage != null) photoImage.gameObject.SetActive(false);

            // ホバーで閉じる等の挙動を防ぐため、closeButton に付与された EventTrigger を削除する
            if (closeButton != null)
            {
                var et = closeButton.gameObject.GetComponent<EventTrigger>();
                if (et != null)
                {
                    Destroy(et);
                }
            }

            // initialize pages to hidden
            if (pages != null && pages.Length > 0)
            {
                for (int i = 0; i < pages.Length; i++)
                {
                    if (pages[i] != null) pages[i].SetActive(false);
                }
                // ensure video players are stopped
                StopAllPageVideos();
                // Configure page-specific players for page 3 and 4 (if assigned)
                if (page3VideoPlayer != null)
                {
                    try
                    {
                        page3VideoPlayer.playOnAwake = false;
                        try { if (page3VideoPlayer.isPlaying) page3VideoPlayer.Stop(); } catch { }
                        try { page3VideoPlayer.enabled = false; } catch { }
                        if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
                        {
                            try { page3VideoPlayer.audioOutputMode = VideoAudioOutputMode.None; } catch { }
                        }
                        try { page3VideoPlayer.prepareCompleted += OnPageVideoPrepared; } catch { }
                        if (page3VideoTarget != null && page3VideoPlayer.targetTexture == null)
                        {
                            try { var rt = new RenderTexture(1280, 720, 0); page3VideoPlayer.targetTexture = rt; page3VideoTarget.texture = rt; } catch { }
                        }
                        else if (page3VideoTarget != null)
                        {
                            try { page3VideoTarget.texture = page3VideoPlayer.targetTexture; } catch { }
                        }
                        // hide target at start
                        if (page3VideoTarget != null) try { page3VideoTarget.gameObject.SetActive(false); } catch { }
                    }
                    catch { }
                }
                if (page4VideoPlayer != null)
                {
                    try
                    {
                        page4VideoPlayer.playOnAwake = false;
                        try { if (page4VideoPlayer.isPlaying) page4VideoPlayer.Stop(); } catch { }
                        try { page4VideoPlayer.enabled = false; } catch { }
                        if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
                        {
                            try { page4VideoPlayer.audioOutputMode = VideoAudioOutputMode.None; } catch { }
                        }
                        try { page4VideoPlayer.prepareCompleted += OnPageVideoPrepared; } catch { }
                        if (page4VideoTarget != null && page4VideoPlayer.targetTexture == null)
                        {
                            try { var rt = new RenderTexture(1280, 720, 0); page4VideoPlayer.targetTexture = rt; page4VideoTarget.texture = rt; } catch { }
                        }
                        else if (page4VideoTarget != null)
                        {
                            try { page4VideoTarget.texture = page4VideoPlayer.targetTexture; } catch { }
                        }
                        // hide target at start
                        if (page4VideoTarget != null) try { page4VideoTarget.gameObject.SetActive(false); } catch { }
                    }
                    catch { }
                }

                // Initialize shared video player similar to TutorialSequence
                if (sharedVideoRawImage != null)
                {
                    try { sharedVideoRawImage.gameObject.SetActive(false); } catch { }
                }
                if (sharedVideoPlayer != null)
                {
                    try
                    {
                        sharedVideoPlayer.playOnAwake = false;
                        try { sharedVideoPlayer.enabled = false; } catch { }
                        sharedVideoPlayer.isLooping = sharedLoopVideo;
                        if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
                        {
                            try { sharedVideoPlayer.audioOutputMode = VideoAudioOutputMode.None; } catch { }
                        }
                        try { sharedVideoPlayer.prepareCompleted += OnSharedVideoPrepared; } catch { }
                        if (sharedVideoPlayer.targetTexture == null && sharedVideoRawImage != null)
                        {
                            int w = 1280, h = 720;
                            // try to size by first available clip
                            try
                            {
                                if (pageVideoClips != null)
                                {
                                    for (int k = 0; k < pageVideoClips.Length; k++)
                                    {
                                        var c = pageVideoClips[k];
                                        if (c != null)
                                        {
                                            int cw = (int)c.width; int ch = (int)c.height;
                                            if (cw > 0) w = cw;
                                            if (ch > 0) h = ch;
                                            break;
                                        }
                                    }
                                }
                            }
                            catch { }
                            try { var rt = new RenderTexture(w, h, 0); sharedVideoPlayer.targetTexture = rt; sharedVideoRawImage.texture = rt; } catch { }
                        }
                        // hide shared RawImage at start
                        if (sharedVideoRawImage != null) try { sharedVideoRawImage.gameObject.SetActive(false); } catch { }
                    }
                    catch { }
                }

                // clear page3/page4 targets if pages inactive
                if (page3VideoTarget != null)
                {
                    bool active3 = (pages != null && pages.Length > 2 && pages[2] != null && pages[2].activeInHierarchy);
                    if (!active3) try { page3VideoTarget.texture = null; } catch { }
                }
                if (page4VideoTarget != null)
                {
                    bool active4 = (pages != null && pages.Length > 3 && pages[3] != null && pages[3].activeInHierarchy);
                    if (!active4) try { page4VideoTarget.texture = null; } catch { }
                }
            }
        }

        private void OnEnable()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(CloseAll);

            // paging buttons
            if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
            if (nextButton != null) nextButton.onClick.AddListener(NextPage);
            if (pageButtons != null)
            {
                for (int i = 0; i < pageButtons.Length; i++)
                {
                    int idx = i;
                    if (pageButtons[i] != null)
                    {
                        pageButtons[i].onClick.AddListener(() => OpenAt(idx));
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseAll);
            if (prevButton != null) prevButton.onClick.RemoveListener(PrevPage);
            if (nextButton != null) nextButton.onClick.RemoveListener(NextPage);
            if (pageButtons != null)
            {
                for (int i = 0; i < pageButtons.Length; i++)
                {
                    if (pageButtons[i] != null)
                    {
                        pageButtons[i].onClick.RemoveAllListeners();
                    }
                }
            }
        }

        // Inspectorで設定した画像を表示する簡易API
        public void Open()
        {
            // If pages are assigned, open first page
            if (pages != null && pages.Length > 0)
            {
                OpenAt(0);
                return;
            }

            // fallback: open single photo
            if (openButton != null) openButton.gameObject.SetActive(false);
            if (closeButton != null) closeButton.gameObject.SetActive(true);
            if (photoImage != null) photoImage.gameObject.SetActive(true);
        }

        // 任意のSpriteを引数で表示するAPI（他スクリプトから呼べる）
        public void OpenWith(Sprite sprite)
        {
            // If pages exist, set first page's image if applicable
            if (pages != null && pages.Length > 0)
            {
                OpenAt(0);
                // try to set image on the first page if it has an Image component
                if (pages[0] != null && sprite != null)
                {
                    var img = pages[0].GetComponentInChildren<Image>(true);
                    if (img != null) img.sprite = sprite;
                }
                return;
            }

            if (photoImage != null)
            {
                if (sprite != null) photoImage.sprite = sprite;
                photoImage.gameObject.SetActive(true);
            }
            if (openButton != null) openButton.gameObject.SetActive(false);
            if (closeButton != null) closeButton.gameObject.SetActive(true);
        }

        // Close current page or close all
        public void Close()
        {
            // If paging mode, close current page only
            if (pages != null && pages.Length > 0)
            {
                ClosePage(currentIndex);
                return;
            }

            // fallback
            if (openButton != null) openButton.gameObject.SetActive(true);
            if (closeButton != null) closeButton.gameObject.SetActive(false);
            if (photoImage != null) photoImage.gameObject.SetActive(false);

            // Ensure any video RawImages/players are cleaned up even in non-paging fallback
            try { if (page3VideoTarget != null) { page3VideoTarget.texture = null; page3VideoTarget.gameObject.SetActive(false); } } catch { }
            try { if (page4VideoTarget != null) { page4VideoTarget.texture = null; page4VideoTarget.gameObject.SetActive(false); } } catch { }
            try { if (sharedVideoRawImage != null) { sharedVideoRawImage.texture = null; sharedVideoRawImage.gameObject.SetActive(false); } } catch { }
            try { if (page3VideoPlayer != null) { if (page3VideoPlayer.isPlaying) page3VideoPlayer.Stop(); page3VideoPlayer.enabled = false; } } catch { }
            try { if (page4VideoPlayer != null) { if (page4VideoPlayer.isPlaying) page4VideoPlayer.Stop(); page4VideoPlayer.enabled = false; } } catch { }
            try { if (sharedVideoPlayer != null) { if (sharedVideoPlayer.isPlaying) sharedVideoPlayer.Stop(); sharedVideoPlayer.enabled = false; } } catch { }
        }

        // Close everything (used by closeAll)
        public void CloseAll()
        {
            if (pages != null && pages.Length > 0)
            {
                for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(false);
                StopAllPageVideos();
                // clear dedicated page targets and fully disable players/targets
                try { if (page3VideoTarget != null) { page3VideoTarget.texture = null; page3VideoTarget.gameObject.SetActive(false); } } catch { }
                try { if (page4VideoTarget != null) { page4VideoTarget.texture = null; page4VideoTarget.gameObject.SetActive(false); } } catch { }
                try { if (page3VideoPlayer != null) { if (page3VideoPlayer.isPlaying) page3VideoPlayer.Stop(); page3VideoPlayer.enabled = false; } } catch { }
                try { if (page4VideoPlayer != null) { if (page4VideoPlayer.isPlaying) page4VideoPlayer.Stop(); page4VideoPlayer.enabled = false; } } catch { }
                // stop shared video and hide its RawImage
                try { if (sharedVideoPlayer != null) { if (sharedVideoPlayer.isPlaying) sharedVideoPlayer.Stop(); sharedVideoPlayer.enabled = false; } } catch { }
                try { if (sharedVideoRawImage != null) { sharedVideoRawImage.texture = null; sharedVideoRawImage.gameObject.SetActive(false); } } catch { }
            }
            if (photoImage != null) photoImage.gameObject.SetActive(false);
            if (openButton != null) openButton.gameObject.SetActive(true);
            if (closeButton != null) closeButton.gameObject.SetActive(false);
        }

        private void ClosePage(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length) return;
            if (pages[index] != null) pages[index].SetActive(false);
            // stop corresponding video
            if (index == 2 && page3VideoPlayer != null)
            {
                try { if (page3VideoPlayer.isPlaying) page3VideoPlayer.Stop(); page3VideoPlayer.enabled = false; } catch { }
            }
            if (index == 3 && page4VideoPlayer != null)
            {
                try { if (page4VideoPlayer.isPlaying) page4VideoPlayer.Stop(); page4VideoPlayer.enabled = false; } catch { }
            }
            // stop shared video if it was used for this page
            if (sharedVideoPlayer != null)
            {
                try { if (sharedVideoPlayer.isPlaying) sharedVideoPlayer.Stop(); sharedVideoPlayer.enabled = false; } catch { }
            }
            // clear corresponding RawImage texture so video won't be visible when page inactive
            if (index == 2 && page3VideoTarget != null)
            {
                try { page3VideoTarget.texture = null; page3VideoTarget.gameObject.SetActive(false); } catch { }
            }
            if (index == 3 && page4VideoTarget != null)
            {
                try { page4VideoTarget.texture = null; page4VideoTarget.gameObject.SetActive(false); } catch { }
            }
            if (sharedVideoRawImage != null)
            {
                try { sharedVideoRawImage.texture = null; sharedVideoRawImage.gameObject.SetActive(false); } catch { }
            }
            // show open button again
            if (openButton != null) openButton.gameObject.SetActive(true);
            if (closeButton != null) closeButton.gameObject.SetActive(false);
        }

        public void OpenAt(int index)
        {
            if (pages == null || pages.Length == 0) { Open(); return; }
            index = Mathf.Clamp(index, 0, pages.Length - 1);
            // hide all
            for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(false);
            StopAllPageVideos();
            // ensure shared video is stopped and hidden when switching pages
            if (sharedVideoPlayer != null)
            {
                try { if (sharedVideoPlayer.isPlaying) sharedVideoPlayer.Stop(); } catch { }
            }
            if (sharedVideoRawImage != null)
            {
                try { sharedVideoRawImage.texture = null; sharedVideoRawImage.gameObject.SetActive(false); } catch { }
            }
            // show selected
            currentIndex = index;
            if (pages[currentIndex] != null) pages[currentIndex].SetActive(true);
            if (openButton != null) openButton.gameObject.SetActive(false);
            if (closeButton != null) closeButton.gameObject.SetActive(true);
            // play page video if present: handle page3/page4 dedicated players first
            if (currentIndex == 2 && page3VideoPlayer != null)
            {
                var vp = page3VideoPlayer;
                var target = page3VideoTarget;
                try
                {
                    if (target != null)
                    {
                        var tex = target.texture as RenderTexture;
                        if (tex != null) vp.targetTexture = tex;
                        else if (vp.targetTexture != null) target.texture = vp.targetTexture;
                    }
                    try { vp.enabled = true; } catch { }
                    if (target != null) try { target.gameObject.SetActive(true); } catch { }
                    try { if (vp.isPlaying) vp.Stop(); } catch { }
                    try { vp.Prepare(); } catch { }
                }
                catch { }
            }
            else if (currentIndex == 3 && page4VideoPlayer != null)
            {
                var vp = page4VideoPlayer;
                var target = page4VideoTarget;
                try
                {
                    if (target != null)
                    {
                        var tex = target.texture as RenderTexture;
                        if (tex != null) vp.targetTexture = tex;
                        else if (vp.targetTexture != null) target.texture = vp.targetTexture;
                    }
                    try { vp.enabled = true; } catch { }
                    if (target != null) try { target.gameObject.SetActive(true); } catch { }
                    try { if (vp.isPlaying) vp.Stop(); } catch { }
                    try { vp.Prepare(); } catch { }
                }
                catch { }
            }
            // If no dedicated page player handled this page, try shared VideoPlayer + per-page clip
            if (sharedVideoPlayer != null && pageVideoClips != null && currentIndex >= 0 && currentIndex < pageVideoClips.Length)
            {
                var clip = pageVideoClips[currentIndex];
                if (clip != null)
                {
                    try
                    {
                        if (sharedVideoRawImage != null) try { sharedVideoRawImage.gameObject.SetActive(true); } catch { }
                        try { sharedVideoPlayer.enabled = true; } catch { }
                        sharedVideoPlayer.clip = clip;
                        sharedVideoPlayer.isLooping = sharedLoopVideo;
                        try { if (sharedVideoPlayer.isPlaying) sharedVideoPlayer.Stop(); } catch { }
                        // Prepare; OnSharedVideoPrepared will Play()
                        try { sharedVideoPlayer.Prepare(); } catch { }
                    }
                    catch { }
                }
            }

            // Ensure that pages without videos do not leave RawImages enabled or players running.
            // If we're not on page 3, hide/clear page3 target and disable its player.
            if (currentIndex != 2)
            {
                if (page3VideoTarget != null)
                {
                    try { page3VideoTarget.texture = null; page3VideoTarget.gameObject.SetActive(false); } catch { }
                }
                if (page3VideoPlayer != null)
                {
                    try { if (page3VideoPlayer.isPlaying) page3VideoPlayer.Stop(); } catch { }
                    try { page3VideoPlayer.enabled = false; } catch { }
                }
            }

            // If we're not on page 4, hide/clear page4 target and disable its player.
            if (currentIndex != 3)
            {
                if (page4VideoTarget != null)
                {
                    try { page4VideoTarget.texture = null; page4VideoTarget.gameObject.SetActive(false); } catch { }
                }
                if (page4VideoPlayer != null)
                {
                    try { if (page4VideoPlayer.isPlaying) page4VideoPlayer.Stop(); } catch { }
                    try { page4VideoPlayer.enabled = false; } catch { }
                }
            }

            // If sharedVideoPlayer wasn't used for this page (no clip), ensure it's disabled and hidden.
            bool sharedUsed = false;
            try { sharedUsed = (sharedVideoPlayer != null && pageVideoClips != null && currentIndex >= 0 && currentIndex < pageVideoClips.Length && pageVideoClips[currentIndex] != null); } catch { }
            if (!sharedUsed)
            {
                if (sharedVideoRawImage != null)
                {
                    try { sharedVideoRawImage.texture = null; sharedVideoRawImage.gameObject.SetActive(false); } catch { }
                }
                if (sharedVideoPlayer != null)
                {
                    try { if (sharedVideoPlayer.isPlaying) sharedVideoPlayer.Stop(); } catch { }
                    try { sharedVideoPlayer.enabled = false; } catch { }
                }
            }
        }

        public void NextPage()
        {
            if (pages == null || pages.Length == 0) return;
            int next = Mathf.Min(currentIndex + 1, pages.Length - 1);
            if (next != currentIndex) OpenAt(next);
        }

        public void PrevPage()
        {
            if (pages == null || pages.Length == 0) return;
            int prev = Mathf.Max(currentIndex - 1, 0);
            if (prev != currentIndex) OpenAt(prev);
        }

        private void StopAllPageVideos()
        {
            // stop dedicated page players and shared player
            if (page3VideoPlayer != null)
            {
                try { if (page3VideoPlayer.isPlaying) page3VideoPlayer.Stop(); } catch { }
            }
            if (page4VideoPlayer != null)
            {
                try { if (page4VideoPlayer.isPlaying) page4VideoPlayer.Stop(); } catch { }
            }
            if (sharedVideoPlayer != null)
            {
                try { if (sharedVideoPlayer.isPlaying) sharedVideoPlayer.Stop(); } catch { }
            }
        }

        private void OnPageVideoPrepared(VideoPlayer vp)
        {
            try { vp.Play(); } catch { }
        }

        private void OnDestroy()
        {
            // unregister dedicated page handlers
            if (page3VideoPlayer != null)
            {
                try { page3VideoPlayer.prepareCompleted -= OnPageVideoPrepared; } catch { }
            }
            if (page4VideoPlayer != null)
            {
                try { page4VideoPlayer.prepareCompleted -= OnPageVideoPrepared; } catch { }
            }
            if (sharedVideoPlayer != null)
            {
                try { sharedVideoPlayer.prepareCompleted -= OnSharedVideoPrepared; } catch { }
            }
        }

        private void OnSharedVideoPrepared(VideoPlayer vp)
        {
            try { vp.Play(); } catch { }
        }
    }
}