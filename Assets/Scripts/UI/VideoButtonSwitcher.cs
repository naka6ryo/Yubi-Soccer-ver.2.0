using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace YubiSoccer.UI
{
    /// <summary>
    /// UI ボタンから VideoPlayer の有効/無効を切り替えるヘルパースクリプト。
    /// - このコンポーネントは Button と一緒にアタッチして使います。
    /// - Inspector で対象の `VideoPlayer` を指定するか、同一 GameObject 上の VideoPlayer を自動検出します。
    /// - トグルモード（押すたびに反転）か、押したときに常に有効化/無効化するモードを選べます。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class VideoButtonSwitcher : MonoBehaviour
    {
        [Tooltip("切り替える VideoPlayer（未設定なら同じ GameObject の VideoPlayer を探します）")]
        public VideoPlayer targetVideoPlayer;

        [Header("動作モード")]
        [Tooltip("トグルモード：押すたびに有効/無効を反転させる。OFF の場合は enableOnClick の値で設定する")]
        public bool toggleMode = true;

        [Tooltip("toggleMode が false のとき、ボタン押下で VideoPlayer を有効にするか（true=有効化, false=無効化）")]
        public bool enableOnClick = true;

        [Header("Fade")]
        [Tooltip("有効/無効時にフェードを行うかどうか")]
        public bool useFade = true;
        [Tooltip("フェードにかける秒数（有効/無効の切替時）")]
        public float fadeDuration = 0.4f;
        [Tooltip("フェード対象の CanvasGroup（未設定なら VideoPlayer 所属オブジェクトの CanvasGroup を探します）")]
        public CanvasGroup targetCanvasGroup;
        [Tooltip("フェード対象が UI ではなく Renderer のマテリアルの場合は Renderer を指定できます（省略可）。マテリアルの _Color.a を操作します）")]
        public Renderer targetRenderer;

        private Button _button;
        private Coroutine fadeRoutine;
        // If we need to tint a Renderer, cache an instantiated material so we don't keep creating instances
        private Material _instancedRendererMaterial;
        // If the VideoPlayer is managed by a VideoPlayerController, keep reference to it
        private VideoPlayerController _videoPlayerController;
        // If RenderTexture is used, try to find the RawImage that displays it so we can fade that
        private RawImage _targetRawImage;

        void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.AddListener(OnButtonClicked);
            }

            if (targetVideoPlayer == null)
            {
                targetVideoPlayer = GetComponent<VideoPlayer>();
            }

            if (targetVideoPlayer != null)
            {
                if (targetCanvasGroup == null)
                    targetCanvasGroup = targetVideoPlayer.GetComponent<CanvasGroup>() ?? targetVideoPlayer.GetComponentInChildren<CanvasGroup>(true);
                if (targetRenderer == null)
                    targetRenderer = targetVideoPlayer.GetComponent<Renderer>() ?? targetVideoPlayer.GetComponentInChildren<Renderer>(true);

                // If there is a renderer and it has a material, instantiate it once so alpha changes affect only this object
                if (targetRenderer != null && targetRenderer.sharedMaterial != null)
                {
                    // Instantiate and assign to renderer.material (this creates an instance but we cache it so it's only once)
                    _instancedRendererMaterial = Object.Instantiate(targetRenderer.sharedMaterial);
                    targetRenderer.material = _instancedRendererMaterial;
                }
                // Try to find a VideoPlayerController on the same object (or parents/children) and keep reference
                _videoPlayerController = targetVideoPlayer.GetComponent<VideoPlayerController>()
                    ?? targetVideoPlayer.GetComponentInParent<VideoPlayerController>()
                    ?? targetVideoPlayer.GetComponentInChildren<VideoPlayerController>(true);

                // If render target is a RenderTexture, try to find a RawImage that shows it so we can fade it
                if (targetVideoPlayer.targetTexture != null)
                {
                    var rt = targetVideoPlayer.targetTexture;
                    // Find all RawImage components in scene (including inactive) and match texture
                    var rawImages = Object.FindObjectsOfType<RawImage>(true);
                    foreach (var ri in rawImages)
                    {
                        if (ri.texture == rt)
                        {
                            _targetRawImage = ri;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Button の onClick に登録されるハンドラ。
        /// </summary>
        public void OnButtonClicked()
        {
            if (targetVideoPlayer == null)
            {
                Debug.LogWarning("[VideoButtonSwitcher] targetVideoPlayer が設定されていません。");
                return;
            }

            bool wantEnable;
            if (toggleMode)
            {
                // If managed by controller, use controller state when possible; otherwise fallback to component enabled
                if (_videoPlayerController != null && targetVideoPlayer != null)
                {
                    wantEnable = !targetVideoPlayer.isPlaying;
                }
                else
                {
                    wantEnable = targetVideoPlayer != null ? !targetVideoPlayer.enabled : enableOnClick;
                }
            }
            else
            {
                wantEnable = enableOnClick;
            }
            // Start fade coroutine which will enable/disable the VideoPlayer appropriately
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeAndSet(wantEnable));
        }

        /// <summary>
        /// 外部から明示的に VideoPlayer を切り替えたいときに呼べるヘルパー。
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (targetVideoPlayer == null) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeAndSet(enabled));
        }

        private System.Collections.IEnumerator FadeAndSet(bool enable)
        {
            if (!useFade || fadeDuration <= 0f)
            {
                // If we're not fading, ensure the visual target is consistent.
                if (_videoPlayerController != null)
                {
                    // Use controller API to play/pause so we don't modify controller-managed state
                    if (enable) _videoPlayerController.Play();
                    else _videoPlayerController.Pause();
                }
                else if (targetVideoPlayer != null)
                {
                    // Fallback: toggle component/GameObject
                    targetVideoPlayer.gameObject.SetActive(enable);
                    targetVideoPlayer.enabled = enable;
                }

                SetAlphaImmediate(enable ? 1f : 0f);
                fadeRoutine = null;
                yield break;
            }

            // If enabling: enable video first then fade in
            if (enable)
            {
                // Ensure visuals are active
                if (targetVideoPlayer != null)
                {
                    targetVideoPlayer.gameObject.SetActive(true);
                }

                // If controller exists, start playback via controller to avoid interfering with it
                if (_videoPlayerController != null)
                {
                    _videoPlayerController.Play();
                }

                float start = GetCurrentAlpha();
                float t = 0f;
                while (t < fadeDuration)
                {
                    float a = Mathf.Lerp(start, 1f, t / fadeDuration);
                    SetAlpha(a);
                    t += Time.deltaTime;
                    yield return null;
                }
                SetAlpha(1f);
            }
            else
            {
                // Fade out first then disable VideoPlayer
                float start = GetCurrentAlpha();
                float t = 0f;
                while (t < fadeDuration)
                {
                    float a = Mathf.Lerp(start, 0f, t / fadeDuration);
                    SetAlpha(a);
                    t += Time.deltaTime;
                    yield return null;
                }
                SetAlpha(0f);
                if (_videoPlayerController != null)
                {
                    // Pause via controller
                    _videoPlayerController.Pause();
                }
                else if (targetVideoPlayer != null)
                {
                    // disable component and optionally deactivate GameObject to fully hide
                    targetVideoPlayer.enabled = false;
                    targetVideoPlayer.gameObject.SetActive(false);
                }
            }
            fadeRoutine = null;
        }

        private float GetCurrentAlpha()
        {
            if (targetCanvasGroup != null) return targetCanvasGroup.alpha;
            if (targetRenderer != null)
            {
                var mat = _instancedRendererMaterial ?? targetRenderer.material;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    return mat.color.a;
                }
            }
            // If there's a RawImage mapped to the RenderTexture, use its color alpha
            if (_targetRawImage != null) return _targetRawImage.color.a;
            return targetVideoPlayer != null && targetVideoPlayer.enabled ? 1f : 0f;
        }

        private void SetAlpha(float a)
        {
            a = Mathf.Clamp01(a);
            if (targetCanvasGroup != null)
            {
                targetCanvasGroup.alpha = a;
                return;
            }
            if (targetRenderer != null)
            {
                var mat = _instancedRendererMaterial ?? targetRenderer.material;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.a = a;
                    mat.color = c;
                    return;
                }
            }
            // If we found a RawImage that displays the render texture, fade its color
            if (_targetRawImage != null)
            {
                Color rc = _targetRawImage.color;
                rc.a = a;
                _targetRawImage.color = rc;
                // Only toggle active at fully off/on
                if (a <= 0f) _targetRawImage.gameObject.SetActive(false);
                else if (a >= 1f) _targetRawImage.gameObject.SetActive(true);
                return;
            }
            // Fallback: toggle active state when fully off/on
            if (targetVideoPlayer != null)
            {
                // Only change active state when fully off or fully on to avoid toggling during fades
                if (a <= 0f) targetVideoPlayer.gameObject.SetActive(false);
                else if (a >= 1f) targetVideoPlayer.gameObject.SetActive(true);
            }
        }

        private void SetAlphaImmediate(float a)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            SetAlpha(a);
        }

        void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
            }
        }
    }
}
