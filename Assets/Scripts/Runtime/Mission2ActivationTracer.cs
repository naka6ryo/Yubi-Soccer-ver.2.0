using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 起動直後に 'Mission (2)' が有効化されたときのスタックトレースをログに残すためのトレーサ。
/// - Scene がロードされたら該当オブジェクトを探して ActivationWatcher を追加します。
/// - ActivationWatcher は OnEnable 時にスタックトレースを出力します。
/// </summary>
public static class Mission2ActivationTracer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 少し遅らせて探す（他の Awake/Start が先に実行される）
        var runner = new GameObject("_Mission2ActivationTracerRunner");
        runner.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(runner);
        var helper = runner.AddComponent<TracerRunner>();
        helper.StartCoroutine(helper.FindAndAttach(scene.name));
    }

    private class TracerRunner : MonoBehaviour
    {
        public IEnumerator FindAndAttach(string sceneName)
        {
            // Wait a few frames to catch activations happening during Start
            for (int i = 0; i < 4; i++) yield return null;

            try
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                int attached = 0;
                foreach (var root in roots)
                {
                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (var t in transforms)
                    {
                        if (t == null) continue;
                        var go = t.gameObject;
                        if (go == null) continue;
                        if (IsTargetName(go.name))
                        {
                            if (go.GetComponent<ActivationWatcher>() == null)
                            {
                                go.AddComponent<ActivationWatcher>();
                                attached++;
                            }
                        }
                    }
                }
                Debug.Log($"Mission2ActivationTracer: Attached ActivationWatcher to {attached} GameObjects in scene '{sceneName}'.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Mission2ActivationTracer: Exception while attaching: {ex}");
            }

            // keep runner around for a short while to catch late creations
            yield return new WaitForSecondsRealtime(5f);
            Destroy(gameObject);
        }

        private static bool IsTargetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var lower = name.ToLowerInvariant();
            return lower.Contains("mission") && (lower.Contains("(2)") || lower.Contains(" 2") || lower.EndsWith("2") || lower.Contains("mission2"));
        }
    }

    private class ActivationWatcher : MonoBehaviour
    {
        private void OnEnable()
        {
            try
            {
                var trace = Environment.StackTrace;
                Debug.Log($"Mission2ActivationTracer: GameObject '{gameObject.name}' OnEnable detected. Stack:\n{trace}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Mission2ActivationTracer: Exception in OnEnable: {ex}");
            }
            // Also try to proactively disable to avoid visible flicker
            try { if (gameObject.activeSelf) gameObject.SetActive(false); } catch { }
        }
    }
}
