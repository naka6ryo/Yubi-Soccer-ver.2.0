using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        public event Action OnOpened;
        public event Action OnClosed;

        private int currentIndex = -1;
        private bool isOpen = false;

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
                for (int i = 0; i < pages.Count; i++)
                {
                    if (pages[i] != null) pages[i].SetActive(false);
                }
                if (pauseGameWhileOpen) Time.timeScale = 1f;
                OnClosed?.Invoke();
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
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] != null) pages[i].SetActive(false);
            }
            cg.interactable = false;
            cg.blocksRaycasts = false;
            if (pauseGameWhileOpen) Time.timeScale = 1f;
            OnClosed?.Invoke();
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
        }
    }
}
