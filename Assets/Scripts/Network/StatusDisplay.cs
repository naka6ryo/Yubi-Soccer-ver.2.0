using UnityEngine;
using TMPro;

/// <summary>
/// ステータステキストの表示を担当するクラス。
/// NetworkManager や他のシステムから参照して、接続状態やメッセージを表示できます。
/// </summary>
public class StatusDisplay : MonoBehaviour
{
    [Tooltip("ステータス表示用の TextMeshPro テキスト")]
    public TMP_Text statusText;

    /// <summary>
    /// ステータステキストを更新する
    /// </summary>
    public void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    /// <summary>
    /// ステータステキストをクリアする
    /// </summary>
    public void ClearStatus()
    {
        if (statusText != null)
        {
            statusText.text = string.Empty;
        }
    }
}