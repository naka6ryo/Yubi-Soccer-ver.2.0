using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;

namespace YubiSoccer.Network
{
    /// <summary>
    /// ボールへのインパルス(力+リフト)を全クライアントへ配信するユーティリティ。
    /// 各クライアントは BallImpulseReceiver により次の FixedUpdate で同じ AddForce を適用する。
    /// </summary>
    public static class BallImpulseBroadcaster
    {
        // RaiseEvent で使用するイベントコード
        public const byte EventCode = 10;

        // 送信者ローカルでインクリメントされる連番（重複適用防止用）
        // Photon は System.UInt32 をそのまま送れないため int を使用
        private static int _localSeq = 0;

        // デバッグログを有効化するフラグ（Inspector からは設定できないので、必要に応じてコード内で true に）
        public static bool DebugLog = false;

        /// <summary>
        /// インパルスを全クライアントに送信。
        /// </summary>
        /// <param name="ballViewId">対象ボールの PhotonView.ViewID</param>
        /// <param name="impulse">水平インパルス</param>
        /// <param name="lift">上向きインパルス</param>
        /// <param name="contact">任意の接触点（VFXなど用途）</param>
        public static void RaiseImpulse(int ballViewId, Vector3 impulse, float lift, Vector3 contact)
        {
            _localSeq++;
            var data = new object[]
            {
                ballViewId,
                impulse.x, impulse.y, impulse.z,
                lift,
                contact.x, contact.y, contact.z,
                _localSeq,
                PhotonNetwork.LocalPlayer?.ActorNumber ?? -1,
                PhotonNetwork.Time
            };

            var options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.All
            };
            var sendOptions = new SendOptions { Reliability = true };

            bool success = PhotonNetwork.RaiseEvent(EventCode, data, options, sendOptions);

            if (!success)
            {
                Debug.LogWarning($"[BallImpulseBroadcaster] Failed to send impulse event! IsConnected={PhotonNetwork.IsConnected}, InRoom={PhotonNetwork.InRoom}");
            }
        }
    }
}
