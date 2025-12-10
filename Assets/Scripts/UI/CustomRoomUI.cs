using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YubiSoccer.UI;

public class CustomRoomUI : MonoBehaviour
{
    public Button oneOnOneButton;
    public Button twoOnTwoButton;
    public Button threeOnThreeButton;
    public Button createRoomButton;
    public Button backButton;
    public TMP_InputField roomNameInput;

    public NetworkManager networkManager;
    public ChangePictureController changePictureController;
    private byte maxPlayers = 0;

    void Start()
    {
        if (oneOnOneButton != null) oneOnOneButton.onClick.AddListener(OnOneOnOneButtonClick);
        if (twoOnTwoButton != null) twoOnTwoButton.onClick.AddListener(OnTwoOnTwoButtonClick);
        if (threeOnThreeButton != null) threeOnThreeButton.onClick.AddListener(OnThreeOnThreeButtonClick);
        if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoomButtonClick);
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClick);
    }

    void OnOneOnOneButtonClick()
    {
        maxPlayers = 2;
        if (changePictureController != null) changePictureController.OnClicked();
    }

    void OnTwoOnTwoButtonClick()
    {
        maxPlayers = 4;
        if (changePictureController != null) changePictureController.OnClicked();
    }

    void OnThreeOnThreeButtonClick()
    {
        maxPlayers = 6;
        if (changePictureController != null) changePictureController.OnClicked();
    }

    void OnCreateRoomButtonClick()
    {
        string roomName = roomNameInput.text.Trim();
        
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("ルーム名を入力してください");
            return;
        }

        if (maxPlayers != 0)
        {
            networkManager.CreateCustomRoom(roomName, maxPlayers);
        }
        else
        {
            networkManager.JoinCustomRoom(roomName);
        }  
    }

    void OnBackButtonClick()
    {
        if (changePictureController != null) changePictureController.OnClickedReverse();
    }
}