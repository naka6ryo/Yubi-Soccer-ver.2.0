using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン起動後の短時間、'Mission (2)' に相当するオブジェクトを監視して見つけ次第強制的に非表示化します。
/// 動的に生成されるオブジェクトや他スクリプトによる再有効化に対処するため、数フレームにわたり繰り返しチェックします。
/// </summary>
public class Mission2Disabler : MonoBehaviour
{
    // 監視するフレーム数（適宜調整）
    private const int WatchFrames = 300; // 約5秒（エディタ/環境により異なる）

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("_Mission2Disabler_Runtime");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        var comp = go.AddComponent<Mission2Disabler>();
        // Register to sceneLoaded so we can perform immediate checks right after the scene is loaded
        try { UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => OnSceneLoaded(scene, mode, comp); } catch { }
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode, Mission2Disabler comp)
    {
        try
        {
            if (comp != null)
            {
                // Start an immediate rapid-check coroutine to reduce visible flicker
                try { comp.StartImmediateDisable(); } catch { }
            }
        }
        catch { }
    }

    private void Start()
    {
        StartCoroutine(WatchAndDisable());
    }

    private void StartImmediateDisable()
    {
        try { StartCoroutine(ImmediateDisable()); } catch { }
    }

    private IEnumerator ImmediateDisable()
    {
        // Try a few times at end-of-frame to catch activations happening during other components' Start()
        for (int i = 0; i < 8; i++)
        {
            TryDisableMatchesInActiveScene();
            yield return new WaitForEndOfFrame();
        }
    }

    private IEnumerator WatchAndDisable()
    {
        int frame = 0;
        while (frame < WatchFrames)
        {
            TryDisableMatchesInActiveScene();
            frame++;
            yield return null;
        }

        // 一定時間経過後は不要なので自身を破棄
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
                // GetComponentsInChildren with includeInactive=true を使って非アクティブの子も探索
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    var go = t.gameObject;
                    if (IsTargetMission2(go.name))
                    {
                        if (go.activeSelf)
                        {
                            go.SetActive(false);
                            Debug.Log($"Mission2Disabler: Disabled GameObject '{go.name}' in scene '{scene.name}'.");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Mission2Disabler: Exception during check: {ex}");
        }
    }

    private static bool IsTargetMission2(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var lower = name.ToLowerInvariant();
        if (!lower.Contains("mission")) return false;

        // 明示的に "2" 系の表記を含むかどうかで判定
        if (lower.Contains("(2)") || lower.Contains("（2）") || lower.Contains(" 2") || lower.EndsWith("2") || lower.Contains("mission2") || lower.Contains("(２)"))
            return true;

        // 名前に 'mission' と '2'（数字）が共に含まれる場合も対象
        if (lower.Contains("mission") && lower.IndexOf('2') >= 0) return true;

        return false;
    }
}
