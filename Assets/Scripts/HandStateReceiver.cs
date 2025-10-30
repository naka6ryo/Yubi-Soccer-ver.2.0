using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

/// <summary>
/// HandStateReceiver
/// 親ページ（ブラウザ）から SendMessage で渡される JSON を受け取り
/// ステート (KICK / RUN / NONE など) と信頼度を画面に表示します。
/// 
/// 使い方:
/// - シーン内に適当な GameObject (例: EmbeddedReceiver) を作り、このスクリプトをアタッチ
/// - Inspector に TextMeshProUGUI（または UI.Text）をアサイン
/// - ブラウザ側は SendMessage('EmbeddedReceiver', 'OnEmbeddedState', jsonString) を送信
/// 
/// 受け取り JSON 例: { "type":"embedded_state", "state":"KICK", "confidence":0.92 }
/// </summary>
public class HandStateReceiver : MonoBehaviour
{
    [Tooltip("TextMeshPro のコンポーネントをアサインする（TMPro.TextMeshProUGUI）。Inspector で直接アサインできます。\n未使用の場合は UI Text（uiText）を使います。")]
    public Component tmpProText; // assign TextMeshProUGUI here (Component so compiles even if TMPro not present)

    [Tooltip("フォールバックの UI.Text（Canvas 上の Text）")]
    public Text uiText;

    [Tooltip("フォールバックで表示する 3D TextMesh を自動生成する場合は true にする。")]
    public bool createWorldTextFallback = true;

    // internal reference to fallback TextMesh
    private TextMesh _worldText;

    [Serializable]
    private class EmbeddedStatePayload
    {
        public string type;
        public string state;
        public float confidence;
    }

    void Start()
    {
        try { Debug.Log($"HandStateReceiver.Start GameObject={gameObject.name} tmpProText={(tmpProText != null)} uiText={(uiText != null)} createWorldTextFallback={createWorldTextFallback}"); } catch { }
        // Try to auto-find assigned UI components if not set
        if (tmpProText == null)
        {
            // try to find a TMPro.TextMeshProUGUI in scene via type name (works even if TMPro assembly absent)
            // Use Resources.FindObjectsOfTypeAll to avoid deprecated APIs and to work across Unity versions
            var all = Resources.FindObjectsOfTypeAll<Component>();
            foreach (var c in all)
            {
                if (c == null) continue;
                var tn = c.GetType().FullName;
                if (tn == "TMPro.TextMeshProUGUI") { tmpProText = c; break; }
            }
        }
        if (uiText == null)
        {
            var texts = Resources.FindObjectsOfTypeAll<Text>();
            uiText = texts.FirstOrDefault();
        }

        if (!HasAnyText() && createWorldTextFallback)
        {
            CreateWorldText();
        }
    }

    bool HasAnyText()
    {
        return tmpProText != null || uiText != null;
    }

    void CreateWorldText()
    {
        var go = new GameObject("HandState_WorldText");
        go.transform.SetParent(this.transform, false);
        go.transform.localPosition = new Vector3(0, 1.8f, 2.0f);
        var tm = go.AddComponent<TextMesh>();
        tm.fontSize = 48;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.yellow;
        tm.text = "State: -";
        _worldText = tm;
    }

    /// <summary>
    /// Unity の SendMessage から呼ばれるエントリポイント。
    /// 引数 json は JSON 文字列。
    /// </summary>
    /// <param name="json"></param>
    [Preserve]
    public void OnEmbeddedState(string json)
    {
        // Always log raw incoming payload so we can see it in the browser console for WebGL builds
        try { Debug.Log("HandStateReceiver.OnEmbeddedState raw: " + (json ?? "(null)")); } catch { }
        ;
        if (string.IsNullOrEmpty(json)) return;
        EmbeddedStatePayload payload = null;
        try
        {
            payload = JsonUtility.FromJson<EmbeddedStatePayload>(json);
        }
        catch (Exception)
        {
            // 受け取り形式が直接 {state:...,confidence:...} の場合にも対応
            try { payload = JsonUtility.FromJson<EmbeddedStatePayload>("{\"type\":\"embedded_state\",\"state\":\"" + json + "\",\"confidence\":0}"); } catch { }
        }

        if (payload == null)
        {
            Debug.LogWarning("HandStateReceiver: 受信した JSON の解析に失敗しました: " + json);
            return;
        }

        // Log parsed payload for visibility in browser console (WebGL)
        try { Debug.Log($"HandStateReceiver.parsed: state={payload.state} confidence={payload.confidence}"); } catch { }

        // Prepare display text
        var text = $"State: {payload.state}\nConfidence: {payload.confidence:F2}";

        // Update UI: prefer TMP if assigned (set via reflection so script compiles without TMPro)
        if (tmpProText != null)
        {
            TrySetTextMeshPro(tmpProText, text, ColorForState(payload.state));
            return;
        }
        if (uiText != null)
        {
            uiText.text = text;
            uiText.color = ColorForState(payload.state);
            return;
        }

        if (_worldText != null)
        {
            _worldText.text = text;
            _worldText.color = ColorForState(payload.state);
            return;
        }

        // Fallback to Debug
        Debug.Log("HandStateReceiver received: " + text);
    }

    private Color ColorForState(string state)
    {
        if (string.IsNullOrEmpty(state)) return Color.white;
        switch (state.ToUpperInvariant())
        {
            case "KICK": return Color.red;
            case "RUN": return Color.green;
            case "NONE": return Color.gray;
            default: return Color.cyan;
        }
    }

    // Try to set TMP text via reflection (works even if TMPro assembly is not referenced at compile time)
    private void TrySetTextMeshPro(Component comp, string text, Color color)
    {
        if (comp == null) return;
        try
        {
            var type = comp.GetType();
            var textProp = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (textProp != null && textProp.CanWrite)
            {
                textProp.SetValue(comp, text, null);
            }
            var colorProp = type.GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
            if (colorProp != null && colorProp.CanWrite)
            {
                colorProp.SetValue(comp, color, null);
            }
            return;
        }
        catch (Exception e)
        {
            Debug.LogWarning("HandStateReceiver: failed to set TextMeshPro via reflection: " + e.Message);
        }
    }
}
