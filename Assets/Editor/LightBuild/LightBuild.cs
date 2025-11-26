using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Lightweight build helper
// - Moves large, non-essential top-level folders under `Assets` to `Assets/_LightBuildExcluded` before building
// - Sets quality to lowest and disables development build options
// - Builds the player using current build target and scenes in Build Settings
// - Restores moved folders after build (even on failure)

public static class LightBuild
{
    [MenuItem("Build/Build Lightweight")]
    public static void BuildLightweight()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Stop play mode before running lightweight build.");
            return;
        }

        // Define project paths
        string projectPath = Directory.GetCurrentDirectory();
        string assetsPath = Path.Combine(projectPath, "Assets");
        string excludeRoot = Path.Combine(assetsPath, "_LightBuildExcluded");

        // Discover top-level asset folders (inside Assets)
        var topDirs = Directory.GetDirectories(assetsPath).Select(p => Path.GetFileName(p)).ToList();

        // Allowed essential directories that we won't move
        var allowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Scripts", "Scenes", "Plugins", "Editor", "Editor Default Resources", "Resources", "Shaders", "StreamingAssets"
        };

        // Candidate list: top-level directories whose total size > threshold and not in allowlist
        long sizeThresholdBytes = 10 * 1024 * 1024; // 10 MB
        var candidates = new List<string>();
        foreach (var dirName in topDirs)
        {
            if (allowlist.Contains(dirName)) continue;
            try
            {
                string full = Path.Combine(assetsPath, dirName);
                long bytes = GetDirectorySize(full);
                if (bytes >= sizeThresholdBytes)
                {
                    candidates.Add(dirName);
                    Debug.Log($"LightBuild: Candidate to exclude: {dirName} (~{bytes / 1024 / 1024} MB)");
                }
            }
            catch { }
        }

        // Always exclude Videos and StreamingAssets if present (they tend to be large)
        foreach (var must in new[] { "Videos", "Sounds", "Audio", "StreamingAssets" })
        {
            if (topDirs.Contains(must, StringComparer.OrdinalIgnoreCase) && !candidates.Contains(must, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(must);
            }
        }

        // Confirm with user which folders will be excluded
        if (candidates.Count == 0)
        {
            if (!EditorUtility.DisplayDialog("Build Lightweight", "No large non-essential folders detected to exclude. Proceed with a lightweight build anyway?", "Yes", "Cancel"))
                return;
        }
        else
        {
            string list = string.Join("\n", candidates);
            if (!EditorUtility.DisplayDialog("Build Lightweight", "The following top-level folders will be temporarily excluded from the build:\n\n" + list + "\n\nContinue?", "OK", "Cancel"))
                return;
        }

        // Prepare exclude folder inside Assets (so AssetDatabase can move assets safely)
        if (!Directory.Exists(excludeRoot)) Directory.CreateDirectory(excludeRoot);

        var moved = new List<Tuple<string, string>>(); // (srcRel, dstRel)

        try
        {
            AssetDatabase.StartAssetEditing();

            // Move candidate folders into Assets/_LightBuildExcluded
            foreach (var name in candidates)
            {
                string src = Path.Combine("Assets", name).Replace("\\", "/");
                string dst = Path.Combine("Assets", "_LightBuildExcluded", name).Replace("\\", "/");
                if (!AssetDatabase.IsValidFolder(src)) continue;
                // ensure parent exists
                string parent = Path.GetDirectoryName(dst).Replace("\\", "/");
                if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", "_LightBuildExcluded");
                string err = AssetDatabase.MoveAsset(src, dst);
                if (!string.IsNullOrEmpty(err))
                {
                    Debug.LogWarning($"LightBuild: Failed to move {src} -> {dst}: {err}");
                }
                else
                {
                    moved.Add(Tuple.Create(src, dst));
                    Debug.Log($"LightBuild: Moved {src} -> {dst}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        // Save current settings to restore later
        int prevQuality = QualitySettings.GetQualityLevel();
        bool prevDevelopment = EditorUserBuildSettings.development;
        bool prevAutoConnectProfiler = EditorUserBuildSettings.connectProfiler;

        try
        {
            // Apply lightweight settings
            QualitySettings.SetQualityLevel(0, true);
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.connectProfiler = false;

            // Build
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("LightBuild: No scenes in Build Settings. Aborting.");
                return;
            }

            string buildPath = GetDefaultBuildPath(EditorUserBuildSettings.activeBuildTarget);
            Debug.Log($"LightBuild: Building to {buildPath} with {scenes.Length} scenes...");

            // Ensure output directory exists
            var outDir = Path.GetDirectoryName(buildPath);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            var report = BuildPipeline.BuildPlayer(scenes, buildPath, EditorUserBuildSettings.activeBuildTarget, BuildOptions.None);
            Debug.Log("LightBuild: Build finished.");
        }
        catch (Exception ex)
        {
            Debug.LogError("LightBuild: Build failed: " + ex);
        }
        finally
        {
            // Restore settings
            QualitySettings.SetQualityLevel(prevQuality, true);
            EditorUserBuildSettings.development = prevDevelopment;
            EditorUserBuildSettings.connectProfiler = prevAutoConnectProfiler;

            // Restore moved assets
            if (moved.Count > 0)
            {
                AssetDatabase.StartAssetEditing();
                foreach (var tup in moved)
                {
                    string src = tup.Item2; // moved destination
                    string dst = tup.Item1; // original
                    try
                    {
                        string err = AssetDatabase.MoveAsset(src, dst);
                        if (!string.IsNullOrEmpty(err))
                        {
                            Debug.LogWarning($"LightBuild: Failed to restore {src} -> {dst}: {err}");
                        }
                        else
                        {
                            Debug.Log($"LightBuild: Restored {src} -> {dst}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"LightBuild: Exception while restoring {src} -> {dst}: {e}");
                    }
                }
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            EditorUtility.RevealInFinder(Path.Combine(Directory.GetCurrentDirectory(), "Builds"));
        }
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        long size = 0;
        try
        {
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                try { size += new FileInfo(f).Length; } catch { }
            }
        }
        catch { }
        return size;
    }

    private static string GetDefaultBuildPath(BuildTarget target)
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Light");
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return Path.Combine(root, "Windows", PlayerSettings.productName + ".exe").Replace("\\", "/");
            case BuildTarget.StandaloneOSX:
                return Path.Combine(root, "OSX", PlayerSettings.productName + ".app").Replace("\\", "/");
            case BuildTarget.WebGL:
                return Path.Combine(root, "WebGL").Replace("\\", "/");
            case BuildTarget.Android:
                return Path.Combine(root, "Android", PlayerSettings.productName + ".apk").Replace("\\", "/");
            default:
                return Path.Combine(root, PlayerSettings.productName + "_Build").Replace("\\", "/");
        }
    }
}
