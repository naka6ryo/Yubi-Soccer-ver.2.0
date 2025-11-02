using UnityEditor;
using UnityEngine;

/// <summary>
/// シーン内のオブジェクトにGlobal Illumination設定を適用するエディタースクリプト
/// 発光の照り付けを受けられるようにする
/// </summary>
public class GlobalIlluminationSetup : EditorWindow
{
    [MenuItem("Tools/Setup Global Illumination (Receive Light)")]
    public static void ShowWindow()
    {
        GetWindow<GlobalIlluminationSetup>("GI Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Global Illumination 設定", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "このツールはシーン内のすべてのオブジェクトに対して：\n" +
            "1. Static フラグを設定（Lightmap Static）\n" +
            "2. マテリアルのGI設定を有効化\n" +
            "3. ライトマップの設定を最適化\n" +
            "して、発光の照り付けを受けられるようにします。",
            MessageType.Info
        );

        GUILayout.Space(10);

        if (GUILayout.Button("すべてのオブジェクトにGI設定を適用", GUILayout.Height(40)))
        {
            SetupGlobalIllumination();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("選択オブジェクトにGI設定を適用", GUILayout.Height(30)))
        {
            SetupSelectedObjects();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("ライトマップを生成", GUILayout.Height(40)))
        {
            GenerateLightmaps();
        }

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "注意: Staticフラグの設定は動的オブジェクト（プレイヤーなど）には適用されません。\n" +
            "ライトマップ生成には時間がかかる場合があります。",
            MessageType.Warning
        );
    }

    private void SetupGlobalIllumination()
    {
        if (!EditorUtility.DisplayDialog(
            "GI設定を適用",
            "シーン内のすべてのオブジェクトにGlobal Illumination設定を適用します。続行しますか？",
            "続行",
            "キャンセル"))
        {
            return;
        }

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int processedCount = 0;
        int totalCount = allObjects.Length;

        for (int i = 0; i < totalCount; i++)
        {
            GameObject obj = allObjects[i];

            EditorUtility.DisplayProgressBar(
                "GI設定を適用中",
                $"処理中: {obj.name} ({i + 1}/{totalCount})",
                (float)i / totalCount
            );

            if (SetupObjectForGI(obj))
            {
                processedCount++;
            }
        }

        EditorUtility.ClearProgressBar();

        EditorUtility.DisplayDialog(
            "設定完了",
            $"{processedCount} 個のオブジェクトにGI設定を適用しました。\n\n" +
            "次のステップ:\n" +
            "1. 「ライトマップを生成」ボタンをクリック\n" +
            "2. または Window → Rendering → Lighting から手動で生成",
            "OK"
        );

        Debug.Log($"[GlobalIlluminationSetup] {processedCount}/{totalCount} objects configured for GI.");
    }

    private void SetupSelectedObjects()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("エラー", "オブジェクトを選択してください。", "OK");
            return;
        }

        int processedCount = 0;

        foreach (GameObject obj in Selection.gameObjects)
        {
            if (SetupObjectForGI(obj))
            {
                processedCount++;
            }
        }

        EditorUtility.DisplayDialog(
            "設定完了",
            $"{processedCount} 個のオブジェクトにGI設定を適用しました。",
            "OK"
        );

        Debug.Log($"[GlobalIlluminationSetup] {processedCount} selected objects configured for GI.");
    }

    private bool SetupObjectForGI(GameObject obj)
    {
        bool modified = false;

        // 1. Static フラグを設定（動的オブジェクトを除く）
        if (!obj.CompareTag("Player") && obj.GetComponent<Rigidbody>() == null)
        {
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.ContributeGI);
            modified = true;
        }

        // 2. Rendererがあればマテリアルの設定を調整
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // ReceiveGI を Lightmaps に設定
            SerializedObject so = new SerializedObject(renderer);
            SerializedProperty receiveGI = so.FindProperty("m_ReceiveGI");
            if (receiveGI != null)
            {
                receiveGI.intValue = 1; // 1 = Lightmaps
                so.ApplyModifiedProperties();
            }

            // マテリアルのGI設定
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;

                // アセットファイルかどうかをチェック
                string assetPath = AssetDatabase.GetAssetPath(mat);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // BakedなEmissionの場合はRealtimeに変更
                    if (mat.globalIlluminationFlags == MaterialGlobalIlluminationFlags.BakedEmissive)
                    {
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }

                    EditorUtility.SetDirty(mat);
                }
            }

            modified = true;
        }

        // GameObjectのSetDirtyは削除（シーン内オブジェクトには不要）

        return modified;
    }

    private void GenerateLightmaps()
    {
        if (!EditorUtility.DisplayDialog(
            "ライトマップを生成",
            "ライトマップの生成には時間がかかる場合があります。\n続行しますか？",
            "生成",
            "キャンセル"))
        {
            return;
        }

        // Lightingウィンドウを開く
        EditorWindow.GetWindow(System.Type.GetType("UnityEditor.LightingWindow,UnityEditor"));

        EditorUtility.DisplayDialog(
            "ライトマップ生成",
            "Lightingウィンドウが開きました。\n\n" +
            "以下の手順で設定してください：\n" +
            "1. 「Mixed Lighting」セクションで「Baked Global Illumination」にチェック\n" +
            "2. 下部の「Generate Lighting」ボタンをクリック",
            "OK"
        );

        Debug.Log("[GlobalIlluminationSetup] Lightmap generation window opened.");
    }
}
