using Photon.Pun;
using UnityEngine;
using System.Collections.Generic;

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

        [Header("Run Clip")]
        [Tooltip("走行時に再生するクリップ（Inspectorで割り当て）。未割当て時はグローバルな SoundManager を利用する挙動を保つ。")]
        public AudioClip runClip;
        [Tooltip("走行音の基準ボリューム (0..1)")]
        [Range(0f, 1f)] public float runVolume = 1.0f;
        [Tooltip("リモートで鳴らすときの倍率 (owner runVolume に対する乗数)")]
        [Range(0f, 1f)] public float remoteRunScale = 0.7f;

        private AudioSource spatialSource; // charge/impact 用 3D
        private AudioSource localSource;   // owner 用 2D (charge/kick)
        private AudioSource localRunSource; // owner 用の走行SE専用 2D (チャージ等と分離)
        private AudioSource remoteRunSource; // remote 用の走行SE専用 3D (charge ソースと分離)
        private PhotonView pv;

        // Object Pool for spatial clips
        private List<AudioSource> _spatialClipPool;
        private int _poolSize = 5; // プールの初期サイズ

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

            // ローカル(自分)用 2D サウンド (チャージ・キック用)
            localSource = gameObject.AddComponent<AudioSource>();
            localSource.spatialBlend = 0f; // 2D
            localSource.playOnAwake = false;
            localSource.loop = false;

            // ローカル走行音用に別の AudioSource を用意して競合を避ける
            localRunSource = gameObject.AddComponent<AudioSource>();
            localRunSource.spatialBlend = 0f; // 2D (所有者の走行音は非空間)
            localRunSource.playOnAwake = false;
            localRunSource.loop = false;

            // リモート走行音用の 3D Source を分離して、チャージループ停止等の影響を避ける
            remoteRunSource = gameObject.AddComponent<AudioSource>();
            remoteRunSource.spatialBlend = 1f;
            remoteRunSource.rolloffMode = rolloff;
            remoteRunSource.minDistance = Mathf.Max(0.01f, minDistance);
            remoteRunSource.maxDistance = Mathf.Max(remoteRunSource.minDistance + 0.01f, maxDistance);
            remoteRunSource.playOnAwake = false;
            remoteRunSource.loop = false;

            // 初期ボリューム設定を反映
            // PlayOneShot で明示的な音量を渡すようにするため、ソースの base volume は 1 に固定する
            if (localRunSource != null) localRunSource.volume = 1.0f;
            if (remoteRunSource != null) remoteRunSource.volume = 1.0f;

            // --- Object Pool 初期化 ---
            _spatialClipPool = new List<AudioSource>(_poolSize);
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"SpatialAudioSource_Pool_{i}");
                go.transform.SetParent(transform); // このオブジェクトの子として整理
                var src = go.AddComponent<AudioSource>();
                go.SetActive(false);
                _spatialClipPool.Add(src);
            }
        }

        // Editor: Inspector の値を変更したら既存の AudioSource に即時反映する
        private void OnValidate()
        {
            // Apply rolloff/min/max to any created sources
            if (spatialSource != null)
            {
                spatialSource.rolloffMode = rolloff;
                spatialSource.minDistance = Mathf.Max(0.01f, minDistance);
                spatialSource.maxDistance = Mathf.Max(spatialSource.minDistance + 0.01f, maxDistance);
            }
            if (remoteRunSource != null)
            {
                remoteRunSource.rolloffMode = rolloff;
                remoteRunSource.minDistance = Mathf.Max(0.01f, minDistance);
                remoteRunSource.maxDistance = Mathf.Max(remoteRunSource.minDistance + 0.01f, maxDistance);
                // base volume remains 1.0f; effective loudness is passed via PlayOneShot multiplier
                remoteRunSource.volume = 1.0f;
            }
            if (localRunSource != null)
            {
                localRunSource.volume = 1.0f;
            }
            if (localSource != null)
            {
                localSource.volume = Mathf.Clamp01(localChargeVolume);
            }
        }

        // --- Owner側の API ---
        public void StartChargingLocal()
        {
            if (chargeLoopClip == null) return;
            // Ensure the loop clip is assigned and start playback unconditionally.
            // PlayOneShot (used for footsteps/kick) can set isPlaying temporarily, which
            // would prevent the loop from starting if we only play when not already playing.
            if (localSource.clip != chargeLoopClip) localSource.clip = chargeLoopClip;
            localSource.loop = true;
            localSource.volume = localChargeVolume;
            localSource.pitch = minPitch;
            // Force playback of the loop clip so it reliably starts even if a one-shot
            // was playing on this source a moment before.
            localSource.Stop();
            localSource.Play();
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

        // --- Run playback API ---
        /// <summary>
        /// 所有者側で走行時に一回だけ再生する（非空間 / 2D）。
        /// PlayerController から定期的に呼び出される想定。
        /// </summary>
        public void PlayRunLocalOneShot()
        {
            if (runClip == null) return;
            if (localRunSource != null)
            {
                // 明示的に所有者の基準ボリュームを渡す
                localRunSource.PlayOneShot(runClip, Mathf.Clamp01(runVolume));
            }
        }

        /// <summary>
        /// リモート側で走行を一回だけ空間再生する（3D）。PlayerController のリモート分岐から呼ぶ想定。
        /// </summary>
        public void PlayRunRemoteOneShot()
        {
            if (runClip == null) return;
            if (remoteRunSource != null)
            {
                // 明示的にリモート用の音量を渡す (owner の runVolume に remoteRunScale を乗算)
                float vol = Mathf.Clamp01(runVolume * remoteRunScale);
                remoteRunSource.PlayOneShot(runClip, vol);
            }
        }

        /// <summary>
        /// リモートのキックを、このプレイヤーの位置に結び付けて再生する（チャージループを停止してから）。
        /// 所有者でないクライアント上で呼ぶことを想定。
        /// </summary>
        public void PlayKickRemote(bool strong)
        {
            if (kickClip == null) return;

            // チャージループが再生中なら止めてからキックSEを鳴らす（ローカルと同様の遷移）
            if (spatialSource != null && spatialSource.isPlaying && spatialSource.clip == chargeLoopClip)
            {
                spatialSource.Stop();
            }

            // spatialSource はこの GameObject に紐づくため、位置感は自動で反映される
            if (spatialSource != null)
            {
                spatialSource.PlayOneShot(kickClip, Mathf.Clamp01(remoteMaxVolume));
            }
        }

        // Remote kick playback: 他クライアントのキックSEを位置付きで鳴らす
        /// <summary>
        /// リモートのキックをこのクライアント上で位置付きで再生する。
        /// 所有者が送った RPC で呼び出される想定。
        /// </summary>
        public void PlayKickRemoteAt(Vector3 worldPosition, bool strong, float charge01 = 0f)
        {
            if (kickClip == null) return;

            // 通常・強キックともリモートでは同じ最大音量で再生する
            float vol = Mathf.Clamp01(remoteMaxVolume);

            // 一時的な AudioSource を生成して位置で再生（距離減衰と方向感を担保）
            StartCoroutine(PlaySpatialClipAtPoint(kickClip, worldPosition, vol));
        }

        private System.Collections.IEnumerator PlaySpatialClipAtPoint(AudioClip clip, Vector3 pos, float volume)
        {
            AudioSource source = null;
            // プールから利用可能なソースを探す
            foreach (var src in _spatialClipPool)
            {
                if (src != null && !src.gameObject.activeSelf)
                {
                    source = src;
                    break;
                }
            }

            // プールに空きがない場合は何もしない（または、新規作成も可能だが、GCを避けるためここでは何もしない）
            if (source == null)
            {
                yield break;
            }

            source.transform.position = pos;
            source.spatialBlend = 1f;
            source.rolloffMode = rolloff;
            source.minDistance = Mathf.Max(0.01f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, maxDistance);
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.gameObject.SetActive(true);
            source.Play();

            float waitTime = clip != null ? clip.length + 0.1f : 1f;
            yield return new WaitForSeconds(waitTime); // 可変時間のため、ここではnewを許容

            if (source != null)
            {
                source.gameObject.SetActive(false);
            }
        }
    }
}

