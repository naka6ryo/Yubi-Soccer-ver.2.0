using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 再戦に関するネットワーク通信を管理
    /// </summary>
    public class RematchNetworkHandler : MonoBehaviourPunCallbacks
    {
        private const string REMATCH_PROP_KEY = "IsReadyToRematch";
        
        public event System.Action OnAllPlayersReady;
        public event System.Action OnPlayerLeft;

        private void Start()
        {
            SetRematchStatus(false);
        }

        /// <summary>
        /// 自分の再戦準備完了状態を設定
        /// </summary>
        public void SetRematchStatus(bool isReady)
        {
            Hashtable props = new Hashtable
            {
                { REMATCH_PROP_KEY, isReady }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        /// <summary>
        /// タイトルへ戻る命令を全員に送信
        /// </summary>
        public void RequestBackToTitle()
        {
            photonView.RPC(nameof(RpcBackToTitle), RpcTarget.All);
        }

        [PunRPC]
        private void RpcBackToTitle()
        {
            PhotonNetwork.LeaveRoom();
        }

        /// <summary>
        /// 全員が準備完了したことを全員に通知
        /// </summary>
        [PunRPC]
        private void RpcAllPlayersReady()
        {
            OnAllPlayersReady?.Invoke();
        }

        // ---------------------------------------------------
        // Photonコールバック
        // ---------------------------------------------------

        public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
        {
            if (!changedProps.ContainsKey(REMATCH_PROP_KEY)) return;

            if (PhotonNetwork.IsMasterClient)
            {
                CheckAllPlayersReady();
            }
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            OnPlayerLeft?.Invoke();
            
            if (PhotonNetwork.IsMasterClient)
            {
                CheckAllPlayersReady();
            }
        }

        private void CheckAllPlayersReady()
        {
            foreach (Photon.Realtime.Player p in PhotonNetwork.PlayerList)
            {
                if (!p.CustomProperties.ContainsKey(REMATCH_PROP_KEY) || 
                    !(bool)p.CustomProperties[REMATCH_PROP_KEY])
                {
                    return;
                }
            }

            // 全員準備完了したら、RPCで全員に通知
            photonView.RPC(nameof(RpcAllPlayersReady), RpcTarget.All);
        }
    }
}