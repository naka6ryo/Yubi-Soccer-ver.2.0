using Photon.Pun;
using UnityEngine;

namespace YubiSoccer.Player
{
    /// <summary>
    /// プレイヤー単位の音再生コントローラ。
    /// - 所有者(自分)は 2D (non-spatial) ソースでチャージ音を大きく再生する。
    /// - リモートは 3D(spatial) ソースで距離減衰する音を再生する。
    /// - キック音は所有者とリモートで使い分ける（リモートは最大音量を抑える）。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAudioController : MonoBehaviour
    {
        [Header("Clips")]
        public AudioClip chargeLoopClip;
        public AudioClip kickClip;

        [Header("Volume / Attenuation")]
        [Tooltip("リモートプレイヤーの最大ボリューム乗数 (0..1)")]
        [Range(0f, 1f)] public float remoteMaxVolume = 0.7f;
        [Tooltip("自分のチャージ音のボリューム乗数 (2D) 自分の音は常にこれを最大全開にできる")]
        [Range(0f, 2f)] public float localChargeVolume = 1.0f;

        [Header("3D Rolloff")]
        public float minDistance = 1f;
        public float maxDistance = 12f;
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;

        [Header("Pitch by charge")]
        public float minPitch = 0.95f;
        public float maxPitch = 1.25f;

        private AudioSource spatialSource; // remote 用 3D
        private AudioSource localSource;   // owner 用 2D
        private PhotonView pv;

        void Awake()
        {
            pv = GetComponentInParent<PhotonView>();

            // 3D ソース (spatial) - 常に作る
            spatialSource = gameObject.AddComponent<AudioSource>();
            spatialSource.spatialBlend = 1f;
            spatialSource.rolloffMode = rolloff;
            spatialSource.minDistance = Mathf.Max(0.01f, minDistance);
            spatialSource.maxDistance = Mathf.Max(spatialSource.minDistance + 0.01f, maxDistance);
            spatialSource.playOnAwake = false;
            spatialSource.loop = false; // チャージループは Play/Stop で管理

            // ローカル(自分)用 2D サウンド
            localSource = gameObject.AddComponent<AudioSource>();
            localSource.spatialBlend = 0f; // 2D
            localSource.playOnAwake = false;
            localSource.loop = false;
        }

        // --- Owner側の API ---
        public void StartChargingLocal()
        {
            if (chargeLoopClip == null) return;
            if (localSource.clip != chargeLoopClip) localSource.clip = chargeLoopClip;
            localSource.loop = true;
            localSource.volume = localChargeVolume;
            localSource.pitch = minPitch;
            if (!localSource.isPlaying) localSource.Play();
        }

        public void StopChargingLocal()
        {
            if (localSource != null && localSource.isPlaying) localSource.Stop();
        }

        public void PlayKickLocal(bool strong)
        {
            if (kickClip != null)
            {
                float vol = Mathf.Clamp01(localChargeVolume);
                localSource.PlayOneShot(kickClip, vol);
            }
        }

        // --- Remote側の API (spatial) ---
        public void SetRemoteCharge(float charge01)
        {
            if (chargeLoopClip == null) return;
            float clamped = Mathf.Clamp01(charge01);
            if (clamped <= 0.01f)
            {
                if (spatialSource.isPlaying && spatialSource.clip == chargeLoopClip)
                    spatialSource.Stop();
                return;
            }

            if (spatialSource.clip != chargeLoopClip) spatialSource.clip = chargeLoopClip;
            spatialSource.loop = true;
            spatialSource.pitch = Mathf.Lerp(minPitch, maxPitch, clamped);
            spatialSource.volume = Mathf.Clamp01(clamped) * remoteMaxVolume;
            if (!spatialSource.isPlaying) spatialSource.Play();
        }

        public void StopRemoteCharge()
        {
            if (spatialSource != null && spatialSource.isPlaying && spatialSource.clip == chargeLoopClip)
                spatialSource.Stop();
        }

        // Note: remote kick playback removed — remote clients no longer play kick SE.
    }
}
