using System.Collections.Generic;
using UnityEngine;
using YubiSoccer.Network;

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
        // 距離アンカー指定
        private enum DistanceAnchorMode { TransformPosition, RenderersBoundsCenter, ColliderClosestPoint }
        private enum BallAnchorMode { TransformPosition, RigidbodyPosition, ColliderClosestPoint }

        [Header("Target (Ball)")]
        [SerializeField] private string ballTag = "Ball";
        [SerializeField] private Transform explicitBall; // 未設定時はTag検索
        [Tooltip("タグ検索を有効にする（未定義タグでWebGLが落ちる環境ではOFF推奨）")]
        [SerializeField] private bool useTagSearch = false;
        [Tooltip("ボール未検出時の再検索間隔(秒)")]
        [SerializeField, Min(0.05f)] private float findRetryInterval = 0.5f;
        [Tooltip("追加で許可するボールタグ（複数タグを並行探索）")]
        [SerializeField] private string[] extraBallTags = new[] { "SoccerBall", "Ball" };

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
        [Tooltip("追加で試すカラー・プロパティ名（順にフォールバック）")]
        [SerializeField] private string[] extraColorPropertyCandidates = new[] { "_Tint", "_UnlitColor" };
        [Tooltip("透明描画のためのブレンド設定を可能なら適用する")]
        [SerializeField] private bool forceTransparentSettings = true;
        [Tooltip("トラブルシュート用ログを出力する")]
        [SerializeField] private bool debugLog = false;
        [Tooltip("MaterialPropertyBlock を使ってレンダラー単位で色を書き込む（マテリアル側プロパティが反映されない場合の対策）")]
        [SerializeField] private bool usePropertyBlock = true;

        [Header("Distance Anchor Options")]
        [Tooltip("ガラス側の距離アンカーの取り方。既定はTransformの位置")]
        [SerializeField] private DistanceAnchorMode glassAnchorMode = DistanceAnchorMode.TransformPosition;
        [Tooltip("ガラス側のアンカーを明示したい場合に指定（優先）")]
        [SerializeField] private Transform glassAnchorOverride;
        [Tooltip("ボール側の距離アンカーの取り方。既定はTransformの位置")]
        [SerializeField] private BallAnchorMode ballAnchorMode = BallAnchorMode.TransformPosition;
        [Tooltip("ボール側のアンカーを明示したい場合に指定（優先）")]
        [SerializeField] private Transform ballAnchorOverride;

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
        private float _nextFindTime = 0f;

        private static readonly HashSet<BreakableProximityGlass> s_instances = new HashSet<BreakableProximityGlass>();

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
            s_instances.Add(this);
            // 起動時にアルファを遠距離状態に
            SetAlpha(farAlpha);
        }

        private void OnDisable()
        {
            s_instances.Remove(this);
        }

        private void Update()
        {
            if (shattered) return;
            // 明示参照がシーン外や非アクティブになった場合は再取得
            if (ball != null)
            {
                var go = ball.gameObject;
                if (!go.activeInHierarchy || !go.scene.IsValid())
                {
                    if (debugLog)
                    {
                        Debug.Log("[BreakableProximityGlass] ボール参照が無効になったため再取得します", this);
                    }
                    ball = null;
                }
            }

            if (ball == null)
            {
                TryResolveBall();
                if (ball == null)
                {
                    if (debugLog)
                    {
                        Debug.Log($"[BreakableProximityGlass] ボール未検出。次回再試行まで待機中（{findRetryInterval:F2}s）。タグ='{ballTag}', 追加タグ数={(extraBallTags != null ? extraBallTags.Length : 0)}", this);
                    }
                    return;
                }
            }

            // 距離アンカーを計算
            Vector3 glassPos = GetGlassAnchorWorld();
            Vector3 ballPos = GetBallAnchorWorld(glassPos);
            float d = Vector3.Distance(glassPos, ballPos);
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
            // 衝突でボール参照を確定（タグ検索に失敗していた場合のフォールバック）
            if (ball == null)
            {
                var rb = other.rigidbody ?? other.collider.attachedRigidbody;
                if (rb != null) ball = rb.transform; else ball = other.transform;
            }
            Vector3 hitPoint = transform.position;
            if (other.contactCount > 0)
            {
                hitPoint = other.GetContact(0).point;
            }
            Shatter(hitPoint);
        }

        // ===========================
        // アンカー計算まわり
        // ===========================
        private Vector3 GetGlassAnchorWorld()
        {
            if (glassAnchorOverride != null) return glassAnchorOverride.position;

            switch (glassAnchorMode)
            {
                case DistanceAnchorMode.RenderersBoundsCenter:
                    if (TryGetRenderersBounds(out var b)) return b.center;
                    return transform.position;
                case DistanceAnchorMode.ColliderClosestPoint:
                    {
                        Vector3 refPos = (ball != null) ? ball.position : transform.position;
                        if (col != null)
                        {
                            return col.ClosestPoint(refPos);
                        }
                        return transform.position;
                    }
                case DistanceAnchorMode.TransformPosition:
                default:
                    return transform.position;
            }
        }

        private Vector3 GetBallAnchorWorld(Vector3 referenceForClosestPoint)
        {
            if (ball == null) return referenceForClosestPoint;
            if (ballAnchorOverride != null) return ballAnchorOverride.position;

            switch (ballAnchorMode)
            {
                case BallAnchorMode.RigidbodyPosition:
                    {
                        var rb = ball.GetComponent<Rigidbody>();
                        if (rb != null) return rb.position;
                        return ball.position;
                    }
                case BallAnchorMode.ColliderClosestPoint:
                    {
                        if (TryGetPrimaryCollider(ball, out var bc))
                        {
                            return bc.ClosestPoint(referenceForClosestPoint);
                        }
                        return ball.position;
                    }
                case BallAnchorMode.TransformPosition:
                default:
                    return ball.position;
            }
        }

        private bool TryGetRenderersBounds(out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    if (!has)
                    {
                        bounds = r.bounds;
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }
            return has;
        }

        private bool TryGetPrimaryCollider(Transform root, out Collider outCol)
        {
            outCol = null;
            if (root == null) return false;
            // 非Trigger優先で探す
            var cols = root.GetComponentsInChildren<Collider>(true);
            Collider fallback = null;
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;
                if (!c.isTrigger)
                {
                    outCol = c; return true;
                }
                if (fallback == null) fallback = c;
            }
            if (fallback != null)
            {
                outCol = fallback; return true;
            }
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (shattered) return;
            if (!IsBall(other)) return;
            // 衝突でボール参照を確定（タグ検索に失敗していた場合のフォールバック）
            if (ball == null)
            {
                var rb = other.attachedRigidbody;
                if (rb != null) ball = rb.transform; else ball = other.transform;
            }
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
            if (Time.time < _nextFindTime) return;
            _nextFindTime = Time.time + findRetryInterval;

            if (explicitBall != null)
            {
                var go = explicitBall.gameObject;
                if (go.scene.IsValid())
                {
                    ball = explicitBall;
                    if (debugLog)
                    {
                        Debug.Log($"[BreakableProximityGlass] explicitBall を採用: {ball.name}", this);
                    }
                    return;
                }
                else
                {
                    if (debugLog)
                    {
                        Debug.LogWarning("[BreakableProximityGlass] explicitBall がシーンに存在しないプレハブの可能性があります。無視して検索を続行します。", this);
                    }
                }
            }

            // 1) タグで検索（複数いたら最も近いもの）
#if !UNITY_WEBGL
            if (useTagSearch && !string.IsNullOrEmpty(ballTag))
            {
                try
                {
                    var tagged = GameObject.FindGameObjectsWithTag(ballTag);
                    if (tagged != null && tagged.Length > 0)
                    {
                        ball = ChooseNearest(tagged);
                        if (ball != null)
                        {
                            if (debugLog)
                            {
                                Debug.Log($"[BreakableProximityGlass] タグ '{ballTag}' から最近傍を採用: {ball.name}", this);
                            }
                            return;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    if (debugLog)
                    {
                        Debug.LogWarning($"[BreakableProximityGlass] タグ '{ballTag}' の検索で例外: {ex.Message}。検索をスキップしてフォールバックします。", this);
                    }
                }
            }
#endif

            // 1b) 追加タグでも検索
            if (useTagSearch && extraBallTags != null && extraBallTags.Length > 0)
            {
                List<GameObject> all = new List<GameObject>();
                for (int i = 0; i < extraBallTags.Length; i++)
                {
                    var tag = extraBallTags[i];
                    if (string.IsNullOrEmpty(tag)) continue;
                    try
                    {
                        var arr = GameObject.FindGameObjectsWithTag(tag);
                        if (arr != null && arr.Length > 0) all.AddRange(arr);
                    }
                    catch { /* 無効なタグ名は無視 */ }
                }
                if (all.Count > 0)
                {
                    ball = ChooseNearest(all.ToArray());
                    if (ball != null)
                    {
                        if (debugLog)
                        {
                            Debug.Log("[BreakableProximityGlass] 追加タグ群から最近傍を採用: " + ball.name, this);
                        }
                        return;
                    }
                }
            }

            // 2) BallNetworkSync を持つものを検索
            var syncs = Object.FindObjectsOfType<BallNetworkSync>(true);
            if (syncs != null && syncs.Length > 0)
            {
                ball = ChooseNearest(syncs);
                if (ball != null)
                {
                    if (debugLog)
                    {
                        Debug.Log("[BreakableProximityGlass] BallNetworkSync から最近傍を採用: " + ball.name, this);
                    }
                    return;
                }
            }

            // 3) Rigidbody の名前に "ball" を含むものを検索（フォールバック）
            var rbs = Object.FindObjectsOfType<Rigidbody>(true);
            Transform nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < rbs.Length; i++)
            {
                var rb = rbs[i];
                if (rb == null || rb.gameObject == null) continue;
                string n = rb.gameObject.name;
                if (string.IsNullOrEmpty(n) || !n.ToLowerInvariant().Contains("ball")) continue;
                float d = (rb.transform.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = rb.transform;
                }
            }
            if (nearest != null)
            {
                ball = nearest;
                if (debugLog)
                {
                    Debug.Log($"[BreakableProximityGlass] 名前一致でボール確定: {ball.name}", this);
                }
            }
        }

        private Transform ChooseNearest(GameObject[] gos)
        {
            Transform nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < gos.Length; i++)
            {
                var go = gos[i];
                if (go == null) continue;
                float d = (go.transform.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = go.transform;
                }
            }
            return nearest;
        }

        private Transform ChooseNearest(BallNetworkSync[] syncs)
        {
            Transform nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < syncs.Length; i++)
            {
                var s = syncs[i];
                if (s == null) continue;
                float d = (s.transform.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = s.transform;
                }
            }
            return nearest;
        }

        /// <summary>
        /// 外部からボールの再取得を即時試行する（例えばリスポーン時）
        /// </summary>
        public void ForceReacquireBall()
        {
            _nextFindTime = 0f;
            ball = null;
            TryResolveBall();
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
            bool anyApplied = false;
            for (int i = 0; i < materials.Count; i++)
            {
                var m = materials[i];
                if (m == null) continue;
                bool applied = false;
                // 1) 明示プロパティ名
                if (!string.IsNullOrEmpty(colorPropertyName))
                {
                    int customId = Shader.PropertyToID(colorPropertyName);
                    if (m.HasProperty(customId))
                    {
                        var col = m.GetColor(customId); col.a = a; m.SetColor(customId, col);
                        applied = true;
                    }
                }
                // 2) _BaseColor
                if (!applied)
                {
                    int baseId = Shader.PropertyToID("_BaseColor");
                    if (m.HasProperty(baseId))
                    {
                        var col = m.GetColor(baseId); col.a = a; m.SetColor(baseId, col);
                        applied = true;
                    }
                }
                // 3) _Color
                if (!applied)
                {
                    int colorId = Shader.PropertyToID("_Color");
                    if (m.HasProperty(colorId))
                    {
                        var col = m.GetColor(colorId); col.a = a; m.SetColor(colorId, col);
                        applied = true;
                    }
                }
                // 4) 追加候補
                if (!applied && extraColorPropertyCandidates != null)
                {
                    for (int c = 0; c < extraColorPropertyCandidates.Length; c++)
                    {
                        string prop = extraColorPropertyCandidates[c];
                        if (string.IsNullOrEmpty(prop)) continue;
                        int pid = Shader.PropertyToID(prop);
                        if (!m.HasProperty(pid)) continue;
                        var col = m.GetColor(pid); col.a = a; m.SetColor(pid, col);
                        applied = true;
                        break;
                    }
                }
                if (!applied && debugLog)
                {
                    Debug.LogWarning($"[BreakableProximityGlass] 透明度プロパティが見つかりません: material='{m.name}', shader='{m.shader?.name}'", this);
                }

                anyApplied |= applied;
            }

            // レンダラー単位の PropertyBlock でも上書き（必要に応じて）
            if (usePropertyBlock && renderers != null && renderers.Length > 0)
            {
                // 候補プロパティリストを用意
                var props = BuildColorPropertyCandidates();
                for (int r = 0; r < renderers.Length; r++)
                {
                    var rend = renderers[r];
                    if (rend == null) continue;
                    var block = new MaterialPropertyBlock();
                    rend.GetPropertyBlock(block);
                    bool wrote = false;
                    for (int p = 0; p < props.Count; p++)
                    {
                        int pid = props[p];
                        // 現在色を各種プロパティから推定（取得できなければ白）
                        Color baseCol;
                        bool got = TryGetRendererColor(rend, pid, out baseCol);
                        if (!got) baseCol = new Color(1, 1, 1, a);
                        baseCol.a = a;
                        block.SetColor(pid, baseCol);
                        wrote = true;
                        // 1つ成功したら十分
                        break;
                    }
                    if (wrote)
                    {
                        rend.SetPropertyBlock(block);
                        anyApplied = true;
                    }
                }
            }

            if (!anyApplied && debugLog)
            {
                Debug.LogWarning("[BreakableProximityGlass] いずれの経路でもアルファ適用に失敗しました。マテリアルのシェーダとカラー・プロパティ名を確認してください。", this);
            }
        }

        /// <summary>
        /// プレハブ生成側などから、このガラスにボールTransformを外部注入する。
        /// </summary>
        public void SetBall(Transform t)
        {
            ball = t;
            _nextFindTime = 0f;
            if (debugLog && ball != null)
            {
                Debug.Log($"[BreakableProximityGlass] 外部からボール参照を受領: {ball.name}", this);
            }
        }

        /// <summary>
        /// シーン内すべての BreakableProximityGlass に対して同一のボールTransformを登録する（スポーナーから呼び出し）
        /// </summary>
        public static void RegisterBallForAll(Transform t)
        {
            foreach (var inst in s_instances)
            {
                if (inst == null) continue;
                inst.SetBall(t);
            }
        }

        // カラー候補プロパティIDのリストを構築
        private List<int> BuildColorPropertyCandidates()
        {
            var list = new List<int>(8);
            if (!string.IsNullOrEmpty(colorPropertyName)) list.Add(Shader.PropertyToID(colorPropertyName));
            list.Add(Shader.PropertyToID("_BaseColor"));
            list.Add(Shader.PropertyToID("_Color"));
            if (extraColorPropertyCandidates != null)
            {
                for (int i = 0; i < extraColorPropertyCandidates.Length; i++)
                {
                    var s = extraColorPropertyCandidates[i];
                    if (string.IsNullOrEmpty(s)) continue;
                    list.Add(Shader.PropertyToID(s));
                }
            }
            return list;
        }

        // レンダラーから現在色を推定（任意の候補IDで）
        private bool TryGetRendererColor(Renderer r, int pid, out Color col)
        {
            // マテリアルから取得（sharedMaterial 優先）
            if (r != null)
            {
                var sm = r.sharedMaterial;
                if (sm != null && sm.HasProperty(pid))
                {
                    col = sm.GetColor(pid);
                    return true;
                }
                var m = r.material;
                if (m != null && m.HasProperty(pid))
                {
                    col = m.GetColor(pid);
                    return true;
                }
            }
            col = default;
            return false;
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
            // よくあるキーワード（存在しない場合は無視される）
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            // Built-in Standard 対応
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f); // Transparent
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // ギズモで距離アンカーを可視化
            var prevCol = Gizmos.color;
            // 可能なら最新のコライダー参照
            if (col == null) col = GetComponent<Collider>();

            Vector3 g = transform.position;
            // renderers 配列は未初期化の可能性があるため安全に
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            // 実際のアンカー関数は ball を参照するため、エディタ上では近似
            switch (glassAnchorMode)
            {
                case DistanceAnchorMode.RenderersBoundsCenter:
                    if (TryGetRenderersBounds(out var b)) g = b.center; else g = transform.position;
                    break;
                case DistanceAnchorMode.ColliderClosestPoint:
                    if (col != null) g = col.ClosestPoint(transform.position); else g = transform.position;
                    break;
                case DistanceAnchorMode.TransformPosition:
                default:
                    g = transform.position;
                    break;
            }
            if (glassAnchorOverride != null) g = glassAnchorOverride.position;

            Vector3 bp = (ball != null ? ball.position : g + Vector3.forward * 1.0f);
            if (ballAnchorOverride != null) bp = ballAnchorOverride.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(g, 0.07f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(bp, 0.07f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(g, bp);
            Gizmos.color = prevCol;
        }
#endif
    }
}
