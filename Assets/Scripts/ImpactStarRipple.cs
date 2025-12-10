using System.Collections;
using UnityEngine;

namespace YubiSoccer.VFX
{
    /// <summary>
    /// 星形(★)のリップルをその場で拡大/フェードさせて自動破棄する簡易VFX。
    /// 任意の法線に向けて配置され、LineRendererで描画されます。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ImpactStarRipple : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField, Min(3)] private int spikes = 5; // 星の尖り数
        [SerializeField, Range(0.1f, 0.99f)] private float innerRadiusRatio = 0.45f; // 内側半径/外側半径
        [SerializeField] private float lineWidth = 0.04f;
        [SerializeField] private float maxRadius = 0.5f;
        [Tooltip("星の塗りつぶしを描画する")]
        [SerializeField] private bool fillStar = true;
        [Tooltip("星のアウトライン(Line)も描画する")]
        [SerializeField] private bool showOutline = true;

        [Header("Timing")]
        [SerializeField] private float duration = 0.35f; // 拡大フェーズの時間
        [SerializeField] private AnimationCurve radiusCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [Tooltip("拡大後に最大サイズのまま維持する時間")]
        [SerializeField] private float holdTime = 0f;
        [Tooltip("保持後にフェードアウトする時間(0で即時消滅)")]
        [SerializeField] private float fadeDuration = 0.2f;
        [Tooltip("保持中の可視アルファ(0..1)")]
        [SerializeField, Range(0f, 1f)] private float holdAlpha = 1f;

        [Header("Visual")]
        [SerializeField] private Color color = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private float surfaceOffset = 0.01f; // 面から少し浮かせてZファイティング回避
        [Tooltip("URPのBloomに乗せるための明るさ(>1で強い発光)。マテリアルのベースカラーに乗算")]
        [SerializeField] private float bloomIntensity = 2.5f;
        [Tooltip("加算合成で描画(Transparent+Additive)するかどうか")]
        [SerializeField] private bool additiveBlend = true;

        // 線状の谷ストリークは削除（要望により無効化）

        private LineRenderer lr;
        private MeshFilter mf;
        private MeshRenderer mr;
        private Mesh mesh;
        private Transform tr;
        private Vector3 normal = Vector3.up;
        // 以前: 谷ストリーク用の LineRenderer 配列や角度配列を保持していたが削除

        public static void Spawn(Vector3 position, Vector3 normal, Color color, float maxRadius, float duration, int spikes = 5, float innerRatio = 0.45f, float lineWidth = 0.04f)
        {
            var go = new GameObject("ImpactStarRipple");
            var comp = go.AddComponent<ImpactStarRipple>();
            comp.color = color;
            comp.maxRadius = Mathf.Max(0.001f, maxRadius);
            comp.duration = Mathf.Max(0.05f, duration);
            comp.spikes = Mathf.Max(3, spikes);
            comp.innerRadiusRatio = Mathf.Clamp(innerRatio, 0.1f, 0.99f);
            comp.lineWidth = Mathf.Max(0.001f, lineWidth);
            comp.SetPose(position, normal);
        }

        private void Awake()
        {
            tr = transform;
            // アウトライン(LineRenderer)
            lr = GetComponent<LineRenderer>();
            if (lr == null) lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = lineWidth;
            EnsureMaterial(lr);
            lr.startColor = Color.white;
            lr.endColor = Color.white;
            lr.enabled = showOutline;

            // 塗りつぶし用メッシュ
            if (fillStar)
            {
                mf = GetComponent<MeshFilter>();
                if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
                mr = GetComponent<MeshRenderer>();
                if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
                if (mesh == null)
                {
                    mesh = new Mesh();
                    mesh.name = "ImpactStarRippleMesh";
                    mesh.MarkDynamic();
                }
                mf.sharedMesh = mesh;
                // Line と同じマテリアルを共有
                if (lr.sharedMaterial != null) mr.sharedMaterial = lr.sharedMaterial;
                EnsureMaterialForRenderer(mr);
            }

            // 谷ストリークの生成ロジックは削除
            StartCoroutine(CoPlay());
        }

        private void SetPose(Vector3 pos, Vector3 n)
        {
            tr = transform;
            normal = (n.sqrMagnitude > 0.0001f) ? n.normalized : Vector3.up;
            tr.position = pos + normal * surfaceOffset;
            tr.rotation = Quaternion.LookRotation(normal, Vector3.up); // Z+ が法線方向
        }

        private IEnumerator CoPlay()
        {
            // 1) 拡大フェーズ
            float t = 0f;
            if (holdTime <= 0f && fadeDuration <= 0f)
            {
                // 旧挙動と互換: 拡大と同時に alphaCurve に従ってフェードし、duration 終了で消滅
                while (t < duration)
                {
                    float u = (duration > 0f) ? (t / duration) : 1f;
                    float r01 = Mathf.Clamp01(radiusCurve.Evaluate(u));
                    float a01 = Mathf.Clamp01(alphaCurve.Evaluate(u));
                    float r = maxRadius * r01;
                    if (showOutline && lr != null)
                    {
                        lr.widthMultiplier = lineWidth;
                        BuildStarGeometry(r, innerRadiusRatio);
                    }
                    if (fillStar && mr != null && mf != null)
                    {
                        BuildStarMesh(r, innerRadiusRatio);
                    }
                    ApplyColor(new Color(color.r, color.g, color.b, color.a * a01));
                    t += Time.deltaTime;
                    yield return null;
                }
                Destroy(gameObject);
                yield break;
            }

            // 拡大のみ（アルファは1固定で見せ切る）
            while (t < duration)
            {
                float u = (duration > 0f) ? (t / duration) : 1f;
                float r01 = Mathf.Clamp01(radiusCurve.Evaluate(u));
                float r = maxRadius * r01;
                if (showOutline && lr != null)
                {
                    lr.widthMultiplier = lineWidth;
                    BuildStarGeometry(r, innerRadiusRatio);
                }
                if (fillStar && mr != null && mf != null)
                {
                    BuildStarMesh(r, innerRadiusRatio);
                }
                ApplyColor(new Color(color.r, color.g, color.b, color.a * 1f));
                t += Time.deltaTime;
                yield return null;
            }

            // 2) 保持フェーズ（最大半径、holdAlpha）
            float maxR = maxRadius * Mathf.Clamp01(radiusCurve.Evaluate(1f));
            float ht = 0f;
            while (ht < holdTime)
            {
                if (showOutline && lr != null)
                {
                    lr.widthMultiplier = lineWidth;
                    BuildStarGeometry(maxR, innerRadiusRatio);
                }
                if (fillStar && mr != null && mf != null)
                {
                    BuildStarMesh(maxR, innerRadiusRatio);
                }
                ApplyColor(new Color(color.r, color.g, color.b, color.a * holdAlpha));
                ht += Time.deltaTime;
                yield return null;
            }

            // 3) フェードフェーズ（最大半径のまま alphaCurve で 1→0）
            float ft = 0f;
            if (fadeDuration > 0f)
            {
                while (ft < fadeDuration)
                {
                    float v = Mathf.Clamp01(ft / fadeDuration);
                    float a01 = Mathf.Clamp01(alphaCurve.Evaluate(v));
                    if (showOutline && lr != null)
                    {
                        lr.widthMultiplier = lineWidth;
                        BuildStarGeometry(maxR, innerRadiusRatio);
                    }
                    if (fillStar && mr != null && mf != null)
                    {
                        BuildStarMesh(maxR, innerRadiusRatio);
                    }
                    ApplyColor(new Color(color.r, color.g, color.b, color.a * (holdAlpha * a01)));
                    ft += Time.deltaTime;
                    yield return null;
                }
            }
            Destroy(gameObject);
        }

        private void BuildStarGeometry(float outerR, float innerRatio)
        {
            int points = spikes * 2;
            if (points < 6) points = 6;
            lr.positionCount = points;

            // 星形をローカルXY平面で作成し、transformで回転/位置決め
            float innerR = Mathf.Max(0f, outerR * innerRatio);
            float step = Mathf.PI * 2f / points;
            for (int i = 0; i < points; i++)
            {
                float a = step * i;
                bool isOuter = (i % 2 == 0);
                float r = isOuter ? outerR : innerR;
                Vector3 local = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                lr.SetPosition(i, tr.TransformPoint(local) - tr.position + tr.position); // 明示的にワールド
            }
        }

        private void BuildStarMesh(float outerR, float innerRatio)
        {
            if (mesh == null) return;
            int rim = Mathf.Max(6, spikes * 2);
            float innerR = Mathf.Max(0f, outerR * innerRatio);
            float step = Mathf.PI * 2f / rim;

            // 頂点: 中心 + リム
            Vector3[] verts = new Vector3[rim + 1];
            verts[0] = Vector3.zero;
            for (int i = 0; i < rim; i++)
            {
                float a = step * i;
                bool isOuter = (i % 2 == 0);
                float r = isOuter ? outerR : innerR;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }

            // 三角形: 扇状
            int triCount = rim * 3;
            int[] tris = new int[triCount];
            int t = 0;
            for (int i = 0; i < rim; i++)
            {
                int i0 = 0;
                int i1 = i + 1;
                int i2 = (i + 1) % rim + 1;
                tris[t++] = i0; tris[t++] = i1; tris[t++] = i2;
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            // 法線/バウンディング
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void ApplyColor(Color c)
        {
            // マテリアルは白固定、頂点色でアルファ等を制御
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1, 1, 1, 1), 0f), new GradientColorKey(new Color(1, 1, 1, 1), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(c.a, 1f) }
            );
            lr.colorGradient = grad;

            var mat = lr.sharedMaterial;
            if (mat != null)
            {
                // HDR(>1)でベースカラーを設定してBloomに乗せる
                var hdr = new Color(c.r * bloomIntensity, c.g * bloomIntensity, c.b * bloomIntensity, 1f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdr);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdr);
            }
        }

        private void EnsureMaterial(LineRenderer target)
        {
            if (target.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) target.sharedMaterial = new Material(shader);
            }
            // 透過+加算ブレンド設定（URP Unlit向け）。Bloomでの発光を見やすくする
            var mat = target.sharedMaterial;
            if (mat != null)
            {
                // Surface Transparent
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                // Additive blend（Src One, Dst One） or Premultiplied
                mat.SetInt("_ZWrite", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)(additiveBlend ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        private void EnsureMaterialForRenderer(MeshRenderer renderer)
        {
            if (renderer == null) return;
            if (renderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) renderer.sharedMaterial = new Material(shader);
            }
            var mat = renderer.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.SetInt("_ZWrite", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)(additiveBlend ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }
    }
}
