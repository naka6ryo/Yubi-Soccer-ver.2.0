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
        // Create a runner that will attach ActivationWatcher as early as possible and
        // continue monitoring for several seconds to catch late activations/instantiations.
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
            // First: immediate attach pass (no wait) to existing objects in scene.
            int totalAttached = 0;
            try
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
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
                                totalAttached++;
                            }
                        }
                    }
                }
                Debug.Log($"Mission2ActivationTracer: Immediately attached ActivationWatcher to {totalAttached} GameObjects in scene '{sceneName}'.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Mission2ActivationTracer: Exception while doing immediate attach: {ex}");
            }

            // Then keep monitoring for a short period: do repeated attach passes and wait a few frames
            float watchSeconds = 5f;
            float elapsed = 0f;
            while (elapsed < watchSeconds)
            {
                try
                {
                    var roots2 = SceneManager.GetActiveScene().GetRootGameObjects();
                    foreach (var root in roots2)
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
                                    totalAttached++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Mission2ActivationTracer: Exception while attaching in watch loop: {ex}");
                }
                // wait a short time before next pass
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Debug.Log($"Mission2ActivationTracer: Finished watch; total ActivationWatcher attached={totalAttached} in scene '{sceneName}'.");
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
                Debug.Log($"Mission2ActivationTracer: GameObject '{GetFullPath(gameObject)}' OnEnable detected. activeSelf={gameObject.activeSelf}. Stack:\n{trace}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Mission2ActivationTracer: Exception in OnEnable: {ex}");
            }
            // NOTE: previously we proactively disabled the GameObject here to avoid flicker during startup,
            // but that interferes with legitimate activations (e.g. when tutorials close). Do not auto-disable.
        }

        private string GetFullPath(GameObject go)
        {
            if (go == null) return "<null>";
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }
    }
}
