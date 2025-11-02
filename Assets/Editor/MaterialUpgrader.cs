using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Built-in Render PipelineのマテリアルをURPに一括変換するエディタースクリプト
/// </summary>
public class MaterialUpgrader : EditorWindow
{
    [MenuItem("Tools/Upgrade Materials to URP")]
    public static void ShowWindow()
    {
        GetWindow<MaterialUpgrader>("Material Upgrader");
    }

    private void OnGUI()
    {
        GUILayout.Label("Material Upgrader to URP", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Upgrade All Materials in Project", GUILayout.Height(40)))
        {
            UpgradeAllMaterials();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "このツールはプロジェクト内のすべてのマテリアルをURPのLitシェーダーに変換します。\n" +
            "実行前にプロジェクトのバックアップを推奨します。",
            MessageType.Warning
        );
    }

    private static void UpgradeAllMaterials()
    {
        if (!EditorUtility.DisplayDialog(
            "マテリアルをアップグレード",
            "プロジェクト内のすべてのマテリアルをURPに変換します。この操作は元に戻せません。続行しますか？",
            "続行",
            "キャンセル"))
        {
            return;
        }

        // プロジェクト内のすべてのマテリアルを取得
        string[] guids = AssetDatabase.FindAssets("t:Material");
        int convertedCount = 0;
        int totalCount = guids.Length;

        for (int i = 0; i < totalCount; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null) continue;

            // プログレスバー表示
            EditorUtility.DisplayProgressBar(
                "マテリアル変換中",
                $"変換中: {material.name} ({i + 1}/{totalCount})",
                (float)i / totalCount
            );

            // Built-inシェーダーをURPに変換
            if (NeedsUpgrade(material))
            {
                UpgradeMaterial(material);
                convertedCount++;
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "変換完了",
            $"{convertedCount} 個のマテリアルをURPに変換しました。",
            "OK"
        );

        Debug.Log($"[MaterialUpgrader] {convertedCount}/{totalCount} materials upgraded to URP.");
    }

    private static bool NeedsUpgrade(Material material)
    {
        if (material.shader == null) return false;

        string shaderName = material.shader.name;

        // Built-inシェーダーかどうかをチェック
        return shaderName.StartsWith("Standard") ||
               shaderName.StartsWith("Legacy Shaders/") ||
               shaderName.StartsWith("Mobile/") ||
               shaderName.Contains("Diffuse") ||
               shaderName.Contains("Specular") ||
               shaderName.Contains("Bumped") ||
               shaderName == "Hidden/InternalErrorShader";
    }

    private static void UpgradeMaterial(Material material)
    {
        // 元のプロパティを保存
        Texture mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        Texture bumpMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
        Texture metallicMap = material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null;
        float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
        float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;

        // URPのLitシェーダーに変更
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader != null)
        {
            material.shader = urpShader;

            // プロパティを再設定
            if (mainTex != null) material.SetTexture("_BaseMap", mainTex);
            material.SetColor("_BaseColor", color);
            if (bumpMap != null) material.SetTexture("_BumpMap", bumpMap);
            if (metallicMap != null) material.SetTexture("_MetallicGlossMap", metallicMap);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);

            EditorUtility.SetDirty(material);
            Debug.Log($"[MaterialUpgrader] Upgraded: {material.name}");
        }
        else
        {
            Debug.LogError("[MaterialUpgrader] URP Lit shader not found! Is URP installed?");
        }
    }
}
