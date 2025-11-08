using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CustomRoomUI : MonoBehaviour
{
    [Header("Create Room UI")]
    public TMP_InputField createRoomNameInput;
    public TMP_Dropdown maxPlayersDropdown;
    public Button createRoomButton;

    [Header("Join Room UI")]
    public TMP_InputField joinRoomNameInput;
    public Button joinRoomButton;

    [Header("NetworkManager")]
    public NetworkManager networkManager;

    void Start()
    {
        // ボタンのクリックイベントを設定
        createRoomButton.onClick.AddListener(OnCreateRoomButtonClick);
        joinRoomButton.onClick.AddListener(OnJoinRoomButtonClick);
    }

    void OnCreateRoomButtonClick()
    {
        string roomName = createRoomNameInput.text.Trim();
        
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("ルーム名を入力してください");
            return;
        }

        // Dropdown の値を MaxPlayers に変換（0=2人, 1=4人, 2=6人）
        byte maxPlayers = (byte)(2 + maxPlayersDropdown.value * 2);

        networkManager.CreateCustomRoom(roomName, maxPlayers);
    }

    void OnJoinRoomButtonClick()
    {
        string roomName = joinRoomNameInput.text.Trim();

        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("ルーム名を入力してください");
            return;
        }

        networkManager.JoinCustomRoom(roomName);
    }
}