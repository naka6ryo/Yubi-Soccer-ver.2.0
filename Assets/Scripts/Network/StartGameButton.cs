using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲーム開始ボタンの表示制御を担当するクラス。
/// NetworkManager から呼び出されます。
/// </summary>
[RequireComponent(typeof(Button))]
public class StartGameButton : MonoBehaviour
{
    [Tooltip("NetworkManager への参照")]
    public NetworkManager networkManager;

    public Button button;

    void Awake()
    {
        button.onClick.AddListener(OnStartGameClicked);
        gameObject.SetActive(false);
    }

    void OnStartGameClicked()
    {
        Debug.Log("ボタンは押せてる");
        if (networkManager != null)
        {
            networkManager.StartGame();
        }
        else
        {
            Debug.LogError("StartGameButton: NetworkManager が設定されていません");
        }
    }

    /// <summary>
    /// ボタンの表示/非表示を制御する（NetworkManager から呼ばれる）
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (button != null)
        {
            button.interactable = visible;
        }
        gameObject.SetActive(visible);
    }
}