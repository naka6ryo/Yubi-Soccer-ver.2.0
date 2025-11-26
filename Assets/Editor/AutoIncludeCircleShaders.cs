using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to ensure circle wipe shaders are included in Graphics Settings 'Always Included Shaders'.
/// - Adds `UI/CircleMask` and `UI/CircleHole` if they are present in the project but not yet included.
/// - Provides a menu entry to force the operation and an automatic one-time attempt on script reload.
/// </summary>
public static class AutoIncludeCircleShaders
{
    private static readonly string[] ShaderNames = new[] { "UI/CircleMask", "UI/CircleHole" };
    private const string EditorPrefKey = "Yubi.IncludeCircleShaders_v1";

    [InitializeOnLoadMethod]
    private static void OnLoad()
    {
        // Attempt once automatically after scripts compile, but allow manual re-run via menu
        if (!EditorPrefs.GetBool(EditorPrefKey, false))
        {
            TryAddShadersToAlwaysIncluded(false);
        }
    }

    [MenuItem("Tools/Include Circle Shaders in Graphics Settings (force)")]
    public static void ForceIncludeShaders()
    {
        TryAddShadersToAlwaysIncluded(true);
    }

    private static void TryAddShadersToAlwaysIncluded(bool force)
    {
        // Find GraphicsSettings asset
        var settingsPath = "ProjectSettings/GraphicsSettings.asset";
        var objs = AssetDatabase.LoadAllAssetsAtPath(settingsPath);
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("AutoIncludeCircleShaders: GraphicsSettings.asset not found. Open Project Settings -> Graphics and ensure it exists.");
            return;
        }

        var so = new SerializedObject(objs[0]);
        var prop = so.FindProperty("m_AlwaysIncludedShaders");
        if (prop == null)
        {
            Debug.LogWarning("AutoIncludeCircleShaders: 'm_AlwaysIncludedShaders' property not found in GraphicsSettings.asset. Unity version may differ.");
            return;
        }

        // Gather all shader assets in project and map by name for reliable lookup
        var allShaderGuids = AssetDatabase.FindAssets("t:Shader");
        var shaderMap = new System.Collections.Generic.Dictionary<string, Shader>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var g in allShaderGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var s = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (s == null) continue;
            if (!shaderMap.ContainsKey(s.name))
            {
                shaderMap[s.name] = s;
            }
            else
            {
                // Duplicate shader name encountered; log and skip the duplicate to avoid exception
                Debug.LogWarning($"AutoIncludeCircleShaders: Duplicate shader name '{s.name}' at path '{path}' - skipping duplicate.");
            }
        }

        bool changed = false;

        foreach (var shaderName in ShaderNames)
        {
            Shader found = null;
            // Try find by exact shader name first
            shaderMap.TryGetValue(shaderName, out found);
            if (found == null)
            {
                // fallback: try case-insensitive match
                found = shaderMap.Where(kv => kv.Key.Equals(shaderName, System.StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Value).FirstOrDefault();
            }

            if (found == null)
            {
                Debug.LogWarning($"AutoIncludeCircleShaders: Shader '{shaderName}' not found in project. Skipping.");
                continue;
            }

            // Check if already present
            bool exists = false;
            for (int i = 0; i < prop.arraySize; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                if (elem != null && elem.objectReferenceValue == found)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                // append
                int idx = prop.arraySize;
                prop.InsertArrayElementAtIndex(idx);
                var newElem = prop.GetArrayElementAtIndex(idx);
                newElem.objectReferenceValue = found;
                Debug.Log($"AutoIncludeCircleShaders: Added '{shaderName}' to Always Included Shaders.");
                changed = true;
            }
            else
            {
                Debug.Log($"AutoIncludeCircleShaders: '{shaderName}' already in Always Included Shaders.");
            }
        }

        if (changed)
        {
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            EditorPrefs.SetBool(EditorPrefKey, true);
            Debug.Log("AutoIncludeCircleShaders: GraphicsSettings updated and saved.");
        }
        else
        {
            if (force)
            {
                // still mark done if user forced it
                EditorPrefs.SetBool(EditorPrefKey, true);
            }
            Debug.Log("AutoIncludeCircleShaders: No changes needed.");
        }
    }
}
