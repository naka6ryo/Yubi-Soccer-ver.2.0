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
        // 実行は Tutorial シーンが読み込まれたときのみ行う
        if (s_hasRun)
            return;

        if (!string.Equals(scene.name, "Tutorial", System.StringComparison.OrdinalIgnoreCase))
        {
            // Tutorial 以外のシーンでは何もしない
            return;
        }

        // Mark we've started the repair attempt sequence (do it only once)
        s_hasRun = true;

        // Spawn a short-lived runner GameObject that will check for the target object
        // over several frames. Some other systems may enable or instantiate the object
        // after sceneLoaded; this runner will catch those cases.
        try
        {
            var runnerGO = new GameObject("__EnsureMission2DisabledRunner");
            Object.DontDestroyOnLoad(runnerGO);
            runnerGO.AddComponent<EnsureMission2DisabledRunner>();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"EnsureMission2Disabled: Failed to create runner: {ex}");
        }

        // We start the runner only once, so unsubscribe the sceneLoaded handler.
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Helper MonoBehaviour that searches for "Mission (2)" for a short period and disables it when found.
    // This lives in the same file to keep the runtime initializer self-contained.
    private class EnsureMission2DisabledRunner : MonoBehaviour
    {
        private System.Collections.IEnumerator Start()
        {
            const int maxFrames = 180; // check for ~3 seconds at 60fps
            bool found = false;
            for (int i = 0; i < maxFrames; i++)
            {
                try
                {
                    var go = GameObject.Find("Mission (2)");
                    if (go != null)
                    {
                        // If found, repeatedly ensure it's disabled for a short while to prevent
                        // other scripts re-enabling it immediately after scene load.
                        try { go.SetActive(false); } catch { }
                        if (!found)
                        {
                            Debug.Log($"EnsureMission2DisabledRunner: Disabled 'Mission (2)' on frame {i} after scene load.");
                            found = true;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"EnsureMission2DisabledRunner: Exception during search: {ex}");
                }
                // keep trying every frame for the duration
                yield return null;
            }
            if (!found)
            {
                Debug.Log("EnsureMission2DisabledRunner: 'Mission (2)' not found after waiting; giving up.");
            }
            else
            {
                Debug.Log("EnsureMission2DisabledRunner: Finished continuous disable attempts.");
            }
            Destroy(this.gameObject);
        }
    }
}
