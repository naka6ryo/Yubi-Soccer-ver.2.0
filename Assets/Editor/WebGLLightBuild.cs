using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGLLightBuild
{
    private const string DefaultOutputPath = "Builds/Light-WebGL";

    [MenuItem("Build/Build WebGL (Light)")]
    public static void BuildWebGLLight()
    {
        // 出力先フォルダ選択（キャンセルされたら終了）
        var output = EditorUtility.SaveFolderPanel("Select WebGL Light Build Output Folder", DefaultOutputPath, "");
        if (string.IsNullOrEmpty(output))
        {
            Debug.Log("[WebGLLightBuild] Canceled.");
            return;
        }

        // 現在の設定を退避
        var prevTarget = EditorUserBuildSettings.activeBuildTarget;
        var prevGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        var prevCompression = PlayerSettings.WebGL.compressionFormat;
        var prevDecompressionFallback = PlayerSettings.WebGL.decompressionFallback;
        var prevDataCaching = PlayerSettings.WebGL.dataCaching;
        int prevMemorySize = PlayerSettings.WebGL.memorySize;

        try
        {
            // WebGL に切り替え
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            // 軽量設定
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // 解凍負荷を減らす
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true; // 2 回目以降を速く
            PlayerSettings.WebGL.memorySize = 256;   // 必要なら後で調整

            // シーン一覧（Build Settings で有効なもの）
            var scenes = EditorBuildSettings.scenes;
            string[] scenePaths = new string[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                scenePaths[i] = scenes[i].path;
            }

            // ビルド実行
            var options = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGLLightBuild] Build succeeded: {report.summary.totalSize / (1024f * 1024f):F1} MB");
            }
            else
            {
                Debug.LogWarning($"[WebGLLightBuild] Build failed: {report.summary.result}");
            }
        }
        finally
        {
            // 設定を元に戻す
            PlayerSettings.WebGL.compressionFormat = prevCompression;
            PlayerSettings.WebGL.decompressionFallback = prevDecompressionFallback;
            PlayerSettings.WebGL.dataCaching = prevDataCaching;
            PlayerSettings.WebGL.memorySize = prevMemorySize;
            EditorUserBuildSettings.SwitchActiveBuildTarget(prevGroup, prevTarget);
        }
    }
}
