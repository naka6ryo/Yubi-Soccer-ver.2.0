using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
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
    // 状態変化イベント
    [System.Serializable]
    public class StateChangeEvent : UnityEvent<string, float> { }

    [Header("Events")]
    [Tooltip("状態が変化したときに発火するイベント。パラメータ: state, confidence")]
    public StateChangeEvent onStateChanged = new StateChangeEvent();

    [Header("UI Display (Optional)")]
    [Tooltip("TextMeshPro のコンポーネントをアサインする（TMPro.TextMeshProUGUI）。Inspector で直接アサインできます。")]
    public Component tmpProText; // assign TextMeshProUGUI here (Component so compiles even if TMPro not present)

    [Tooltip("フォールバックの UI.Text（Canvas 上の Text）")]
    public Text uiText;

    // 現在のステートと信頼度（他のスクリプトから参照可能）
    [Header("Current State")]
    public string currentState = "NONE";
    public float currentConfidence = 0f;

    // 前回の状態（状態変化検出用）
    private string previousState = "NONE";

    [Serializable]
    private class EmbeddedStatePayload
    {
        public string type;
        public string state;
        public float confidence;
    }

    void Start()
    {
        try { Debug.Log($"HandStateReceiver.Start GameObject={gameObject.name}"); } catch { }
        // 自動テキストアタッチ処理を削除
        // 必要に応じて Inspector で手動でアサインしてください
    }

    /// <summary>
    /// Unity の SendMessage から呼ばれるエントリポイント。
    /// 引数 json は JSON 文字列。
    /// </summary>
    /// <param name="json"></param>
    [Preserve]
    public void OnEmbeddedState(string json)
    {
        // スロットリングを削除して即座に反応するように変更
        // 状態の更新を確実に受け取るため、すべてのメッセージを処理

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

        // 前回の状態を保存
        previousState = currentState;

        // Update current state (public variables for other scripts to access)
        currentState = payload.state ?? "NONE";
        currentConfidence = payload.confidence;

        // 状態が変化した場合、イベントを発火
        if (previousState != currentState)
        {
            try
            {
                Debug.Log($"HandStateReceiver: State changed from {previousState} to {currentState}");
                onStateChanged?.Invoke(currentState, currentConfidence);
            }
            catch (Exception e)
            {
                Debug.LogError($"HandStateReceiver: Error invoking state change event: {e.Message}");
            }
        }

        // Prepare display text (オプション: UI が設定されている場合のみ表示)
        if (tmpProText != null || uiText != null)
        {
            var text = $"State: {payload.state}\nConfidence: {payload.confidence:F2}";
            var color = ColorForState(payload.state);

            // Update UI: prefer TMP if assigned (set via reflection so script compiles without TMPro)
            if (tmpProText != null)
            {
                TrySetTextMeshPro(tmpProText, text, color);
            }
            else if (uiText != null)
            {
                uiText.text = text;
                uiText.color = color;
            }
        }
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
