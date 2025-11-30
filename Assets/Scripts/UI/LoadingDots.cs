using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 3つ（以上可）のドットを順番に濃くしていくことで「ローディング」を表現するシンプルなコンポーネント。
    /// - `dots` に `Image` (または任意の `Graphic`) を 3 つ以上割り当ててください。
    /// - `autoStart` が true の場合、`OnEnable` で自動的にアニメを開始します。
    /// - アニメはシーン中で無限ループします（Disable すると停止）。
    /// </summary>
    public class LoadingDots : MonoBehaviour
    {
        [Tooltip("点に使う UI Graphic（Image, TextMeshProUGUI ではなく Graphic を使います）")]
        [SerializeField] private Graphic[] dots = new Graphic[3];

        [Tooltip("点の切り替え間隔（秒）")]
        [SerializeField] private float interval = 0.35f;

        [Tooltip("点の非アクティブ時のアルファ（0..1）")]
        [Range(0f, 1f)]
        [SerializeField] private float minAlpha = 0.35f;

        [Tooltip("点のアクティブ（濃い）時のアルファ（0..1）")]
        [Range(0f, 1f)]
        [SerializeField] private float maxAlpha = 1.0f;

        [Tooltip("自動で再生を開始するか（OnEnable 時）")]
        [SerializeField] private bool autoStart = true;

        private Coroutine loopCoroutine;

        private void Reset()
        {
            // convenience: ensure there are 3 slots by default
            if (dots == null || dots.Length < 3) dots = new Graphic[3];
        }

        private void OnEnable()
        {
            if (autoStart) StartAnimation();
        }

        private void OnDisable()
        {
            StopAnimation();
        }

        /// <summary>
        /// アニメ再生を開始する（外部からも呼べます）
        /// </summary>
        public void StartAnimation()
        {
            if (dots == null || dots.Length == 0) return;
            if (loopCoroutine == null) loopCoroutine = StartCoroutine(CoLoop());
        }

        /// <summary>
        /// アニメを停止する
        /// </summary>
        public void StopAnimation()
        {
            if (loopCoroutine != null)
            {
                try { StopCoroutine(loopCoroutine); } catch { }
                loopCoroutine = null;
            }
            // reset alphas to min
            ApplyAlphaToAll(minAlpha);
        }

        private IEnumerator CoLoop()
        {
            int len = (dots != null) ? dots.Length : 0;
            if (len == 0) yield break;

            int idx = 0;
            // Ensure starting state
            ApplyAlphaToAll(minAlpha);

            while (true)
            {
                // set all to min, then highlight one
                ApplyAlphaToAll(minAlpha);
                if (dots[idx] != null)
                {
                    SetAlpha(dots[idx], maxAlpha);
                }

                // advance
                idx = (idx + 1) % len;

                float t = 0f;
                while (t < interval)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private void ApplyAlphaToAll(float a)
        {
            if (dots == null) return;
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] != null) SetAlpha(dots[i], a);
            }
        }

        private void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            Color c = g.color;
            c.a = Mathf.Clamp01(a);
            g.color = c;
        }
    }
}
