using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 画面中心から円が広がって画面を白く塗りつぶすワイプ用コンポーネント。
    /// - Inspector から `duration` / `curve` / `color` / `feather` を調整できます。
    /// - `Play()` を呼ぶとワイプ開始。`onComplete` が呼ばれます。
    /// - マテリアル `overrideMaterial` を渡すとそれを使用します。なければ組み込みシェーダー "UI/CircleMask" を利用します。
    /// </summary>
    public class ScreenCircleWipe : MonoBehaviour
    {
        [Tooltip("ワイプにかける秒数")]
        public float duration = 0.6f;
        [Tooltip("イージングカーブ（0->1）")]
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("円の色（通常は白）")]
        public Color color = Color.white;
        [Tooltip("円の縁のフェザー（0〜0.5）")]
        [Range(0f, 0.5f)]
        public float feather = 0.02f;
        [Tooltip("開始時に自動で再生する")]
        public bool playOnStart = false;
        [Tooltip("任意のマテリアルを使いたい場合にセット（UI/CircleMask 互換）")]
        public Material overrideMaterial;
        [Tooltip("ワイプ完了時のイベント")]
        public UnityEvent onComplete;

        // internal
        private Material _matInstance;
        private Image _image;
        // whether a play animation is currently running
        private bool _isPlaying = false;
        // radius の最大値: normalized 0..1（1 が画面の四隅まで届く）
        private float _maxRadius = 1.0f;

        // singleton instance for global scene-wipe orchestration
        private static ScreenCircleWipe _instance;

        public static ScreenCircleWipe Instance
        {
            get
            {
                if (_instance == null)
                {
                    // try to find existing in scene
                    _instance = Object.FindObjectOfType<ScreenCircleWipe>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("_ScreenCircleWipe_Manager");
                        _instance = go.AddComponent<ScreenCircleWipe>();
                        // ensure created manager persists across scenes
                        DontDestroyOnLoad(go);
                        _instance.SetupMask();
                    }
                }
                return _instance;
            }
        }

        void Awake()
        {
            // if this is created automatically as singleton, make sure it persists
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else if (_instance != this)
            {
                // duplicate: destroy
                Destroy(this.gameObject);
                return;
            }

            SetupMask();
            if (playOnStart) Play();
        }

        /// <summary>
        /// マスク Image とマテリアルを作成／初期化します。
        /// </summary>
        public void SetupMask()
        {
            if (_image != null) return;

            // Use (or create) a dedicated overlay canvas so the mask is always topmost.
            Canvas canvas = null;
            try
            {
                var existing = GameObject.Find("_ScreenCircleWipe_Canvas");
                if (existing != null)
                {
                    canvas = existing.GetComponent<Canvas>();
                }
            }
            catch { }
            if (canvas == null)
            {
                GameObject cgo = new GameObject("_ScreenCircleWipe_Canvas");
                canvas = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Force this canvas to render on top
                try { canvas.overrideSorting = true; canvas.sortingOrder = 32767; } catch { }
                cgo.AddComponent<CanvasScaler>();
                cgo.AddComponent<GraphicRaycaster>();
            }
            else
            {
                // Ensure existing canvas renders on top
                try { canvas.overrideSorting = true; canvas.sortingOrder = 32767; } catch { }
            }

            GameObject go = new GameObject("ScreenCircleWipe_Mask");
            go.transform.SetParent(canvas.transform, false);
            _image = go.AddComponent<Image>();
            _image.raycastTarget = false;
            // ensure mask is last sibling so it's rendered over other canvas children
            try { go.transform.SetAsLastSibling(); } catch { }

            Material mat = null;
            if (overrideMaterial != null)
            {
                mat = new Material(overrideMaterial);
            }
            else
            {
                Shader sh = Shader.Find("UI/CircleMask");
                if (sh == null)
                {
                    Debug.LogError("[ScreenCircleWipe] Shader 'UI/CircleMask' not found. Please ensure Assets/Shaders/UI/CircleMask.shader exists.");
                }
                else
                {
                    mat = new Material(sh);
                }
            }

            if (mat == null)
            {
                // fallback: plain white image (no circle) — still usable but won't animate a circular reveal
                _image.color = color;
                RectTransform rtfb = _image.rectTransform;
                rtfb.anchorMin = Vector2.zero;
                rtfb.anchorMax = Vector2.one;
                rtfb.sizeDelta = Vector2.zero;
                _matInstance = null;
                Debug.LogWarning("[ScreenCircleWipe] Material creation failed - using plain white fallback.");
                return;
            }

            _matInstance = mat;
            _matInstance.SetColor("_Color", color);
            _matInstance.SetFloat("_Radius", 0f);
            _matInstance.SetFloat("_Feather", feather);
            _image.material = _matInstance;
            // ensure image color is set to match material for visibility
            try { _image.color = color; } catch { }

            RectTransform rt = _image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// ワイプ再生を開始します。
        /// </summary>
        public void Play()
        {
            StopAllCoroutines();
            // ensure mask is visible and properly initialized
            SetupMask();
            if (_image != null)
            {
                try { _image.enabled = true; _image.raycastTarget = false; _image.color = color; } catch { }
            }
            if (_matInstance != null)
            {
                try { _matInstance.SetColor("_Color", color); } catch { }
            }
            StartCoroutine(CoPlay());
        }

        /// <summary>
        /// Play を呼んで、完了時にコールバックを実行するヘルパー。
        /// </summary>
        public void PlayWithCallback(UnityEngine.Events.UnityAction callback)
        {
            if (callback == null)
            {
                Play();
                return;
            }
            try
            {
                onComplete.AddListener(callback);
            }
            catch { }
            Play();
        }

        /// <summary>
        /// 逆再生（現在画面が塗りつぶされている状態から中心を透明にして表示する）を開始します。
        /// </summary>
        public void ReversePlay()
        {
            StopAllCoroutines();
            StartCoroutine(CoReverse());
        }

        private IEnumerator CoReverse()
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                float v = curve.Evaluate(t);
                float radius = Mathf.Lerp(_maxRadius, 0f, v);
                if (_matInstance != null)
                {
                    _matInstance.SetFloat("_Radius", radius);
                    _matInstance.SetFloat("_Feather", feather);
                }
                yield return null;
            }

            if (_matInstance != null)
            {
                _matInstance.SetFloat("_Radius", 0f);
            }

            onComplete?.Invoke();
        }

        /// <summary>
        /// シーン切替を割り込み、ワイプしてからシーンをロードし、ロード後にリバース（表示）するユーティリティ。
        /// 既存の `SceneManager.LoadScene` 呼び出しを置き換えて使ってください。
        /// </summary>
        public static void LoadSceneWithWipe(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode = UnityEngine.SceneManagement.LoadSceneMode.Single, float overrideDuration = -1f)
        {
            var inst = Instance; // ensure instance exists
            inst.StopAllCoroutines();
            inst.StartCoroutine(inst.CoLoadSceneWithWipe(sceneName, mode, overrideDuration));
        }

        private IEnumerator CoLoadSceneWithWipe(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode, float overrideDuration)
        {
            float playDur = (overrideDuration > 0f) ? overrideDuration : duration;
            // ensure mask is ready
            SetupMask();

            // fill (白で覆う)
            float elapsed = 0f;
            _isPlaying = true;
            Debug.Log("[ScreenCircleWipe] Play (CoLoad) started for: " + sceneName);
            if (_matInstance == null && _image != null)
            {
                // fallback: animate image alpha 0 -> 1 to cover the screen
                Color start = _image.color; start.a = 0f;
                Color end = _image.color; end.a = 1f;
                _image.color = start;
                while (elapsed < playDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, playDur));
                    float v = curve.Evaluate(t);
                    if (_image != null) _image.color = Color.Lerp(start, end, v);
                    yield return null;
                }
                if (_image != null) _image.color = end;
            }
            else
            {
                while (elapsed < playDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, playDur));
                    float v = curve.Evaluate(t);
                    float radius = Mathf.Lerp(0f, _maxRadius, v);
                    if (_matInstance != null)
                    {
                        _matInstance.SetFloat("_Radius", radius);
                        _matInstance.SetFloat("_Feather", feather);
                    }
                    yield return null;
                }

                if (_matInstance != null)
                {
                    _matInstance.SetFloat("_Radius", _maxRadius);
                }
            }

            // start async load
            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, mode);
            if (op != null)
            {
                // do not allow activation until load complete - but allow immediate activation
                while (!op.isDone)
                    yield return null;
            }

            // wait one frame for scene to stabilize
            yield return null;

            // reveal (hole expand -> transparent center) in the new scene
            // Use ReversePlay-like animation: expand hole from 0 to max -> but for reveal we want radius from max->0
            elapsed = 0f;
            if (_matInstance == null && _image != null)
            {
                // fallback reveal: animate alpha 1 -> 0
                Color start = _image.color; start.a = 1f;
                Color end = _image.color; end.a = 0f;
                _image.color = start;
                while (elapsed < playDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, playDur));
                    float v = curve.Evaluate(t);
                    if (_image != null) _image.color = Color.Lerp(start, end, v);
                    yield return null;
                }
                if (_image != null) _image.color = end;
            }
            else
            {
                while (elapsed < playDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, playDur));
                    float v = curve.Evaluate(t);
                    float radius = Mathf.Lerp(_maxRadius, 0f, v);
                    if (_matInstance != null)
                    {
                        _matInstance.SetFloat("_Radius", radius);
                        _matInstance.SetFloat("_Feather", feather);
                    }
                    yield return null;
                }

                if (_matInstance != null)
                {
                    _matInstance.SetFloat("_Radius", 0f);
                }
            }

            // hide mask after reveal
            try
            {
                if (_image != null) _image.gameObject.SetActive(false);
            }
            catch { }

            _isPlaying = false;
            yield break;
        }

        private IEnumerator CoPlay()
        {
            float elapsed = 0f;
            _isPlaying = true;
            Debug.Log("[ScreenCircleWipe] Play started");
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                float v = curve.Evaluate(t);
                float radius = Mathf.Lerp(0f, _maxRadius, v);
                if (_matInstance != null)
                {
                    _matInstance.SetFloat("_Radius", radius);
                }
                yield return null;
            }

            if (_matInstance != null)
            {
                _matInstance.SetFloat("_Radius", _maxRadius);
            }

            onComplete?.Invoke();
            _isPlaying = false;
            Debug.Log("[ScreenCircleWipe] Play completed (onComplete invoked)");
        }

        /// <summary>
        /// 現在 Play 中かどうか（読み取り専用）。
        /// </summary>
        public bool IsPlaying => _isPlaying;
    }
}
