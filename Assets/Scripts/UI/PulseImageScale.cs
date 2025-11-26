using UnityEngine;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// Image（または任意の RectTransform）のスケールを、回転軸（Pivot）を中心に
    /// 正弦波で拡大・縮小させるコンポーネント。
    /// Inspector で最大/最小スケールと周期を指定できます。
    /// </summary>
    [DisallowMultipleComponent]
    public class PulseImageScale : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("アニメーションさせる Image。未設定ならこの GameObject の RectTransform を使う。")]
        [SerializeField] private Image targetImage;

        [Header("Scale Settings")]
        [Tooltip("最小スケール（単位: 1 = 元の大きさ）")]
        [SerializeField] private float minScale = 0.8f;
        [Tooltip("最大スケール（単位: 1 = 元の大きさ）")]
        [SerializeField] private float maxScale = 1.2f;
        [Tooltip("変化の周期（秒）")]
        [SerializeField] private float period = 1.5f;

        [Header("Behavior")]
        [Tooltip("X/Y を同じ倍率でスケールする（通常は true）")]
        [SerializeField] private bool uniformScale = true;
        [Tooltip("再生を開始するか（OnEnable 時）")]
        [SerializeField] private bool playOnEnable = true;
        [Tooltip("時間に unscaledTime (ゲーム停止時でも進む) を使う場合は true")]
        [SerializeField] private bool useUnscaledTime = false;
        [Tooltip("アニメーション開始の位相オフセット（秒）")]
        [SerializeField] private float phaseOffset = 0f;

        private RectTransform _rt;
        private bool _playing = false;

        void Reset()
        {
            minScale = 0.8f;
            maxScale = 1.2f;
            period = 1.5f;
            uniformScale = true;
            playOnEnable = true;
        }

        void Awake()
        {
            if (targetImage != null)
            {
                _rt = targetImage.rectTransform;
            }
            else
            {
                _rt = GetComponent<RectTransform>();
            }
        }

        void OnEnable()
        {
            if (playOnEnable) Play();
        }

        void OnDisable()
        {
            Pause();
        }

        void Update()
        {
            if (!_playing) return;

            float t = useUnscaledTime ? Time.unscaledTime : Time.time;
            t += phaseOffset;

            if (period <= 0f)
            {
                ApplyScale(maxScale);
                return;
            }

            // 正弦波 -1..1 -> 0..1
            float omega = Mathf.PI * 2f / period;
            float v = (Mathf.Sin(omega * t) + 1f) * 0.5f; // 0..1

            float s = Mathf.Lerp(minScale, maxScale, v);
            ApplyScale(s);
        }

        private void ApplyScale(float s)
        {
            if (_rt == null) return;
            if (uniformScale)
            {
                _rt.localScale = new Vector3(s, s, s);
            }
            else
            {
                // X/Y に同じ値を使うが Z は現状維持
                _rt.localScale = new Vector3(s, s, _rt.localScale.z);
            }
        }

        /// <summary>アニメーションを開始する</summary>
        public void Play()
        {
            _playing = true;
        }

        /// <summary>アニメーションを停止する（現在のスケールを維持）</summary>
        public void Pause()
        {
            _playing = false;
        }

        /// <summary>スケールを初期状態（1）に戻す</summary>
        public void ResetScale()
        {
            if (_rt != null) _rt.localScale = Vector3.one;
        }

        void OnValidate()
        {
            if (minScale < 0f) minScale = 0f;
            if (maxScale < 0f) maxScale = 0f;
            if (period < 0f) period = 0f;
            if (minScale > maxScale)
            {
                float tmp = minScale; minScale = maxScale; maxScale = tmp;
            }
        }
    }
}
