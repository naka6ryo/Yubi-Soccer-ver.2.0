using UnityEditor;
using UnityEngine;

/// <summary>
/// スタジアムの照明を発光マテリアルに変換するエディタースクリプト
/// </summary>
public class EmissiveLightConverter : EditorWindow
{
    private Color emissionColor = Color.white;
    private float emissionIntensity = 2.0f;
    private string searchKeyword = "light";

    [MenuItem("Tools/Convert Lights to Emissive")]
    public static void ShowWindow()
    {
        GetWindow<EmissiveLightConverter>("Emissive Light Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("照明を発光マテリアルに変換", EditorStyles.boldLabel);
        GUILayout.Space(10);

        searchKeyword = EditorGUILayout.TextField("検索キーワード:", searchKeyword);
        EditorGUILayout.HelpBox(
            "オブジェクト名にこのキーワードを含むものを検索します。\n" +
            "例: 'light', 'lamp', 'bulb', 'spotlight' など",
            MessageType.Info
        );

        GUILayout.Space(10);

        emissionColor = EditorGUILayout.ColorField("発光色:", emissionColor);
        emissionIntensity = EditorGUILayout.Slider("発光強度:", emissionIntensity, 0.1f, 10f);

        GUILayout.Space(10);

        if (GUILayout.Button("シーン内の照明を発光に変換", GUILayout.Height(40)))
        {
            ConvertLightsToEmissive();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("選択オブジェクトを発光に変換", GUILayout.Height(30)))
        {
            ConvertSelectedToEmissive();
        }

        GUILayout.Space(5);

        EditorGUI.BeginDisabledGroup(true);
        if (GUILayout.Button("選択オブジェクトの発光を削除", GUILayout.Height(30)))
        {
            RemoveEmissionFromSelected();
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "発光を削除したい場合は、下のボタンを使用してください。",
            MessageType.Info
        );
    }

    [MenuItem("Tools/Remove Emission from Selected")]
    public static void RemoveEmissionFromSelected()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("エラー", "オブジェクトを選択してください。", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "発光を削除",
            $"{Selection.gameObjects.Length} 個のオブジェクトから発光を削除します。よろしいですか？",
            "削除",
            "キャンセル"))
        {
            return;
        }

        int removedCount = 0;

        foreach (GameObject obj in Selection.gameObjects)
        {
            if (RemoveEmission(obj))
            {
                removedCount++;
            }
        }

        EditorUtility.DisplayDialog(
            "削除完了",
            $"{removedCount} 個のオブジェクトから発光を削除しました。",
            "OK"
        );

        Debug.Log($"[EmissiveLightConverter] Removed emission from {removedCount} objects.");
    }

    private void ConvertLightsToEmissive()
    {
        if (string.IsNullOrEmpty(searchKeyword))
        {
            EditorUtility.DisplayDialog("エラー", "検索キーワードを入力してください。", "OK");
            return;
        }

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int convertedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains(searchKeyword.ToLower()))
            {
                if (MakeEmissive(obj))
                {
                    convertedCount++;
                }
            }
        }

        EditorUtility.DisplayDialog(
            "変換完了",
            $"{convertedCount} 個のオブジェクトを発光に変換しました。",
            "OK"
        );

        Debug.Log($"[EmissiveLightConverter] {convertedCount} objects converted to emissive.");
    }

    private void ConvertSelectedToEmissive()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("エラー", "オブジェクトを選択してください。", "OK");
            return;
        }

        int convertedCount = 0;

        foreach (GameObject obj in Selection.gameObjects)
        {
            if (MakeEmissive(obj))
            {
                convertedCount++;
            }
        }

        EditorUtility.DisplayDialog(
            "変換完了",
            $"{convertedCount} 個のオブジェクトを発光に変換しました。",
            "OK"
        );

        Debug.Log($"[EmissiveLightConverter] {convertedCount} selected objects converted to emissive.");
    }

    private bool MakeEmissive(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return false;

        bool anyConverted = false;

        foreach (Material mat in renderer.sharedMaterials)
        {
            if (mat == null) continue;

            // アセットファイルかどうかをチェック
            string assetPath = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"[EmissiveLightConverter] Skipping runtime material: {mat.name}");
                continue;
            }

            // URPのLitシェーダーに変更（まだの場合）
            if (mat.shader.name != "Universal Render Pipeline/Lit")
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader != null)
                {
                    mat.shader = urpShader;
                }
            }

            // Emissionを有効化
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            // 発光色と強度を設定
            Color finalEmission = emissionColor * emissionIntensity;
            mat.SetColor("_EmissionColor", finalEmission);

            // Base Colorも明るくする（オプション）
            if (!mat.HasProperty("_BaseColor") || mat.GetColor("_BaseColor") == Color.black)
            {
                mat.SetColor("_BaseColor", emissionColor);
            }

            EditorUtility.SetDirty(mat);
            anyConverted = true;

            Debug.Log($"[EmissiveLightConverter] Made emissive: {obj.name} - {mat.name}");
        }

        return anyConverted;
    }

    private static bool RemoveEmission(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return false;

        bool anyRemoved = false;

        foreach (Material mat in renderer.sharedMaterials)
        {
            if (mat == null) continue;

            // アセットファイルかどうかをチェック
            string assetPath = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"[EmissiveLightConverter] Skipping runtime material: {mat.name}");
                continue;
            }

            // Emissionを無効化
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            EditorUtility.SetDirty(mat);
            anyRemoved = true;

            Debug.Log($"[EmissiveLightConverter] Removed emission: {obj.name} - {mat.name}");
        }

        return anyRemoved;
    }
}
