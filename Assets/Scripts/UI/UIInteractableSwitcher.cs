using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// UI 要素（Button / Toggle / Selectable）の有効化・無効化を行うユーティリティ。
    /// - ボタンを「触れない」状態にするには Selectable.interactable=false と CanvasGroup.blocksRaycasts=false を併用します。
    /// - optional: CanvasGroup が指定されていればフェードで視覚的に ON/OFF できます。
    /// - VideoPlayerController 等の管理対象を壊したくない場合は、対象の見た目だけをフェードしてインタラクションを制御します。
    ///
    /// 使い方（簡単）:
    ///  - インスペクタで `targets` に影響させたい Button / Toggle / Selectable を登録。
    ///  - 必要なら `targetCanvasGroups` に対応する CanvasGroup を登録してフェードを有効化。
    ///  - このコンポーネントを Button にアタッチして OnClick に `OnButtonClicked` を登録、または `controlButton` に Button を割り当てる。
    ///
    /// 動作のポイント:
    ///  - 無効化（disable）時は即座に Selectable.interactable=false と CanvasGroup.blocksRaycasts=false を行い、
    ///    視覚的フェードはその後に行います（ユーザーが触れられない状態を即座に作るため）。
    ///  - 有効化（enable）時は先に GameObject をアクティブ化し、フェード完了後に interactable=true / blocksRaycasts=true を行います。
    /// </summary>
    public class UIInteractableSwitcher : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("操作対象の Selectable (Button, Toggle...)。複数可。")]
        public Selectable[] targets;

        [Tooltip("視覚的にフェードしたい CanvasGroup。targets と一対一でなくてもよい（複数適用可）。")]
        public CanvasGroup[] targetCanvasGroups;

        [Tooltip("対象の GameObject 自体をアクティブ/非アクティブで制御したい場合に指定。フェードは各 CanvasGroup を優先します。")]
        public GameObject[] targetGameObjects;

        [Header("Control")]
        [Tooltip("トグルモード：押すたび反転。OFF の場合は enableOnClick の値で設定する")]
        public bool toggleMode = true;
        [Tooltip("toggleMode=false のときにボタン押下で常に有効化するか否か（true=有効化, false=無効化）")]
        public bool enableOnClick = true;
        [Tooltip("true の場合、起動時に targets を無効化しておき、ボタン押下で有効化できるようにします。")]
        public bool startDisabled = false;

        [Header("Fade")]
        [Tooltip("フェードを使うか（CanvasGroup があればそれを使う）")]
        public bool useFade = true;
        [Tooltip("フェード時間（秒）")]
        public float fadeDuration = 0.25f;

        [Header("Optional")]
        [Tooltip("このスイッチャーをトリガーする Button を明示的に指定できます（空なら同一 GameObject の Button を探します）")]
        public Button controlButton;

        private Coroutine fadeRoutine;

        void Awake()
        {
            if (controlButton == null)
            {
                controlButton = GetComponent<Button>();
            }
            if (controlButton != null)
            {
                controlButton.onClick.AddListener(OnButtonClicked);
            }

            // 初期状態を無効にするオプション
            if (startDisabled)
            {
                // Disable selectables (but don't disable the control button itself so it can re-enable)
                if (targets != null)
                {
                    foreach (var t in targets)
                    {
                        if (t == null) continue;
                        if (controlButton != null && t == controlButton) continue; // leave the control button enabled
                        t.interactable = false;
                    }
                }

                // CanvasGroups: make transparent and non-interactable
                if (targetCanvasGroups != null)
                {
                    foreach (var cg in targetCanvasGroups)
                    {
                        if (cg == null) continue;
                        cg.alpha = 0f;
                        cg.blocksRaycasts = false;
                    }
                }

                // Optionally deactivate target game objects (but don't deactivate the control button's GameObject)
                if (targetGameObjects != null)
                {
                    foreach (var go in targetGameObjects)
                    {
                        if (go == null) continue;
                        if (controlButton != null && go == controlButton.gameObject) continue;
                        go.SetActive(false);
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (controlButton != null)
            {
                controlButton.onClick.RemoveListener(OnButtonClicked);
            }
        }

        /// <summary>
        /// Button の onClick に割り当てるハンドラ。
        /// </summary>
        public void OnButtonClicked()
        {
            bool wantEnable = toggleMode ? !AreTargetsInteractable() : enableOnClick;
            SetEnabled(wantEnable);
        }

        /// <summary>
        /// 外部から明示的に ON/OFF を切り替える。
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeAndSet(enabled));
        }

        private bool AreTargetsInteractable()
        {
            if (targets != null && targets.Length > 0)
            {
                // 見つかった最初のターゲットの interactable を基準に判断
                foreach (var t in targets)
                {
                    if (t != null) return t.interactable;
                }
            }
            // 代替：CanvasGroup の blocksRaycasts を見る
            if (targetCanvasGroups != null && targetCanvasGroups.Length > 0)
            {
                foreach (var cg in targetCanvasGroups)
                {
                    if (cg != null) return cg.blocksRaycasts;
                }
            }
            // デフォルトは true
            return true;
        }

        private System.Collections.IEnumerator FadeAndSet(bool enable)
        {
            // Immediate disable of interaction to prevent user input while fading out
            if (!enable)
            {
                // disable Selectables immediately
                if (targets != null)
                {
                    foreach (var t in targets)
                    {
                        if (t != null) t.interactable = false;
                    }
                }
                // disable blocksRaycasts on canvas groups so clicks pass through
                if (targetCanvasGroups != null)
                {
                    foreach (var cg in targetCanvasGroups)
                    {
                        if (cg != null) cg.blocksRaycasts = false;
                    }
                }
            }

            // If no fade or no CanvasGroups, just set active/interactable immediately
            if (!useFade || fadeDuration <= 0f || targetCanvasGroups == null || targetCanvasGroups.Length == 0)
            {
                ApplyFinalState(enable);
                fadeRoutine = null;
                yield break;
            }

            // If enabling: ensure GameObjects are active first
            if (enable)
            {
                if (targetGameObjects != null)
                {
                    foreach (var go in targetGameObjects)
                    {
                        if (go != null) go.SetActive(true);
                    }
                }

                // Fade in canvas groups
                float t = 0f;
                // capture starts
                float[] starts = new float[targetCanvasGroups.Length];
                for (int i = 0; i < targetCanvasGroups.Length; i++) starts[i] = targetCanvasGroups[i] != null ? targetCanvasGroups[i].alpha : 0f;
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / fadeDuration);
                    for (int i = 0; i < targetCanvasGroups.Length; i++)
                    {
                        var cg = targetCanvasGroups[i];
                        if (cg == null) continue;
                        cg.alpha = Mathf.Lerp(starts[i], 1f, p);
                    }
                    yield return null;
                }
                // finalize alpha to 1
                foreach (var cg in targetCanvasGroups) if (cg != null) cg.alpha = 1f;

                // enable interactions after fade
                if (targets != null)
                {
                    foreach (var tS in targets) if (tS != null) tS.interactable = true;
                }
                foreach (var cg in targetCanvasGroups) if (cg != null) cg.blocksRaycasts = true;
            }
            else
            {
                // Fade out canvas groups visually while interaction already disabled
                float t = 0f;
                float[] starts = new float[targetCanvasGroups.Length];
                for (int i = 0; i < targetCanvasGroups.Length; i++) starts[i] = targetCanvasGroups[i] != null ? targetCanvasGroups[i].alpha : 1f;
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / fadeDuration);
                    for (int i = 0; i < targetCanvasGroups.Length; i++)
                    {
                        var cg = targetCanvasGroups[i];
                        if (cg == null) continue;
                        cg.alpha = Mathf.Lerp(starts[i], 0f, p);
                    }
                    yield return null;
                }
                // finalize alpha to 0
                foreach (var cg in targetCanvasGroups) if (cg != null) cg.alpha = 0f;

                // optionally deactivate target game objects when fully hidden
                if (targetGameObjects != null)
                {
                    foreach (var go in targetGameObjects)
                    {
                        if (go != null) go.SetActive(false);
                    }
                }
            }

            ApplyFinalState(enable);
            fadeRoutine = null;
        }

        private void ApplyFinalState(bool enabled)
        {
            // Finalize selectables
            if (targets != null)
            {
                foreach (var t in targets)
                {
                    if (t == null) continue;
                    t.interactable = enabled;
                }
            }

            // CanvasGroup blocksRaycasts
            if (targetCanvasGroups != null)
            {
                foreach (var cg in targetCanvasGroups)
                {
                    if (cg == null) continue;
                    cg.blocksRaycasts = enabled;
                    // if not using fade, also set alpha to full/zero
                    if (!useFade)
                    {
                        cg.alpha = enabled ? 1f : 0f;
                    }
                }
            }

            // GameObject active state
            if (targetGameObjects != null)
            {
                foreach (var go in targetGameObjects)
                {
                    if (go == null) continue;
                    go.SetActive(enabled);
                }
            }
        }
    }
}
