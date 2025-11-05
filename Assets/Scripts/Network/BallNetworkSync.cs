using Photon.Pun;
using UnityEngine;
using System.Collections.Generic;

namespace YubiSoccer.Network
{
    /// <summary>
    /// ボールの位置/回転/速度/角速度を Photon で同期する。
    /// - 所有者(PhotonView.IsMine)は状態を書き込み
    /// - 非所有者は受信値へ補間し、乖離が閾値以上ならスナップ
    /// Impulse 配信（全員 AddForce）の“安全網”として併用すると安定する。
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BallNetworkSync : MonoBehaviour, IPunObservable
    {
        [Header("Smoothing (non-owners)")]
        [Tooltip("位置の補間係数（0-1）。値が大きいほど速く追従")]
        [Range(0.01f, 1f)] public float positionLerp = 0.3f;
        [Tooltip("回転の補間係数（0-1）。値が大きいほど速く追従")]
        [Range(0.01f, 1f)] public float rotationLerp = 0.35f;

        [Header("Snap thresholds")]
        [Tooltip("この距離以上ズレたらテレポート（位置スナップ）")]
        public float teleportDistance = 2.5f;
        [Tooltip("この角度以上ズレたら回転をスナップ（度）")]
        public float teleportAngleDeg = 25f;
        [Tooltip("非所有者で位置の補正を行う（OFF で位置は補正しない）")]
        public bool correctPositionRotation = true; // 既存互換: 位置補正用フラグとして扱う
        [Tooltip("非所有者で回転の補正を行う（OFF で回転は補正しない）")]
        public bool correctRotation = false;

        [Header("Velocity follow")]
        [Tooltip("非所有者が受信した速度/角速度へも追従するか")]
        public bool followVelocities = false;
        [Tooltip("速度の補間係数（0-1）")]
        [Range(0.01f, 1f)] public float velocityLerp = 0.5f;
        [Tooltip("開始直後にテレポート補正を抑制する時間（秒）")]
        public float warmupNoTeleportSeconds = 0.5f;
        [Tooltip("角速度の補間係数（0-1）")]
        [Range(0.01f, 1f)] public float angularVelocityLerp = 0.5f;

        private PhotonView _pv;
        private Rigidbody _rb;

        // 受信側が追従するターゲット状態
        private Vector3 _targetPos;
        [Header("Authority options")]
        [Tooltip("非所有者では Rigidbody を Kinematic にして、所有者のみ物理シミュレーションする")]
        public bool nonOwnerKinematic = false;
        private Quaternion _targetRot;
        private Vector3 _targetVel;
        private Vector3 _targetAngularVel;
        private bool _initialized;

        private void Awake()
        {
            _pv = GetComponent<PhotonView>();
            _rb = GetComponent<Rigidbody>();
            EnsureObservedByPhotonView();
        }
        private float _startTime;

        private void Start()
        {
            if (!_pv) _pv = GetComponent<PhotonView>();
            if (!_rb) _rb = GetComponent<Rigidbody>();
            EnsureObservedByPhotonView();

            // 受信側で初期値を設定
            _targetPos = transform.position;
            _targetRot = transform.rotation;
            _targetVel = _rb.linearVelocity;
            _targetAngularVel = _rb.angularVelocity;
            _initialized = true;
            _startTime = Time.time;

            // 非所有者の物理権限設定（任意）
            if (_pv != null && !_pv.IsMine && nonOwnerKinematic)
            {
                _rb.isKinematic = true;
            }
        }

        // PhotonView の ObservedComponents に自分を登録（未設定だと OnPhotonSerializeView が呼ばれず非オーナーが更新を受け取れない）
        private void EnsureObservedByPhotonView()
        {
            if (_pv == null) _pv = GetComponent<PhotonView>();
            if (_pv == null) return;
            var list = _pv.ObservedComponents;
            if (list == null)
            {
                list = new List<Component>();
                _pv.ObservedComponents = list;
            }
            if (!list.Contains(this))
            {
                list.Add(this);
            }
        }

        private void FixedUpdate()
        {
            if (!_initialized || _pv == null || _rb == null) return;
            if (_pv.IsMine) return; // 所有者は物理を通常通り進める

            bool allowTeleport = (Time.time - _startTime) > warmupNoTeleportSeconds;

            // 位置補正（小さなズレは無視して揺れを抑制）
            if (correctPositionRotation)
            {
                float posDist = Vector3.Distance(_rb.position, _targetPos);
                if (allowTeleport && posDist > teleportDistance)
                {
                    _rb.position = _targetPos;
                }
                else if (posDist > 0.01f)
                {
                    Vector3 p = Vector3.Lerp(_rb.position, _targetPos, positionLerp);
                    _rb.MovePosition(p);
                }
            }

            // 回転補正（オプトイン）
            if (correctRotation)
            {
                float ang = Quaternion.Angle(_rb.rotation, _targetRot);
                if (allowTeleport && ang > teleportAngleDeg)
                {
                    _rb.rotation = _targetRot;
                }
                else if (ang > 0.5f)
                {
                    Quaternion r = Quaternion.Slerp(_rb.rotation, _targetRot, rotationLerp);
                    _rb.MoveRotation(r);
                }
            }

            if (followVelocities)
            {
                _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, _targetVel, velocityLerp);
                _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, _targetAngularVel, angularVelocityLerp);
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody>();
                if (_rb == null) return;
            }

            if (stream.IsWriting)
            {
                // 所有者: 状態を書き出す
                stream.SendNext(_rb.position);
                stream.SendNext(_rb.rotation);
                stream.SendNext(_rb.linearVelocity);
                stream.SendNext(_rb.angularVelocity);
            }
            else
            {
                // 受信側: 目標状態を更新
                _targetPos = (Vector3)stream.ReceiveNext();
                _targetRot = (Quaternion)stream.ReceiveNext();
                _targetVel = (Vector3)stream.ReceiveNext();
                _targetAngularVel = (Vector3)stream.ReceiveNext();
            }
        }
    }
}
