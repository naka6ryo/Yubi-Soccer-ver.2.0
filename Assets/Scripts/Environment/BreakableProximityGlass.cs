using System.Collections.Generic;
using UnityEngine;
using YubiSoccer.Network;
using YubiSoccer.Game;
using YubiSoccer.Field;

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
        [SerializeField] private Confetti redGoalParticle;
        [SerializeField] private Confetti blueGoalParticle;
        private GoalTrigger goalTrigger;

        // 距離アンカー指定
        private enum DistanceAnchorMode { TransformPosition, RenderersBoundsCenter, ColliderClosestPoint }
        private enum BallAnchorMode { TransformPosition, RigidbodyPosition, ColliderClosestPoint }
        private enum MidlineAxis { LocalX, LocalY, LocalZ }

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

        [Header("Distance Mode")]
        [Tooltip("オブジェクトの横方向中線に対する距離を使うか（true の場合は中心/コライダー距離ではなく中線からの横方向距離を使用）")]
        [SerializeField] private bool useMidlineDistance = true;
        [Tooltip("中線として扱うローカル軸。通常は横方向（LocalX）を選択してください。")]
        [SerializeField] private MidlineAxis midlineAxis = MidlineAxis.LocalX;

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
        [Tooltip("ガラス側の距離アンカーの取り方。既定はオブジェクトの端に近づいた際にも反応するコライダー最短点（ColliderClosestPoint）")]
        [SerializeField] private DistanceAnchorMode glassAnchorMode = DistanceAnchorMode.ColliderClosestPoint;
        [Tooltip("ガラス側のアンカーを明示したい場合に指定（優先）")]
        [SerializeField] private Transform glassAnchorOverride;
        [Tooltip("ボール側の距離アンカーの取り方。既定はTransformの位置")]
        [SerializeField] private BallAnchorMode ballAnchorMode = BallAnchorMode.TransformPosition;
        [Tooltip("ボール側のアンカーを明示したい場合に指定（優先）")]
        [SerializeField] private Transform ballAnchorOverride;

        [Header("Shatter")]
        [Tooltip("割れた状態のプレハブ(破片含む)。未割当だと見た目のみ非表示にします")]
        [SerializeField] private GameObject shatteredPrefab;
        // （爆発力のパラメータは不要になったため削除）
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
        // シャッター用モード: true の間は破片を自動破棄しない（シーン遷移まで保持する用途）
        private static bool s_keepShardsUntilSceneChange = false;
        private static bool s_sceneChangeHandlerRegistered = false;

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

            float d;
            if (useMidlineDistance && ball != null)
            {
                // 指定オブジェクトの ZY 平面（local x = 0）に平行な直方体面への最短距離を計算する
                // 具体的には、ローカル座標系で球の位置を求め、y/z を直方体の範囲にクランプした点を
                // 面上の最近接点とみなし、その点との距離を d とする。
                Vector3 localBall = transform.InverseTransformPoint(ballPos);
                Vector3 halfExtents = GetLocalBoundsHalfExtents(); // ローカルでの半長さ (x,y,z)

                // 直方体面は local x = 0 にあり、y/z が [-halfExtents.y, halfExtents.y], [-halfExtents.z, halfExtents.z]
                float cy = Mathf.Clamp(localBall.y, -halfExtents.y, halfExtents.y);
                float cz = Mathf.Clamp(localBall.z, -halfExtents.z, halfExtents.z);
                Vector3 nearest = new Vector3(0f, cy, cz);
                d = (localBall - nearest).magnitude;
            }
            else
            {
                // 既存の挙動: 可能ならコライダー表面間距離、なければ中心間距離
                if (glassAnchorMode == DistanceAnchorMode.ColliderClosestPoint && col != null && ball != null)
                {
                    if (TryGetPrimaryCollider(ball, out var ballCol) && ballCol != null)
                    {
                        Vector3 glassClosest = col.ClosestPoint(ball.position);
                        Vector3 ballClosest = ballCol.ClosestPoint(glassClosest);
                        d = Vector3.Distance(glassClosest, ballClosest);
                    }
                    else
                    {
                        Vector3 glassClosest = col.ClosestPoint(ballPos);
                        d = Vector3.Distance(glassClosest, ballPos);
                    }
                }
                else
                {
                    d = Vector3.Distance(glassPos, ballPos);
                }
            }
            float interp = 0f;
            if (Mathf.Approximately(maxDistance, minDistance))
            {
                interp = d <= minDistance ? 1f : 0f;
            }
            else
            {
                // d>=max→0, d<=min→1 となる 0..1 値
                interp = Mathf.InverseLerp(maxDistance, minDistance, d);
            }
            if (fadeCurve != null)
                interp = Mathf.Clamp01(fadeCurve.Evaluate(Mathf.Clamp01(interp)));
            float a = Mathf.Lerp(farAlpha, nearAlpha, interp);

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

            // 1) 自身の Collider を優先
            var self = root.GetComponent<Collider>();
            Collider fallback = null;
            if (self != null)
            {
                if (!self.isTrigger)
                {
                    outCol = self; return true;
                }
                fallback = self;
            }

            // 2) 子供を検索（自身は除外）
            var childCols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < childCols.Length; i++)
            {
                var c = childCols[i];
                if (c == null || c == self) continue;
                if (!c.isTrigger)
                {
                    outCol = c; return true;
                }
                if (fallback == null) fallback = c;
            }

            // 3) 親方向も検索（自身/子で見つからない場合）
            var parentCols = root.GetComponentsInParent<Collider>(true);
            for (int i = 0; i < parentCols.Length; i++)
            {
                var c = parentCols[i];
                if (c == null || c == self) continue;
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
            if (shattered) return;
            shattered = true;

            // 見た目を消す
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null) r.enabled = false;
                }
            }
            if (col != null) col.enabled = false;

            SpawnShatteredPrefab();
            HandleOriginalObjectLifecycle();
            PlayConfetti();
        }

        private void SpawnShatteredPrefab()
        {
            if (shatteredPrefab == null)
            {
                lastShards = null;
                return;
            }

            var parent = transform.parent;
            var shards = Instantiate(shatteredPrefab, transform.position, transform.rotation, parent);
            shards.transform.localScale = transform.localScale;
            lastShards = shards;

            if (!s_keepShardsUntilSceneChange && autoDestroyShardsAfter > 0f)
            {
                Destroy(shards, autoDestroyShardsAfter);
            }
        }

        private void HandleOriginalObjectLifecycle()
        {
            if (keepOriginalForRespawn)
            {
                return;
            }

            if (destroyOriginalDelay <= 0f)
            {
                Destroy(gameObject);
            }
            else
            {
                Destroy(gameObject, destroyOriginalDelay);
            }
        }

        /// <summary>
        /// シーン遷移までシャッター（破片）を保持するモードを有効にして、全インスタンスを即座に割る。
        /// Result 表示などで呼ぶ想定。
        /// </summary>
        public static void StartShutterForAll()
        {
            if (s_keepShardsUntilSceneChange) return;
            s_keepShardsUntilSceneChange = true;
            // シーン切替時にモードを解除して破片を片付けるハンドラを登録
            if (!s_sceneChangeHandlerRegistered)
            {
                UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
                s_sceneChangeHandlerRegistered = true;
            }
            foreach (var inst in s_instances)
            {
                if (inst == null) continue;
                // 即時で割る（位置は各インスタンスの中心）
                inst.Shatter(inst.transform.position);
            }
        }

        private static void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
        {
            // シーンが変わったら保持モードを解除し、残された破片を片付ける
            s_keepShardsUntilSceneChange = false;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            s_sceneChangeHandlerRegistered = false;
            foreach (var inst in s_instances)
            {
                if (inst == null) continue;
                inst.CleanupShardsAfterSceneChange();
            }
        }

        private void CleanupShardsAfterSceneChange()
        {
            if (lastShards != null)
            {
                Destroy(lastShards);
                lastShards = null;
            }
        }

        /// <summary>
        /// 割れ状態から元の見た目/当たりに復元する。
        /// </summary>
        public void ResetIntact()
        {
            HideConfetti();
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

        // 指定のローカル軸に沿った中線の半長さ（ローカル座標系）を返す。
        // まず Renderer のバウンディングボックスを優先し、なければ Collider.bounds を利用する。
        private float GetMidlineHalfLength(Vector3 localAxis)
        {
            // world-space bounds を取得
            Bounds b;
            bool haveBounds = TryGetRenderersBounds(out b);
            if (!haveBounds && col != null)
            {
                b = col.bounds;
                haveBounds = true;
            }
            if (!haveBounds)
            {
                // フォールバック: 小さめの値
                return 0.5f;
            }

            // bounds の 8 コーナーをローカル空間に変換して、指定軸に沿った min/max を求める
            Vector3[] corners = new Vector3[8];
            Vector3 ext = b.extents;
            Vector3 c = b.center;
            int idx = 0;
            for (int xi = -1; xi <= 1; xi += 2)
            {
                for (int yi = -1; yi <= 1; yi += 2)
                {
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 worldCorner = c + Vector3.Scale(ext, new Vector3(xi, yi, zi));
                        corners[idx++] = transform.InverseTransformPoint(worldCorner);
                    }
                }
            }

            // localAxis はローカル単位ベクトル（x/y/z）で渡される想定
            // axisValue = dot(corner, localAxis)
            float minv = float.PositiveInfinity;
            float maxv = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                float v = Vector3.Dot(corners[i], localAxis);
                if (v < minv) minv = v;
                if (v > maxv) maxv = v;
            }
            float halfLength = (maxv - minv) * 0.5f;
            // 安全側の最低値
            if (halfLength <= 0f) halfLength = 0.5f;
            return halfLength;
        }

        // ローカル座標系での bounds の半長さを (x,y,z) で返す。Renderer bounds を優先し、なければ Collider.bounds を使用。
        private Vector3 GetLocalBoundsHalfExtents()
        {
            Bounds b;
            bool haveBounds = TryGetRenderersBounds(out b);
            if (!haveBounds && col != null)
            {
                b = col.bounds;
                haveBounds = true;
            }
            if (!haveBounds)
            {
                return new Vector3(0.5f, 0.5f, 0.5f);
            }

            // ワールド空間の bounds のコーナーをローカルに変換して min/max を求める
            Vector3 ext = b.extents;
            Vector3 c = b.center;
            float minx = float.PositiveInfinity, miny = float.PositiveInfinity, minz = float.PositiveInfinity;
            float maxx = float.NegativeInfinity, maxy = float.NegativeInfinity, maxz = float.NegativeInfinity;
            for (int xi = -1; xi <= 1; xi += 2)
            {
                for (int yi = -1; yi <= 1; yi += 2)
                {
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 worldCorner = c + Vector3.Scale(ext, new Vector3(xi, yi, zi));
                        Vector3 local = transform.InverseTransformPoint(worldCorner);
                        if (local.x < minx) minx = local.x;
                        if (local.x > maxx) maxx = local.x;
                        if (local.y < miny) miny = local.y;
                        if (local.y > maxy) maxy = local.y;
                        if (local.z < minz) minz = local.z;
                        if (local.z > maxz) maxz = local.z;
                    }
                }
            }
            Vector3 half = new Vector3((maxx - minx) * 0.5f, (maxy - miny) * 0.5f, (maxz - minz) * 0.5f);
            if (half.x <= 0f) half.x = 0.5f;
            if (half.y <= 0f) half.y = 0.5f;
            if (half.z <= 0f) half.z = 0.5f;
            return half;
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

        private void PlayConfetti()
        {
            if (goalTrigger == null) goalTrigger = GetComponent<GoalTrigger>();
            if (goalTrigger == null) return;

            switch (goalTrigger.AwardToTeam)
            {
                case Team.TeamA:
                    redGoalParticle?.Show();
                    blueGoalParticle?.Hide();
                    break;
                case Team.TeamB:
                    blueGoalParticle?.Show();
                    redGoalParticle?.Hide();
                    break;
            }
        }

        private void HideConfetti()
        {
            redGoalParticle?.Hide();
            blueGoalParticle?.Hide();
        }
    }
}
