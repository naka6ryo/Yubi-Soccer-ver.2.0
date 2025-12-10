using UnityEngine;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// RectTransform の回転を、Pivot を中心に正弦波で左右に振るスクリプト。
    /// Inspector で最小角/最大角（度）と周期（秒）を設定できます。
    /// </summary>
    [DisallowMultipleComponent]
    public class TiltImageRotate : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("アニメーション対象の Image。未設定ならこの GameObject の RectTransform を使います。")]
        [SerializeField] private Image targetImage;

        [Header("Tilt Settings")]
        [Tooltip("最小角度（度）。左方向（負）などを指定してください）")]
        [SerializeField] private float minAngle = -15f;
        [Tooltip("最大角度（度）。右方向（正）などを指定してください）")]
        [SerializeField] private float maxAngle = 15f;
        [Tooltip("振幅の周期（秒）")]
        [SerializeField] private float period = 1.5f;

        [Header("Behavior")]
        [Tooltip("回転軸。通常は Z 軸 (UI の左右回転)。")]
        [SerializeField] private Axis rotateAxis = Axis.Z;
        [Tooltip("OnEnable 時に自動で再生するかどうか")]
        [SerializeField] private bool playOnEnable = true;
        [Tooltip("ゲーム停止中も進めたい場合は true")]
        [SerializeField] private bool useUnscaledTime = false;
        [Tooltip("開始位相オフセット（秒）")]
        [SerializeField] private float phaseOffset = 0f;

        public enum Axis { X, Y, Z }

        private RectTransform _rt;
        private bool _playing = false;

        void Reset()
        {
            minAngle = -15f;
            maxAngle = 15f;
            period = 1.5f;
            rotateAxis = Axis.Z;
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
                ApplyAngle(maxAngle);
                return;
            }

            float omega = Mathf.PI * 2f / period;
            float v = (Mathf.Sin(omega * t) + 1f) * 0.5f; // 0..1
            float angle = Mathf.Lerp(minAngle, maxAngle, v);
            ApplyAngle(angle);
        }

        private void ApplyAngle(float angle)
        {
            if (_rt == null) return;

            Vector3 e = _rt.localEulerAngles;
            // localEulerAngles は 0..360 表現なので直接代入するためには注意するが
            // ここでは Quaternion を使って確実に回転を設定する。
            Vector3 rot = Vector3.zero;
            switch (rotateAxis)
            {
                case Axis.X: rot = new Vector3(angle, 0f, 0f); break;
                case Axis.Y: rot = new Vector3(0f, angle, 0f); break;
                case Axis.Z: rot = new Vector3(0f, 0f, angle); break;
            }
            _rt.localRotation = Quaternion.Euler(rot);
        }

        /// <summary>開始</summary>
        public void Play()
        {
            _playing = true;
        }

        /// <summary>停止（現在の角度を維持）</summary>
        public void Pause()
        {
            _playing = false;
        }

        /// <summary>角度をリセット（0）</summary>
        public void ResetRotation()
        {
            if (_rt != null) _rt.localRotation = Quaternion.identity;
        }

        void OnValidate()
        {
            if (period < 0f) period = 0f;
            // min/max が逆になっている場合は自動で入れ替え
            if (minAngle > maxAngle)
            {
                float t = minAngle; minAngle = maxAngle; maxAngle = t;
            }
        }
    }
}
