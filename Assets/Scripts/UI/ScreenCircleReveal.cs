using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 画面全体を白で覆い、中心から円形に透明化していく（穴を開ける）ワイプ。
    /// - Shader 'UI/CircleHole' を利用します。
    /// - Play() で穴が拡大して中心が透明になります。
    /// </summary>
    public class ScreenCircleReveal : MonoBehaviour
    {
        [Tooltip("ワイプにかける秒数")]
        public float duration = 0.6f;
        [Tooltip("イージングカーブ（0->1）")]
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("覆いの色（通常は白）")]
        public Color color = Color.white;
        [Range(0f, 0.5f)]
        [Tooltip("縁のフェザー（0〜0.5）")]
        public float feather = 0.02f;
        [Tooltip("開始時に自動再生する")]
        public bool playOnStart = false;
        [Tooltip("任意のマテリアルを使いたい場合にセット（UI/CircleHole 互換）")]
        public Material overrideMaterial;
        [Tooltip("ワイプ完了時のイベント（穴が最大になった時）")]
        public UnityEvent onComplete;

        private Material _matInstance;
        private Image _image;
        private float _maxRadius = 1.0f;

        void Awake()
        {
            SetupMask();
            if (playOnStart) Play();
        }

        public void SetupMask()
        {
            if (_image != null) return;

            // Prefer a dedicated overlay canvas to avoid depending on scene-specific canvases
            Canvas canvas = null;
            try { canvas = GameObject.FindObjectOfType<Canvas>(); } catch { }
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                // create our own overlay canvas to ensure consistent behavior across scenes
                GameObject cgo = new GameObject("_ScreenCircleReveal_Canvas");
                canvas = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // ensure this canvas renders on top
                try { canvas.sortingOrder = 1000; } catch { }
                cgo.AddComponent<CanvasScaler>();
                cgo.AddComponent<GraphicRaycaster>();
            }

            GameObject go = new GameObject("ScreenCircleReveal_Mask");
            go.transform.SetParent(canvas.transform, false);
            _image = go.AddComponent<Image>();
            _image.raycastTarget = false;

            Material mat = null;
            if (overrideMaterial != null)
            {
                mat = new Material(overrideMaterial);
            }
            else
            {
                Shader sh = Shader.Find("UI/CircleHole");
                if (sh == null)
                {
                    Debug.LogError("[ScreenCircleReveal] Shader 'UI/CircleHole' not found. Ensure the shader exists.");
                }
                else
                {
                    mat = new Material(sh);
                }
            }

            if (mat == null)
            {
                // fallback: plain white image. We'll animate alpha as a degrade-friendly fallback
                _image.color = color;
                RectTransform rtfb = _image.rectTransform;
                rtfb.anchorMin = Vector2.zero;
                rtfb.anchorMax = Vector2.one;
                rtfb.sizeDelta = Vector2.zero;
                _matInstance = null;
                Debug.LogWarning("[ScreenCircleReveal] Material creation failed - using alpha-fade fallback.");
                return;
            }

            _matInstance = mat;
            _matInstance.SetColor("_Color", color);
            _matInstance.SetFloat("_Radius", 0f);
            _matInstance.SetFloat("_Feather", feather);
            _image.material = _matInstance;

            RectTransform rt = _image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// 穴を拡大して透明化するワイプを開始します。
        /// </summary>
        public void Play()
        {
            StopAllCoroutines();
            StartCoroutine(CoPlay());
        }

        /// <summary>
        /// 穴を閉じる（透明→不透明）ワイプを開始します。
        /// </summary>
        public void ReversePlay()
        {
            StopAllCoroutines();
            StartCoroutine(CoReverse());
        }

        private IEnumerator CoPlay()
        {
            float elapsed = 0f;
            // If shader/material is missing, fallback to animating image alpha (1 -> 0)
            if (_matInstance == null && _image != null)
            {
                Color startCol = _image.color;
                startCol.a = 1f;
                Color endCol = startCol; endCol.a = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                    float v = curve.Evaluate(t);
                    if (_image != null) _image.color = Color.Lerp(startCol, endCol, v);
                    yield return null;
                }
                if (_image != null) _image.color = endCol;
                onComplete?.Invoke();
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
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
            onComplete?.Invoke();
        }

        private IEnumerator CoReverse()
        {
            float elapsed = 0f;
            // Fallback reverse: animate alpha 0 -> 1 if no material
            if (_matInstance == null && _image != null)
            {
                Color startCol = _image.color;
                startCol.a = 0f;
                Color endCol = startCol; endCol.a = 1f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                    float v = curve.Evaluate(t);
                    if (_image != null) _image.color = Color.Lerp(startCol, endCol, v);
                    yield return null;
                }
                if (_image != null) _image.color = endCol;
                onComplete?.Invoke();
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
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
    }
}
