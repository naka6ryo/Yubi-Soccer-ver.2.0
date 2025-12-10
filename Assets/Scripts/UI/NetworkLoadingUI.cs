using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

namespace YubiSoccer.UI
{
    /// <summary>
    /// Photon Network 接続完了までローディング画面を表示
    /// ConnectedToMasterServer になるまで表示し続ける
    /// </summary>
    public class NetworkLoadingUI : MonoBehaviourPunCallbacks
    {
        [Header("UI References")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image loadingIcon; // 回転するアイコン（任意）

        [Header("Loading Animation")]
        [SerializeField] private bool rotateLoadingIcon = true;
        [SerializeField] private float rotationSpeed = 180f; // 度/秒

        [Header("Status Messages")]
        [SerializeField] private string connectingMessage = "Connecting to Server...";
        [SerializeField] private string connectedMessage = "Connected!";
        [SerializeField] private string disconnectedMessage = "Disconnected...";
        [SerializeField] private string joiningLobbyMessage = "Joining Lobby...";

        [Header("Auto Hide Settings")]
        [SerializeField] private bool autoHideOnConnected = true;
        [SerializeField] private float hideDelay = 0.5f; // 接続完了後、少し待ってから非表示

        private void Start()
        {
            UpdateLoadingUI();
        }

        private void Update()
        {
            // ローディングアイコンを回転
            if (rotateLoadingIcon && loadingIcon != null && loadingPanel != null && loadingPanel.activeSelf)
            {
                loadingIcon.transform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// ローディングUIの表示/非表示を更新
        /// </summary>
        private void UpdateLoadingUI()
        {
            bool isConnected = PhotonNetwork.IsConnected && PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer;

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(!isConnected);
            }

            UpdateStatusText();
        }

        /// <summary>
        /// ステータステキストを更新
        /// </summary>
        private void UpdateStatusText()
        {
            if (statusText == null) return;

            ClientState state = PhotonNetwork.NetworkClientState;

            switch (state)
            {
                case ClientState.ConnectedToMasterServer:
                    statusText.text = connectedMessage;
                    break;

                case ClientState.ConnectingToMasterServer:
                case ClientState.Authenticating:
                    statusText.text = connectingMessage;
                    break;

                case ClientState.JoiningLobby:
                case ClientState.JoinedLobby:
                    statusText.text = joiningLobbyMessage;
                    break;

                case ClientState.Disconnected:
                case ClientState.Disconnecting:
                    statusText.text = disconnectedMessage;
                    break;

                default:
                    statusText.text = $"Status: {state}";
                    break;
            }
        }

        #region Photon Callbacks

        public override void OnConnectedToMaster()
        {
            Debug.Log("[NetworkLoadingUI] Connected to Master Server!");
            UpdateStatusText();

            if (autoHideOnConnected)
            {
                Invoke(nameof(HideLoadingPanel), hideDelay);
            }
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log($"[NetworkLoadingUI] Disconnected: {cause}");
            UpdateLoadingUI();
        }

        public override void OnConnected()
        {
            Debug.Log("[NetworkLoadingUI] Connected!");
            UpdateLoadingUI();
        }

        public override void OnJoinedLobby()
        {
            Debug.Log("[NetworkLoadingUI] Joined Lobby!");
            UpdateStatusText();
        }

        #endregion

        /// <summary>
        /// ローディングパネルを非表示にする
        /// </summary>
        private void HideLoadingPanel()
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 外部から手動でローディングを表示/非表示
        /// </summary>
        public void ShowLoading(bool show)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(show);
            }

            if (show)
            {
                UpdateStatusText();
            }
        }

        /// <summary>
        /// ステータステキストを手動で設定
        /// </summary>
        public void SetStatusText(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
