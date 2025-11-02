using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// ジョイスティックの位置をデバイスの傾きに応じて動的に変更するコントローラー
/// ブラウザ（JavaScript）から SendMessage で呼ばれて、ジョイスティックを左右に配置します。
/// </summary>
public class JoystickPositionController : MonoBehaviour
{
    [Header("Joystick Reference")]
    [Tooltip("制御対象のジョイスティックのRectTransform")]
    public RectTransform joystickRect;

    [Header("Position Settings")]
    [Tooltip("左側配置時のX位置（画面左端からのオフセット）")]
    public float leftPositionX = 100f;

    [Tooltip("右側配置時のX位置（画面右端からのオフセット）")]
    public float rightPositionX = -100f;

    [Tooltip("Y位置（画面下端からのオフセット）")]
    public float positionY = 100f;

    [Tooltip("位置変更のアニメーション速度")]
    public float transitionSpeed = 5f;

    private Vector2 targetAnchoredPosition;
    private string currentSide = "right"; // "left" or "right"

    void Start()
    {
        if (joystickRect == null)
        {
            Debug.LogWarning("JoystickPositionController: joystickRect is not assigned!");
            return;
        }

        // 初期位置を右側に設定
        SetJoystickSide("right", immediate: true);
    }

    void Update()
    {
        if (joystickRect == null) return;

        // スムーズに目標位置へ移動
        joystickRect.anchoredPosition = Vector2.Lerp(
            joystickRect.anchoredPosition,
            targetAnchoredPosition,
            Time.deltaTime * transitionSpeed
        );
    }

    /// <summary>
    /// JavaScriptから呼ばれるメソッド: ジョイスティックを指定側に配置
    /// </summary>
    /// <param name="side">"left" または "right"</param>
    [Preserve]
    public void SetJoystickSide(string side)
    {
        SetJoystickSide(side, immediate: false);
    }

    private void SetJoystickSide(string side, bool immediate)
    {
        if (joystickRect == null) return;

        currentSide = side;

        // ピボットは常に中心に設定（表示と当たり判定を一致させる）
        joystickRect.pivot = new Vector2(0.5f, 0.5f);

        if (side == "left")
        {
            // カメラが左側 → ジョイスティックは右側
            joystickRect.anchorMin = new Vector2(1, 0); // 右下基準
            joystickRect.anchorMax = new Vector2(1, 0);
            targetAnchoredPosition = new Vector2(rightPositionX, positionY);
        }
        else
        {
            // カメラが右側（またはデフォルト） → ジョイスティックは左側
            joystickRect.anchorMin = new Vector2(0, 0); // 左下基準
            joystickRect.anchorMax = new Vector2(0, 0);
            targetAnchoredPosition = new Vector2(leftPositionX, positionY);
        }

        if (immediate)
        {
            joystickRect.anchoredPosition = targetAnchoredPosition;
        }

        Debug.Log($"JoystickPositionController: Set side to {side} (camera on {side}, joystick on {(side == "left" ? "right" : "left")})");
    }

    /// <summary>
    /// 現在の配置側を取得（デバッグ用）
    /// </summary>
    public string GetCurrentSide()
    {
        return currentSide;
    }
}
