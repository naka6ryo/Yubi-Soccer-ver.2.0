using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace YubiSoccer.UI
{
    /// <summary>
    /// ゴールアニメーション（MissionUIController の Announcement 完了）後に
    /// 最後の UI を表示するコントローラ。
    /// - 指定の Canvas / UI を一時的に無効化し（State を除外）、代わりに finalRoot を表示します。
    /// - finalRoot の中に Close / Home ボタンを割り当ててください。
    /// - Close は元の UI を復帰、Home はシーン遷移（homeSceneName が未設定時はログのみ）を行います。
    /// </summary>
    public class FinalGoalUIController : MonoBehaviour
    {
        // If true, when the scene is next loaded we will hide mission UI initial states.
        public static bool hideMissionsOnNextLoad = false;
        [Tooltip("表示する最終 UI のルート GameObject（Canvas 等）")]
        public GameObject finalRoot;

        [Tooltip("閉じるボタン（finalRoot 内の Close）")]
        public Button closeButton;

        [Tooltip("ホームへ戻るボタン（finalRoot 内の Home）")]
        public Button homeButton;

        [Tooltip("もう一度読むボタン（finalRoot 内の ReadAgain） - シーンを再読み込みします")]
        public Button readAgainButton;
        [Tooltip("Close を押したときに ReadAgain ボタンを無効化せず残すか（Inspector で制御、デフォルト false）")]
        public bool keepReadAgainAfterClose = false;

        [Tooltip("ホームへ戻る際にロードするシーン名（未設定ならログ出力のみ）")]
        public string homeSceneName = "";

        [Header("Disable Targets")]
        [Tooltip("アナウンス完了時に無効化する UI のルートを Inspector で指定してください。ここに割当したオブジェクトのみを無効化します（State 等の除外ロジックは不要）。")]
        public GameObject[] targetsToDisable;

        [Header("Show / Fade")]
        [Tooltip("表示前の遅延(秒)")]
        public float showDelay = 0f;
        [Tooltip("フェードを使用するか（CanvasGroup が必要）")]
        public bool useFade = true;
        [Tooltip("フェード時間（秒）")]
        public float fadeDuration = 0.25f;
        [Tooltip("フェード用の CanvasGroup を割り当てます。未設定時は finalRoot に追加します。")]
        public CanvasGroup canvasGroup;

        // 保存しておいて Close 時に復帰する GameObject のリスト
        private List<GameObject> disabledCanvases = new List<GameObject>();

        private MissionUIController missionUIController;

        private bool isShowing = false;

        private void Start()
        {
            // find mission controller if not assigned via inspector
            try { missionUIController = FindObjectOfType<MissionUIController>(); } catch { }
            if (missionUIController != null)
            {
                missionUIController.OnAnnouncementFinished += OnMissionAnnouncementFinished;
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
                try
                {
                    closeButton.interactable = false;
                    if (closeButton.gameObject != null) closeButton.gameObject.SetActive(false);
                }
                catch { }
            }
            if (homeButton != null)
            {
                homeButton.onClick.AddListener(OnHomeClicked);
                try
                {
                    homeButton.interactable = false;
                    if (homeButton.gameObject != null) homeButton.gameObject.SetActive(false);
                }
                catch { }
            }
            if (readAgainButton != null)
            {
                readAgainButton.onClick.AddListener(OnReadAgainClicked);
                try
                {
                    readAgainButton.interactable = false;
                    if (readAgainButton.gameObject != null) readAgainButton.gameObject.SetActive(false);
                }
                catch { }
            }

            if (finalRoot != null) finalRoot.SetActive(false);
            // Ensure CanvasGroup exists if fade is enabled
            if (useFade)
            {
                try
                {
                    if (canvasGroup == null && finalRoot != null)
                    {
                        canvasGroup = finalRoot.GetComponent<CanvasGroup>();
                        if (canvasGroup == null)
                        {
                            canvasGroup = finalRoot.AddComponent<CanvasGroup>();
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
            if (missionUIController != null)
            {
                missionUIController.OnAnnouncementFinished -= OnMissionAnnouncementFinished;
            }
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
            if (homeButton != null) homeButton.onClick.RemoveListener(OnHomeClicked);
            if (readAgainButton != null) readAgainButton.onClick.RemoveListener(OnReadAgainClicked);
        }

        private void OnMissionAnnouncementFinished()
        {
            try { StartCoroutine(ShowFinalUICoroutine()); } catch { ShowFinalUI(); }
        }

        private System.Collections.IEnumerator ShowFinalUICoroutine()
        {
            if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);
            ShowFinalUI();
        }

        private void ShowFinalUI()
        {
            // Disable only Inspector-assigned targets
            disabledCanvases.Clear();
            if (targetsToDisable != null)
            {
                foreach (var go in targetsToDisable)
                {
                    if (go == null) continue;
                    try
                    {
                        if (go.activeSelf)
                        {
                            go.SetActive(false);
                            disabledCanvases.Add(go);
                        }
                    }
                    catch { }
                }
            }

            if (finalRoot != null)
            {
                finalRoot.SetActive(true);
            }

            // Handle fade-in if configured
            if (useFade && canvasGroup != null)
            {
                try
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    StartCoroutine(CoFade(canvasGroup, 0f, 1f, fadeDuration));
                }
                catch { }
            }

            // Enable Close/Home buttons when final UI is shown
            if (closeButton != null)
            {
                try
                {
                    if (closeButton.gameObject != null) closeButton.gameObject.SetActive(true);
                    closeButton.interactable = true;
                }
                catch { }
            }
            if (homeButton != null)
            {
                try
                {
                    if (homeButton.gameObject != null) homeButton.gameObject.SetActive(true);
                    homeButton.interactable = true;
                }
                catch { }
            }
            if (readAgainButton != null)
            {
                try
                {
                    if (readAgainButton.gameObject != null) readAgainButton.gameObject.SetActive(true);
                    readAgainButton.interactable = true;
                }
                catch { }
            }
            isShowing = true;
            Debug.Log($"FinalGoalUIController: Shown final UI. Disabled {disabledCanvases.Count} assigned UI roots.");
        }

        private void OnCloseClicked()
        {
            // If fade-out configured, perform fade then close
            if (useFade && canvasGroup != null && isShowing)
            {
                try { StartCoroutine(CloseWithFadeCoroutine()); return; } catch { }
            }

            // Immediate hide
            if (finalRoot != null) finalRoot.SetActive(false);
            // keep disabledCanvases as-is (no restoration)
            // Disable only the Close button again so it starts disabled next time
            if (closeButton != null)
            {
                try { closeButton.interactable = false; if (closeButton.gameObject != null) closeButton.gameObject.SetActive(false); } catch { }
            }
            // Also disable ReadAgain so it starts disabled next time (unless configured to keep it)
            if (readAgainButton != null && !keepReadAgainAfterClose)
            {
                try { readAgainButton.interactable = false; if (readAgainButton.gameObject != null) readAgainButton.gameObject.SetActive(false); } catch { }
            }
            // Do NOT disable Home button on Close; leave it enabled per request.
            isShowing = false;
            Debug.Log("FinalGoalUIController: Close clicked - final UI hidden. Previously disabled UI remain disabled.");
        }

        private System.Collections.IEnumerator CloseWithFadeCoroutine()
        {
            // Fade out
            if (canvasGroup != null)
            {
                yield return StartCoroutine(CoFade(canvasGroup, canvasGroup.alpha, 0f, fadeDuration));
            }
            // Hide finalRoot and update buttons
            if (finalRoot != null) finalRoot.SetActive(false);
            if (closeButton != null)
            {
                try { closeButton.interactable = false; if (closeButton.gameObject != null) closeButton.gameObject.SetActive(false); } catch { }
            }
            if (readAgainButton != null && !keepReadAgainAfterClose)
            {
                try { readAgainButton.interactable = false; if (readAgainButton.gameObject != null) readAgainButton.gameObject.SetActive(false); } catch { }
            }
            // leave homeButton enabled
            isShowing = false;
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

        private void OnHomeClicked()
        {
            Debug.Log("FinalGoalUIController: Home clicked.");
            if (!string.IsNullOrEmpty(homeSceneName))
            {
                try { SceneManager.LoadScene(homeSceneName); } catch (System.Exception ex) { Debug.LogWarning("FinalGoalUIController: Failed to load home scene: " + ex); }
            }
            else
            {
                Debug.Log("FinalGoalUIController: homeSceneName is empty; no scene loaded.");
            }
        }

        private void OnReadAgainClicked()
        {
            Debug.Log("FinalGoalUIController: ReadAgain clicked - reloading current scene.");
            try
            {
                // Request that missions be hidden on the next scene load (so initial UI won't flash visible)
                hideMissionsOnNextLoad = true;
                var scene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(scene.name);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("FinalGoalUIController: Failed to reload scene: " + ex);
            }
        }

        // Note: Exclusion logic removed — behavior now disables only explicitly assigned targets.
    }
}
