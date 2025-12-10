using UnityEngine;

namespace YubiSoccer.Player
{
    /// <summary>
    /// プレイヤー(など)を中心に地面上へ円を描画するインジケーター。
    /// LineRenderer を用いて半径と中心を更新するだけの軽量実装。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class KickRadiusIndicator : MonoBehaviour
    {
        [Header("Circle Visual")]
        [SerializeField, Min(3)] private int segments = 64;
        [SerializeField] private float lineWidth = 0.05f;
        [SerializeField] private Color lineColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        [Tooltip("マテリアルの色よりもインスペクターの lineColor を優先して適用する")]
        [SerializeField] private bool overrideMaterialColor = true;
        [Tooltip("円を表示する高さオフセット(中心Y + offset)")]
        [SerializeField] private float yOffset = 0.02f;

        [Header("Target")]
        [SerializeField] private Transform center; // 中心(通常はプレイヤー)

        private LineRenderer lr;
        [SerializeField] private LineRenderer pulseLr; // パルス用のリング（子オブジェクトに保持）
        private float radius = 1f;

        [Header("Pulse Effect")]
        [Tooltip("パルス(0→現在半径に拡大するリング)を繰り返し表示する")]
        [SerializeField] private bool enablePulseEffect = true;
        [Tooltip("ゼロチャージ時のパルス間隔(秒)。チャージが低いほど間隔は長い")]
        [SerializeField] private float pulseMaxInterval = 1.0f;
        [Tooltip("フルチャージ時のパルス間隔(秒)。チャージが高いほど間隔は短い")]
        [SerializeField] private float pulseMinInterval = 0.25f;
        [Tooltip("チャージ率→パルス間隔短縮のカーブ(0..1)。0でmaxInterval、1でminIntervalへ")]
        [SerializeField] private AnimationCurve chargeToPulseInterval = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("1回のパルスに要する時間。interval×この係数")]
        [SerializeField, Range(0.1f, 1.0f)] private float pulseDurationFraction = 1.0f;
        [Tooltip("パルスの開始時アルファ(現在の色に乗算)")]
        [SerializeField, Range(0f, 1f)] private float pulseAlphaStart = 0.8f;
        [Tooltip("パルスの終了時アルファ(現在の色に乗算)")]
        [SerializeField, Range(0f, 1f)] private float pulseAlphaEnd = 0.0f;
        [Tooltip("パルスリングの太さ倍率(基準線幅×倍率)")]
        [SerializeField] private float pulseWidthMultiplier = 1.0f;

        // ランタイム状態
        private float pulseTimer = 0f;
        private bool pulseActive = false;
        private float pulseProgress = 1f; // 1=非表示

        private void Awake()
        {
            lr = GetComponent<LineRenderer>();
            if (lr == null) lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = lineWidth;

            EnsureMaterial();
            // グラデーションは白固定、色はマテリアルで管理（テクスチャ未設定によるグレー化を回避）
            lr.startColor = Color.white;
            lr.endColor = Color.white;
            ApplyMaterialColor(lineColor);

            lr.enabled = false; // 初期は非表示

            // パルス用LineRendererを準備（子オブジェクトに作成して分離）
            if (pulseLr == null)
            {
                var child = transform.Find("PulseRing");
                if (child == null)
                {
                    var go = new GameObject("PulseRing");
                    go.transform.SetParent(transform, false);
                    child = go.transform;
                }
                pulseLr = child.GetComponent<LineRenderer>();
                if (pulseLr == null)
                {
                    pulseLr = child.gameObject.AddComponent<LineRenderer>();
                }
            }
            if (pulseLr != null)
            {
                pulseLr.useWorldSpace = true;
                pulseLr.loop = true;
                pulseLr.widthMultiplier = lineWidth * pulseWidthMultiplier;
                pulseLr.positionCount = 0;
                // ベースと同じマテリアルを共有（色は頂点色で乗算制御）
                if (lr != null && lr.sharedMaterial != null)
                    pulseLr.sharedMaterial = lr.sharedMaterial;
                else
                    EnsureMaterialFor(pulseLr);
                // 色は都度Updateで設定するため初期は白
                pulseLr.startColor = Color.white;
                pulseLr.endColor = Color.white;
                pulseLr.enabled = false;
            }
        }

        private void OnEnable()
        {
            // 有効化時にも色適用を保証（他所でマテリアルが入れ替わっても上書きできるように）
            EnsureMaterial();
            if (overrideMaterialColor)
                ApplyMaterialColor(lineColor);
        }

        private void OnValidate()
        {
            lr = GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.widthMultiplier = lineWidth;
                EnsureMaterial();
                lr.startColor = Color.white;
                lr.endColor = Color.white;
                if (overrideMaterialColor)
                    ApplyMaterialColor(lineColor);
            }
            if (pulseLr == null)
            {
                // 子から取得（なければ作成）
                var child = transform.Find("PulseRing");
                if (child == null)
                {
                    var go = new GameObject("PulseRing");
                    go.transform.SetParent(transform, false);
                    child = go.transform;
                }
                pulseLr = child.GetComponent<LineRenderer>();
                if (pulseLr == null)
                    pulseLr = child.gameObject.AddComponent<LineRenderer>();
            }
            if (pulseLr != null)
            {
                pulseLr.widthMultiplier = lineWidth * pulseWidthMultiplier;
                EnsureMaterialFor(pulseLr);
                pulseLr.startColor = Color.white;
                pulseLr.endColor = Color.white;
                pulseLr.enabled = false;
            }
            Rebuild();
        }

        public void Show()
        {
            if (lr != null)
            {
                lr.enabled = true;
                if (overrideMaterialColor)
                    ApplyMaterialColor(lineColor);
            }
        }

        public void Hide()
        {
            if (lr != null) lr.enabled = false;
            ResetPulse();
        }

        public void SetCenter(Transform t)
        {
            center = t;
            Rebuild();
        }

        public void SetRadius(float r)
        {
            radius = Mathf.Max(0f, r);
            Rebuild();
        }

        public void SetColor(Color c)
        {
            lineColor = c;
            if (lr != null)
            {
                lr.startColor = Color.white;
                lr.endColor = Color.white;
                if (overrideMaterialColor)
                    ApplyMaterialColor(c);
            }
        }

        public void SetWidth(float w)
        {
            lineWidth = Mathf.Max(0.001f, w);
            if (lr != null)
            {
                lr.widthMultiplier = lineWidth;
            }
        }

        public void SetSegments(int seg)
        {
            segments = Mathf.Max(3, seg);
            Rebuild();
        }

        private void Rebuild()
        {
            if (lr == null || center == null)
                return;

            int count = Mathf.Max(3, segments);
            lr.positionCount = count;

            Vector3 cpos = center.position;
            float y = cpos.y + yOffset;
            float step = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                float a = step * i;
                float x = cpos.x + Mathf.Cos(a) * radius;
                float z = cpos.z + Mathf.Sin(a) * radius;
                lr.SetPosition(i, new Vector3(x, y, z));
            }
        }

        private void RebuildPulse(float r)
        {
            if (pulseLr == null || center == null) return;
            int count = Mathf.Max(3, segments);
            pulseLr.positionCount = count;

            Vector3 cpos = center.position;
            float y = cpos.y + yOffset;
            float step = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                float a = step * i;
                float x = cpos.x + Mathf.Cos(a) * r;
                float z = cpos.z + Mathf.Sin(a) * r;
                pulseLr.SetPosition(i, new Vector3(x, y, z));
            }
        }

        private void EnsureMaterial()
        {
            if (lr == null) return;
            // Prefab上で material にアクセスすると例外が出るため sharedMaterial を使う
            if (lr.sharedMaterial == null)
            {
                // URPのUnlit優先、なければBuilt-inのUnlit/Color、最後にSprites/Default
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    lr.sharedMaterial = new Material(shader);
                }
            }
        }

        private void EnsureMaterialFor(LineRenderer target)
        {
            if (target == null) return;
            if (target.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    target.sharedMaterial = new Material(shader);
                }
            }
        }

        private void ApplyMaterialColor(Color c)
        {
            if (lr == null) return;
            // まず sharedMaterial を使用（Prefabやエディタでも安全）
            Material mat = lr.sharedMaterial;
            if (mat == null)
            {
                // 実行時で sharedMaterial が未設定の場合のみ material を参照
                if (Application.isPlaying)
                {
                    mat = lr.material;
                }
            }
            if (mat == null) return;

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", c);
        }

        /// <summary>
        /// 現在の円の基準色を取得（SetColor で更新された lineColor を返す）
        /// </summary>
        public Color GetCurrentColor() => lineColor;

        /// <summary>
        /// 充電中パルスの更新。毎フレーム呼び出しを想定。
        /// </summary>
        /// <param name="targetRadius">このフレームの到達半径（0→targetRadiusに拡大）</param>
        /// <param name="currentColor">現在の基準色（本体円の色）</param>
        /// <param name="charge01">チャージ率0..1（間隔計算に使用）</param>
        /// <param name="dt">デルタタイム</param>
        public void UpdatePulseEffect(float targetRadius, Color currentColor, float charge01, float dt)
        {
            if (!enablePulseEffect)
            {
                if (pulseLr != null) pulseLr.enabled = false;
                return;
            }
            if (pulseLr == null || center == null) return;

            float t = Mathf.Clamp01(chargeToPulseInterval != null ? chargeToPulseInterval.Evaluate(Mathf.Clamp01(charge01)) : Mathf.Clamp01(charge01));
            float interval = Mathf.Lerp(pulseMaxInterval, pulseMinInterval, t);
            float duration = Mathf.Clamp(interval * Mathf.Clamp01(pulseDurationFraction), 0.05f, 10f);

            if (!pulseActive)
            {
                pulseTimer += dt;
                if (pulseTimer >= interval)
                {
                    pulseActive = true;
                    pulseTimer = 0f;
                    pulseProgress = 0f;
                    pulseLr.enabled = true;
                }
            }

            if (pulseActive)
            {
                pulseProgress += (duration > 0f ? dt / duration : 1f);
                float p = Mathf.Clamp01(pulseProgress);
                float r = Mathf.Lerp(0f, Mathf.Max(0f, targetRadius), p);
                pulseLr.widthMultiplier = Mathf.Max(0.001f, lineWidth * pulseWidthMultiplier);
                RebuildPulse(r);

                // 色（アルファフェード）
                Color c0 = currentColor; c0.a *= Mathf.Clamp01(pulseAlphaStart);
                Color c1 = currentColor; c1.a *= Mathf.Clamp01(pulseAlphaEnd);
                var grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(c0.r, c0.g, c0.b), 0f), new GradientColorKey(new Color(c1.r, c1.g, c1.b), 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(c0.a, 0f), new GradientAlphaKey(c1.a, 1f) }
                );
                pulseLr.colorGradient = grad;

                if (p >= 1f)
                {
                    pulseActive = false;
                    pulseLr.enabled = false; // 1回のパルス完了で非表示
                }
            }
        }

        /// <summary>
        /// パルス状態をリセットして非表示にする
        /// </summary>
        public void ResetPulse()
        {
            pulseTimer = 0f;
            pulseActive = false;
            pulseProgress = 1f;
            if (pulseLr != null)
            {
                pulseLr.enabled = false;
                pulseLr.positionCount = 0;
            }
        }
    }
}
