using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// When `FinalGoalUIController.hideMissionsOnNextLoad` is set, this runner will hide MissionUIController
/// initial displays after the next scene load. It waits a couple frames to allow scene objects to initialize,
/// then calls `ResetCompleted()` on all `MissionUIController` instances and clears the flag.
/// </summary>
public static class ReloadMissionHider
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If the flag isn't set, nothing to do
        try
        {
            if (!YubiSoccer.UI.FinalGoalUIController.hideMissionsOnNextLoad) return;
        }
        catch { return; }

        // Create a transient runner to perform delayed actions after scene load
        var runner = new GameObject("_ReloadMissionHiderRunner");
        runner.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(runner);
        var comp = runner.AddComponent<RunnerBehaviour>();
        comp.StartCoroutine(comp.DoHideAfterFrames());
    }

    private class RunnerBehaviour : MonoBehaviour
    {
        public IEnumerator DoHideAfterFrames()
        {
            // Wait a couple frames to allow Awake/Start to complete
            yield return null;
            yield return null;
            int count = 0;
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<YubiSoccer.UI.MissionUIController>(true);
                foreach (var m in all)
                {
                    try
                    {
                        m.ResetCompleted();
                        count++;
                    }
                    catch { }
                }
                Debug.Log($"ReloadMissionHider: ResetCompleted called on {count} MissionUIController(s) after scene '{SceneManager.GetActiveScene().name}' loaded.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ReloadMissionHider: Exception while hiding missions: " + ex);
            }

            // Additionally, perform several end-of-frame immediate-disable attempts
            // to catch any mission GameObjects that are (re)activated after ResetCompleted.
            for (int i = 0; i < 8; i++)
            {
                try { TryDisableMatchesInActiveScene(); } catch { }
                yield return new WaitForEndOfFrame();
            }

            // clear the request flag so it only applies once
            try { YubiSoccer.UI.FinalGoalUIController.hideMissionsOnNextLoad = false; } catch { }
            Destroy(gameObject);
        }

        private void TryDisableMatchesInActiveScene()
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (var t in transforms)
                    {
                        var go = t.gameObject;
                        if (go == null) continue;
                        if (IsTargetMission2(go.name))
                        {
                            if (go.activeSelf)
                            {
                                go.SetActive(false);
                                Debug.Log($"ReloadMissionHider: Disabled GameObject '{go.name}' in scene '{scene.name}'.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ReloadMissionHider: Exception during immediate-disable: " + ex);
            }
        }

        private static bool IsTargetMission2(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var lower = name.ToLowerInvariant();
            if (!lower.Contains("mission")) return false;
            if (lower.Contains("(2)") || lower.Contains("（2）") || lower.Contains(" 2") || lower.EndsWith("2") || lower.Contains("mission2") || lower.Contains("(２)"))
                return true;
            if (lower.Contains("mission") && lower.IndexOf('2') >= 0) return true;
            return false;
        }
    }
}
