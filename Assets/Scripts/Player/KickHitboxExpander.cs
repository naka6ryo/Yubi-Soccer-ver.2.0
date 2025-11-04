using System.Collections;
using UnityEngine;

namespace YubiSoccer.Player
{
    /// <summary>
    /// AddForce や Lift を使わず、コライダーの一時的な拡大で物理的な当たりを強めるコンポーネント。
    /// - Tap キック時: 短時間だけ拡大
    /// - Charge 時: 溜め中に段階的に拡大、解放で縮小
    /// 注意: targetCollider は isTrigger=false の非トリガーとして物理衝突を発生させること。
    /// </summary>
    public class KickHitboxExpander : MonoBehaviour
    {
        [Header("Target Collider")]
        [Tooltip("拡大対象のコライダー（Sphere/Box/Capsule をサポート）")]
        public Collider targetCollider;
        [Tooltip("開始時に isTrigger を false にする（物理衝突必須）")]
        public bool forceNonTrigger = true;

        [Header("Tap Kick Settings")]
        [Min(1f)] public float tapScale = 1.8f;
        [Min(0f)] public float tapExpandTime = 0.05f;
        [Min(0f)] public float tapHoldTime = 0.10f;
        [Min(0f)] public float tapShrinkTime = 0.08f;

        [Header("Charge Settings")]
        [Min(1f)] public float chargeMaxScale = 2.2f;
        [Min(0f)] public float chargeExpandTime = 0.15f;
        [Min(0f)] public float chargeShrinkTime = 0.12f;

        // 保存用（コライダー種類ごとに基準サイズ）
        Vector3 _boxBaseSize;
        float _sphereBaseRadius;
        float _capsuleBaseRadius;
        float _capsuleBaseHeight;

        Coroutine _routine;
        bool _charging;
        float _chargeT; // 0..1

        void Awake()
        {
            if (targetCollider == null)
            {
                targetCollider = GetComponent<Collider>();
            }
            CacheBaseSize();
            if (forceNonTrigger && targetCollider != null)
            {
                targetCollider.isTrigger = false;
            }
        }

        void CacheBaseSize()
        {
            if (targetCollider is BoxCollider bc)
            {
                _boxBaseSize = bc.size;
            }
            else if (targetCollider is SphereCollider sc)
            {
                _sphereBaseRadius = sc.radius;
            }
            else if (targetCollider is CapsuleCollider cc)
            {
                _capsuleBaseRadius = cc.radius;
                _capsuleBaseHeight = cc.height;
            }
        }

        public void KickTap()
        {
            if (targetCollider == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CoTap());
        }

        public void ChargeStart()
        {
            if (targetCollider == null) return;
            _charging = true;
            _chargeT = 0f;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CoCharge());
        }

        public void ChargeUpdate(float dt)
        {
            if (!_charging) return;
            // 時間に伴って 0..1 に近づける（簡易）
            _chargeT = Mathf.Clamp01(_chargeT + (dt / Mathf.Max(0.0001f, chargeExpandTime)));
        }

        public void ChargeRelease()
        {
            _charging = false;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CoShrinkToBase(chargeShrinkTime));
        }

        IEnumerator CoTap()
        {
            // 拡大
            yield return ScaleTo(tapScale, tapExpandTime);
            // ホールド
            if (tapHoldTime > 0f) yield return new WaitForSeconds(tapHoldTime);
            // 縮小
            yield return ScaleTo(1f, tapShrinkTime);
            _routine = null;
        }

        IEnumerator CoCharge()
        {
            // 溜め中は chargeMaxScale * _chargeT へ徐々に近づける
            while (_charging)
            {
                float targetScale = Mathf.Lerp(1f, chargeMaxScale, _chargeT);
                ApplyScaleInstant(targetScale);
                yield return null;
            }
            // 解放時は縮小
            yield return CoShrinkToBase(chargeShrinkTime);
            _routine = null;
        }

        IEnumerator CoShrinkToBase(float time)
        {
            yield return ScaleTo(1f, time);
        }

        IEnumerator ScaleTo(float scale, float time)
        {
            time = Mathf.Max(0f, time);
            if (time == 0f)
            {
                ApplyScaleInstant(scale);
                yield break;
            }
            // 現在サイズから目標サイズへ補間
            float t = 0f;
            float startScale = GetCurrentScale();
            while (t < time)
            {
                float u = t / time;
                float s = Mathf.Lerp(startScale, scale, u);
                ApplyScaleInstant(s);
                t += Time.deltaTime;
                yield return null;
            }
            ApplyScaleInstant(scale);
        }

        float GetCurrentScale()
        {
            if (targetCollider is BoxCollider bc)
            {
                return bc.size.x / Mathf.Max(0.0001f, _boxBaseSize.x);
            }
            if (targetCollider is SphereCollider sc)
            {
                return sc.radius / Mathf.Max(0.0001f, _sphereBaseRadius);
            }
            if (targetCollider is CapsuleCollider cc)
            {
                return cc.radius / Mathf.Max(0.0001f, _capsuleBaseRadius);
            }
            return 1f;
        }

        void ApplyScaleInstant(float scale)
        {
            if (targetCollider is BoxCollider bc)
            {
                bc.size = _boxBaseSize * scale;
            }
            else if (targetCollider is SphereCollider sc)
            {
                sc.radius = _sphereBaseRadius * scale;
            }
            else if (targetCollider is CapsuleCollider cc)
            {
                cc.radius = _capsuleBaseRadius * scale;
                cc.height = _capsuleBaseHeight * scale;
            }
        }
    }
}
