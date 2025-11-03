using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// マッチングシーンでゲーム開始ボタンを制御するクラス。
/// 部屋が満員かつマスタークライアントの場合のみボタンを表示します。
/// </summary>
[RequireComponent(typeof(Button))]
public class StartGameButton : MonoBehaviourPunCallbacks
{
    public NetworkManager networkManager;

    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnStartGameClicked);
        this.gameObject.SetActive(false);
    }

    void OnStartGameClicked()
    {
        if (networkManager != null)
        {
            networkManager.StartGame();
        }
        else
        {
            Debug.LogError("StartGameButton: NetworkManager が設定されていません");
        }
    }

    void UpdateButtonVisibility()
    {
        // マスタークライアントかつ部屋が満員の場合のみボタンを表示
        bool shouldShow = PhotonNetwork.IsMasterClient 
            && PhotonNetwork.InRoom 
            && PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers;

        button.interactable = shouldShow;
        Debug.Log("ShouldShow:" + shouldShow);
        this.gameObject.SetActive(shouldShow);
    }

    // Photon コールバック: プレイヤーが入室したときに更新
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log("コールバック！！");
        UpdateButtonVisibility();
    }

    // Photon コールバック: プレイヤーが退室したときに更新
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateButtonVisibility();
    }

    // Photon コールバック: マスタークライアントが変わったときに更新
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        UpdateButtonVisibility();
    }
}