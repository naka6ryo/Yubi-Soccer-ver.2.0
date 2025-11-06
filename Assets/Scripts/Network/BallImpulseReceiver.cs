using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;

namespace YubiSoccer.Network
{
    /// <summary>
    /// ボール側に取り付け、受信したインパルスを次の FixedUpdate で適用する。
    /// 依存: 同じゲームオブジェクトに PhotonView と Rigidbody が必要。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PhotonView))]
    public class BallImpulseReceiver : MonoBehaviour, IOnEventCallback
    {
        struct QueuedImpulse
        {
            public Vector3 impulse;
            public float lift;
            public int seq;
            public double serverTime;
        }

        Rigidbody _rb;
        PhotonView _pv;
        readonly Queue<QueuedImpulse> _queue = new Queue<QueuedImpulse>();
        readonly HashSet<int> _appliedSeq = new HashSet<int>();

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _pv = GetComponent<PhotonView>();
        }

        void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != BallImpulseBroadcaster.EventCode) return;

            var data = photonEvent.CustomData as object[];
            if (data == null || data.Length < 11) return;

            int viewId = (int)data[0];
            if (_pv == null || _pv.ViewID != viewId) return; // 自分宛てでない

            var impulse = new Vector3((float)data[1], (float)data[2], (float)data[3]);
            float lift = (float)data[4];
            // contact: x,y,z は data[5..7]（今は未使用）
            int seq = (int)data[8];
            // senderActor = (int)data[9];
            double serverTime = (double)data[10];

            if (_appliedSeq.Contains(seq)) return;

            _queue.Enqueue(new QueuedImpulse
            {
                impulse = impulse,
                lift = lift,
                seq = seq,
                serverTime = serverTime
            });
        }

        void FixedUpdate()
        {
            if (_rb == null) return;

            while (_queue.Count > 0)
            {
                var qi = _queue.Dequeue();
                if (_appliedSeq.Contains(qi.seq)) continue;

                if (qi.impulse != Vector3.zero)
                {
                    _rb.AddForce(qi.impulse, ForceMode.Impulse);
                }
                if (qi.lift > 0f)
                {
                    _rb.AddForce(Vector3.up * qi.lift, ForceMode.Impulse);
                }

                _appliedSeq.Add(qi.seq);
            }
        }
    }
}
