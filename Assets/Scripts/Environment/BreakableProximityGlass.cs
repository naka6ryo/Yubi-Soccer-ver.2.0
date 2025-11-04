using System.Collections.Generic;
using UnityEngine;

namespace YubiSoccer.Environment
{
    /// <summary>
    /// ボールが近づくほどオブジェクトを透明にし、衝突時に割れプレハブへ差し替えて爆散させるコンポーネント。
    /// - Renderer 配下のマテリアルの _BaseColor/_Color アルファを制御（URP Unlit/Standard想定）
    /// - 透明描画用に必要なブレンド設定を可能な範囲で付与
    /// - 衝突時、shatteredPrefab を生成し、その子の Rigidbody に爆発力を与える
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class BreakableProximityGlass : MonoBehaviour
    {
        [Header("Target (Ball)")]
        [SerializeField] private string ballTag = "Ball";
        [SerializeField] private Transform explicitBall; // 未設定時はTag検索

        [Header("Fade by Distance")]
        [Tooltip("この距離以下で最も透明(nearAlpha)になる")]
        [SerializeField, Min(0f)] private float minDistance = 0.5f;
        [Tooltip("この距離以上で最も不透明(farAlpha)になる")]
        [SerializeField, Min(0f)] private float maxDistance = 5f;
        [Tooltip("遠いときのアルファ(不透明度)。1=不透明、0=完全透明")]
        [SerializeField, Range(0f, 1f)] private float farAlpha = 1f;
        [Tooltip("近いときのアルファ(不透明度)。1=不透明、0=完全透明")]
        [SerializeField, Range(0f, 1f)] private float nearAlpha = 0.1f;
        [Tooltip("距離→透明度の応答。未設定なら線形")]
        [SerializeField] private AnimationCurve fadeCurve = null; // 0..1

        [Header("Renderers")]
        [Tooltip("アルファを操作する対象Renderer。未指定時は自身と子から自動収集")]
        [SerializeField] private Renderer[] renderers;
        [Tooltip("_BaseColor/_Color などの色プロパティ名。空なら自動推定")]
        [SerializeField] private string colorPropertyName = "";
        [Tooltip("透明描画のためのブレンド設定を可能なら適用する")]
        [SerializeField] private bool forceTransparentSettings = true;

        [Header("Shatter")]
        [Tooltip("割れた状態のプレハブ(破片含む)。未割当だと見た目のみ非表示にします")]
        [SerializeField] private GameObject shatteredPrefab;
        [Tooltip("爆散力(Impulse)。0で爆発なし")]
        [SerializeField, Min(0f)] private float explosionForce = 2.5f;
        [SerializeField, Min(0f)] private float explosionRadius = 1.5f;
        [SerializeField] private float upwardsModifier = 0.2f;
        [Tooltip("割れ後、元オブジェクトを消すまでの秒数。0で即時")]
        [SerializeField, Min(0f)] private float destroyOriginalDelay = 0.05f;
        [Tooltip("割れプレハブを自動破棄する秒数。0以下で破棄しない")]
        [SerializeField] private float autoDestroyShardsAfter = 10f;
        [Tooltip("trueで元オブジェクトを破棄せず、後から復元可能にする")]
        [SerializeField] private bool keepOriginalForRespawn = true;

        private Transform ball;
        private bool shattered = false;
        private List<Material> materials = new List<Material>();
        private int colorPropId = -1;
        private Collider col;
        private GameObject lastShards;

        private void Awake()
        {
            col = GetComponent<Collider>();
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
            CacheMaterials();
            TryResolveBall();
        }

        private void OnEnable()
        {
            // 起動時にアルファを遠距離状態に
            SetAlpha(farAlpha);
        }

        private void Update()
        {
            if (shattered) return;
            if (ball == null) TryResolveBall();
            if (ball == null) return;

            float d = Vector3.Distance(transform.position, ball.position);
            float t = 0f;
            if (Mathf.Approximately(maxDistance, minDistance))
            {
                t = d <= minDistance ? 1f : 0f;
            }
            else
            {
                // d>=max→0, d<=min→1 となる 0..1 値
                t = Mathf.InverseLerp(maxDistance, minDistance, d);
            }
            if (fadeCurve != null)
                t = Mathf.Clamp01(fadeCurve.Evaluate(Mathf.Clamp01(t)));
            float a = Mathf.Lerp(farAlpha, nearAlpha, t);
            SetAlpha(a);
        }

        private void OnCollisionEnter(Collision other)
        {
            if (shattered) return;
            if (!IsBall(other.collider)) return;
            Vector3 hitPoint = transform.position;
            if (other.contactCount > 0)
            {
                hitPoint = other.GetContact(0).point;
            }
            Shatter(hitPoint);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (shattered) return;
            if (!IsBall(other)) return;
            Shatter(transform.position);
        }

        private void Shatter(Vector3 explosionCenter)
        {
            shattered = true;
            // 見た目を消す
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = false;
            }
            if (col != null) col.enabled = false;

            GameObject shards = null;
            if (shatteredPrefab != null)
            {
                shards = Instantiate(shatteredPrefab, transform.position, transform.rotation);
                if (explosionForce > 0f)
                {
                    var rbs = shards.GetComponentsInChildren<Rigidbody>();
                    foreach (var rb in rbs)
                    {
                        if (rb == null) continue;
                        rb.AddExplosionForce(explosionForce, explosionCenter, Mathf.Max(0.01f, explosionRadius), upwardsModifier, ForceMode.Impulse);
                    }
                }
                if (autoDestroyShardsAfter > 0f)
                {
                    Destroy(shards, autoDestroyShardsAfter);
                }
            }
            lastShards = shards;
            // 元オブジェクトの破棄 or 復元用に保持
            if (!keepOriginalForRespawn)
            {
                if (destroyOriginalDelay <= 0f) Destroy(gameObject);
                else Destroy(gameObject, destroyOriginalDelay);
            }
        }

        /// <summary>
        /// 割れ状態から元の見た目/当たりに復元する。
        /// </summary>
        public void ResetIntact()
        {
            // 破片が残っていれば片付け
            if (lastShards != null)
            {
                Destroy(lastShards);
                lastShards = null;
            }
            // 見た目/当たり復帰
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null) r.enabled = true;
                }
            }
            if (col != null) col.enabled = true;
            shattered = false;
            // アルファ/距離フェードを初期状態へ
            SetAlpha(farAlpha);
        }

        private bool IsBall(Collider c)
        {
            if (c == null) return false;
            if (!string.IsNullOrEmpty(ballTag) && c.CompareTag(ballTag)) return true;
            var rb = c.attachedRigidbody;
            if (rb != null && rb.gameObject.name.ToLowerInvariant().Contains("ball")) return true;
            return false;
        }

        private void TryResolveBall()
        {
            if (explicitBall != null) { ball = explicitBall; return; }
            if (string.IsNullOrEmpty(ballTag)) return;
            var go = GameObject.FindGameObjectWithTag(ballTag);
            if (go != null) ball = go.transform;
        }

        private void CacheMaterials()
        {
            materials.Clear();
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                // インスタンス化されたマテリアルで個別制御
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    if (forceTransparentSettings) EnsureTransparentSettings(m);
                    materials.Add(m);
                }
            }
            // カラー用プロパティID決定
            if (!string.IsNullOrEmpty(colorPropertyName))
            {
                colorPropId = Shader.PropertyToID(colorPropertyName);
            }
            else
            {
                // _BaseColor 優先、なければ _Color
                colorPropId = Shader.PropertyToID("_BaseColor");
                bool allHaveBase = true;
                for (int i = 0; i < materials.Count; i++)
                {
                    if (!materials[i].HasProperty(colorPropId)) { allHaveBase = false; break; }
                }
                if (!allHaveBase)
                {
                    colorPropId = Shader.PropertyToID("_Color");
                }
            }
        }

        private void SetAlpha(float a)
        {
            a = Mathf.Clamp01(a);
            for (int i = 0; i < materials.Count; i++)
            {
                var m = materials[i];
                if (m == null) continue;
                if (m.HasProperty(colorPropId))
                {
                    var col = m.GetColor(colorPropId);
                    col.a = a;
                    m.SetColor(colorPropId, col);
                }
            }
        }

        private void EnsureTransparentSettings(Material mat)
        {
            if (mat == null) return;
            // URP Lit/Unlit の簡易設定
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
