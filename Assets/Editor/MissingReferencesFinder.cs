using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// シーン内のMissing ScriptやNull参照を検出して修正するエディタースクリプト
/// </summary>
public class MissingReferencesFinder : EditorWindow
{
    private Vector2 scrollPos;
    private List<GameObject> objectsWithMissingScripts = new List<GameObject>();
    private List<GameObject> objectsWithNullReferences = new List<GameObject>();

    [MenuItem("Tools/Find and Fix Missing References")]
    public static void ShowWindow()
    {
        GetWindow<MissingReferencesFinder>("Missing References Finder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Missing References 検出ツール", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("シーンをスキャン", GUILayout.Height(40)))
        {
            ScanScene();
        }

        GUILayout.Space(10);

        if (objectsWithMissingScripts.Count > 0 || objectsWithNullReferences.Count > 0)
        {
            if (GUILayout.Button("Missing Scriptを削除", GUILayout.Height(30)))
            {
                RemoveMissingScripts();
            }

            GUILayout.Space(10);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            if (objectsWithMissingScripts.Count > 0)
            {
                EditorGUILayout.LabelField($"Missing Scripts: {objectsWithMissingScripts.Count}", EditorStyles.boldLabel);
                foreach (GameObject obj in objectsWithMissingScripts)
                {
                    if (obj != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            Selection.activeGameObject = obj;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                GUILayout.Space(10);
            }

            if (objectsWithNullReferences.Count > 0)
            {
                EditorGUILayout.LabelField($"Null References: {objectsWithNullReferences.Count}", EditorStyles.boldLabel);
                foreach (GameObject obj in objectsWithNullReferences)
                {
                    if (obj != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            Selection.activeGameObject = obj;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
        else if (objectsWithMissingScripts.Count == 0 && objectsWithNullReferences.Count == 0)
        {
            EditorGUILayout.HelpBox("スキャンを実行してください。", MessageType.Info);
        }
    }

    private void ScanScene()
    {
        objectsWithMissingScripts.Clear();
        objectsWithNullReferences.Clear();

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int scannedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            scannedCount++;

            // Missing Scriptをチェック
            Component[] components = obj.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null)
                {
                    if (!objectsWithMissingScripts.Contains(obj))
                    {
                        objectsWithMissingScripts.Add(obj);
                        Debug.LogWarning($"[MissingReferencesFinder] Missing script on: {GetGameObjectPath(obj)}", obj);
                    }
                }
            }

            // Null参照をチェック
            if (HasNullReferences(obj))
            {
                if (!objectsWithNullReferences.Contains(obj))
                {
                    objectsWithNullReferences.Add(obj);
                }
            }
        }

        Debug.Log($"[MissingReferencesFinder] Scanned {scannedCount} objects. Found {objectsWithMissingScripts.Count} with missing scripts, {objectsWithNullReferences.Count} with null references.");

        EditorUtility.DisplayDialog(
            "スキャン完了",
            $"スキャン完了:\n" +
            $"- チェックしたオブジェクト: {scannedCount}\n" +
            $"- Missing Scripts: {objectsWithMissingScripts.Count}\n" +
            $"- Null References: {objectsWithNullReferences.Count}",
            "OK"
        );

        Repaint();
    }

    private bool HasNullReferences(GameObject obj)
    {
        Component[] components = obj.GetComponents<Component>();
        foreach (Component comp in components)
        {
            if (comp == null) continue;

            SerializedObject so = new SerializedObject(comp);
            SerializedProperty prop = so.GetIterator();

            while (prop.NextVisible(true))
            {
                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
                    {
                        Debug.LogWarning($"[MissingReferencesFinder] Null reference in {comp.GetType().Name}.{prop.name} on: {GetGameObjectPath(obj)}", obj);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private void RemoveMissingScripts()
    {
        if (!EditorUtility.DisplayDialog(
            "Missing Scriptを削除",
            $"{objectsWithMissingScripts.Count} 個のオブジェクトからMissing Scriptを削除します。よろしいですか？",
            "削除",
            "キャンセル"))
        {
            return;
        }

        int removedCount = 0;

        foreach (GameObject obj in objectsWithMissingScripts)
        {
            if (obj == null) continue;

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
            removedCount++;
        }

        objectsWithMissingScripts.Clear();

        EditorUtility.DisplayDialog(
            "削除完了",
            $"{removedCount} 個のオブジェクトからMissing Scriptを削除しました。",
            "OK"
        );

        Debug.Log($"[MissingReferencesFinder] Removed missing scripts from {removedCount} objects.");
        Repaint();
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
