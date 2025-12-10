using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif

/// <summary>
/// WebGL最適化ツール
/// スマホでの動作を軽くするための自動設定
/// 
/// 使い方:
/// 1. Unity Editor で Tools → Optimize for Mobile WebGL を実行
/// 2. 確認ダイアログが表示されるので OK を押す
/// 3. 自動的に最適な設定に変更されます
/// </summary>
public class WebGLOptimizer
{
#if UNITY_EDITOR
    [MenuItem("Tools/Optimize for Mobile WebGL")]
    public static void OptimizeForMobileWebGL()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "WebGL最適化",
            "スマホ向けにWebGLビルドを最適化します。\n\n" +
            "以下の設定を変更します:\n" +
            "- Quality Settings を軽量化\n" +
            "- Player Settings を最適化\n" +
            "- カメラ設定を軽量化\n" +
            "- 物理演算を最適化\n\n" +
            "続行しますか？",
            "はい",
            "いいえ"
        );

        if (!proceed) return;

        OptimizeQualitySettings();
        OptimizePlayerSettings();
        OptimizeCameras();
        OptimizePhysics();

        EditorUtility.DisplayDialog(
            "最適化完了",
            "WebGLの最適化が完了しました！\n\n" +
            "次のステップ:\n" +
            "1. スタジアムのライトマップを削除\n" +
            "2. テクスチャサイズを256に圧縮\n" +
            "3. ビルドしてテスト\n\n" +
            "詳細は OPTIMIZATION_GUIDE.md を参照してください。",
            "OK"
        );

        Debug.Log("[WebGLOptimizer] 最適化完了！");
    }

    static void OptimizeQualitySettings()
    {
        Debug.Log("[WebGLOptimizer] Quality Settings を最適化中...");

        // WebGL用の品質設定を取得（通常は Mobile）
        string[] qualityNames = QualitySettings.names;
        int webglQualityIndex = -1;

        // Mobile 品質を探す
        for (int i = 0; i < qualityNames.Length; i++)
        {
            if (qualityNames[i].Contains("Mobile") || qualityNames[i].Contains("Low"))
            {
                webglQualityIndex = i;
                break;
            }
        }

        if (webglQualityIndex >= 0)
        {
            QualitySettings.SetQualityLevel(webglQualityIndex, true);
        }

        // 軽量化設定
        QualitySettings.pixelLightCount = 1;
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.shadowDistance = 20f;
        QualitySettings.shadowCascades = 1;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.antiAliasing = 0;
        QualitySettings.softParticles = false;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.vSyncCount = 0;
        QualitySettings.globalTextureMipmapLimit = 1; // Half resolution

        Debug.Log("[WebGLOptimizer] Quality Settings 最適化完了");
    }

    static void OptimizePlayerSettings()
    {
        Debug.Log("[WebGLOptimizer] Player Settings を最適化中...");

        // WebGL固有の設定
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.decompressionFallback = false; // wasmStreaming の代替

        // 一般設定
        PlayerSettings.colorSpace = ColorSpace.Gamma; // Linear より軽い
        PlayerSettings.defaultWebScreenWidth = 800;
        PlayerSettings.defaultWebScreenHeight = 600;
        PlayerSettings.runInBackground = false;
        PlayerSettings.MTRendering = true;

        // コード最適化（Unity 6000 対応）
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
        
        // IL2CPP最適化
        PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);

        Debug.Log("[WebGLOptimizer] Player Settings 最適化完了");
    }

    static void OptimizeCameras()
    {
        Debug.Log("[WebGLOptimizer] カメラ設定を最適化中...");

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera cam in cameras)
        {
            cam.farClipPlane = Mathf.Min(cam.farClipPlane, 100f);
            cam.allowMSAA = false;
            cam.allowHDR = false;
            
            Debug.Log($"[WebGLOptimizer] カメラ最適化: {cam.name}");
        }

        Debug.Log($"[WebGLOptimizer] {cameras.Length} 個のカメラを最適化完了");
    }

    static void OptimizePhysics()
    {
        Debug.Log("[WebGLOptimizer] 物理演算設定を最適化中...");

        Physics.defaultSolverIterations = 4; // デフォルト6 → 4
        Physics.defaultSolverVelocityIterations = 1; // デフォルト1（そのまま）
        Physics.sleepThreshold = 0.01f; // より早くスリープ
        Physics.autoSyncTransforms = false; // パフォーマンス向上

        // Time settings
        Time.fixedDeltaTime = 0.03f; // 0.02 → 0.03 (30fps物理演算)

        Debug.Log("[WebGLOptimizer] 物理演算設定最適化完了");
    }

    [MenuItem("Tools/Compress All Textures to 256")]
    public static void CompressAllTextures()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "テクスチャ圧縮",
            "すべてのテクスチャを256pxに圧縮します。\n" +
            "（元に戻すことはできません）\n\n" +
            "続行しますか？",
            "はい",
            "いいえ"
        );

        if (!proceed) return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int count = 0;

        EditorUtility.DisplayProgressBar("テクスチャ圧縮中", "処理中...", 0f);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.compressionQuality = 50; // High Quality
                importer.SaveAndReimport();
                count++;
            }

            EditorUtility.DisplayProgressBar("テクスチャ圧縮中", $"{i + 1}/{guids.Length}", (float)(i + 1) / guids.Length);
        }

        EditorUtility.ClearProgressBar();

        EditorUtility.DisplayDialog(
            "圧縮完了",
            $"{count} 個のテクスチャを256pxに圧縮しました。",
            "OK"
        );

        Debug.Log($"[WebGLOptimizer] {count} 個のテクスチャを圧縮完了");
    }

    [MenuItem("Tools/Clear Baked Lighting Data")]
    public static void ClearBakedLighting()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "ライトマップ削除",
            "ベイク済みライティングデータを削除します。\n" +
            "（大幅に軽量化されますが、見た目が変わります）\n\n" +
            "続行しますか？",
            "はい",
            "いいえ"
        );

        if (!proceed) return;

        Lightmapping.Clear();
        Lightmapping.ClearLightingDataAsset();

        EditorUtility.DisplayDialog(
            "削除完了",
            "ライトマップデータを削除しました。\n" +
            "シーンを保存してください。",
            "OK"
        );

        Debug.Log("[WebGLOptimizer] ライトマップ削除完了");
    }
#endif
}
