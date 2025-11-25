using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// シーン内のすべての UI Button の onClick に効果音再生リスナを追加します。
    /// - Inspector の `seName` に任意の SE 名（例: "決定"）を入力してください。
    /// - 動的に生成されるボタンをサポートする場合は `rescanInterval` を >0 に設定します。
    /// - 通常はプレハブとしてシーンに 1 つだけ配置してください（makeSingleton=true がデフォルト）。
    /// </summary>
    public class AnyButtonSEPlayer : MonoBehaviour
    {
        [Tooltip("再生する SE の名前（SoundManager のキー）、例: 決定")] public string seName = "決定";
        [Tooltip("動的に生成されるボタンを自動で検出するための再スキャン間隔(秒)。0 = 再スキャンしない")]
        public float rescanInterval = 0f;
        [Tooltip("非アクティブな GameObject 上の Button も含めて検出するか（Unity 2020.2+ の FindObjectsOfType(includeInactive) を使用）")]
        public bool includeInactive = false;
        [Tooltip("シーン内に複数配置された場合、先に存在するものを残して重複を避けるシングルトン動作")]
        public bool makeSingleton = true;

        private static AnyButtonSEPlayer instance;
        private readonly HashSet<int> hookedIds = new HashSet<int>();
        private readonly List<Button> hookedButtons = new List<Button>();
        private UnityAction playAction;

        private void Awake()
        {
            if (makeSingleton)
            {
                if (instance != null && instance != this)
                {
                    Debug.Log("AnyButtonSEPlayer: Another instance already exists — destroying duplicate.");
                    Destroy(this);
                    return;
                }
                instance = this;
            }

            playAction = PlaySE;
            HookAllButtons();

            if (rescanInterval > 0f)
            {
                InvokeRepeating(nameof(HookAllButtons), rescanInterval, rescanInterval);
            }
        }

        private void OnDestroy()
        {
            // Remove listeners we added
            try
            {
                foreach (var b in hookedButtons)
                {
                    if (b != null)
                    {
                        try { b.onClick.RemoveListener(playAction); } catch { }
                    }
                }
            }
            catch { }

            if (instance == this) instance = null;
            CancelInvoke(nameof(HookAllButtons));
        }

        private void HookAllButtons()
        {
            Button[] buttons = null;
            try
            {
                buttons = UnityEngine.Object.FindObjectsOfType<Button>(includeInactive);
            }
            catch
            {
                // Fallback if includeInactive overload not available (older Unity): use simple FindObjectsOfType
                try { buttons = UnityEngine.Object.FindObjectsOfType<Button>(); } catch { buttons = new Button[0]; }
            }

            foreach (var b in buttons)
            {
                if (b == null) continue;
                int id = b.GetInstanceID();
                if (hookedIds.Contains(id)) continue;
                try
                {
                    b.onClick.AddListener(playAction);
                    hookedIds.Add(id);
                    hookedButtons.Add(b);
                    Debug.Log($"AnyButtonSEPlayer: Hooked Button '{GetGameObjectPath(b.gameObject)}'.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("AnyButtonSEPlayer: Failed to add listener to Button " + b.name + " : " + ex);
                }
            }
        }

        private void PlaySE()
        {
            try
            {
                if (string.IsNullOrEmpty(seName)) return;
                var sm = SoundManager.Instance;
                if (sm != null)
                {
                    sm.PlaySE(seName);
                }
                else
                {
                    Debug.LogWarning($"AnyButtonSEPlayer: SoundManager.Instance is null. Tried to play '{seName}'");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("AnyButtonSEPlayer: Exception while PlaySE: " + ex);
            }
        }

        private string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "(null)";
            try
            {
                string path = go.name;
                var t = go.transform.parent;
                while (t != null)
                {
                    path = t.name + "/" + path;
                    t = t.parent;
                }
                return path;
            }
            catch { return go.name; }
        }
    }
}
