using UnityEngine;
using UnityEngine.SceneManagement;

// シーン初回ロード時に GameObject 名 "Mission (2)" を確実に無効化するためのランタイム初期化スクリプト
public static class EnsureMission2Disabled
{
    private static bool s_hasRun = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        // シーンがロードされたタイミングで一度だけハンドラを実行する
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (s_hasRun)
            return;
        s_hasRun = true;

        // 指定名のオブジェクトを探して無効化する
        try
        {
            var go = GameObject.Find("Mission (2)");
            if (go != null)
            {
                go.SetActive(false);
                Debug.Log($"EnsureMission2Disabled: Disabled 'Mission (2)' on first scene load (scene={scene.name}).");
            }
            else
            {
                Debug.Log($"EnsureMission2Disabled: 'Mission (2)' not found on scene '{scene.name}'.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"EnsureMission2Disabled: Exception while disabling 'Mission (2)': {ex}");
        }

        // 処理は一度きりなのでイベント解除
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
