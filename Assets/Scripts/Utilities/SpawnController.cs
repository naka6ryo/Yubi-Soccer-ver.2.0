using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace YubiSoccer.Utilities
{
    /// <summary>
    /// Prefab の生成／破棄を Inspector や UI ボタンから制御するユーティリティ。
    /// - Inspector で生成したい Prefab と Spawn Point (Transform) を割り当てます。
    /// - Button の OnClick に Spawn/Despawn/Toggle/Respawn を割り当てるだけで操作できます。
    /// - Spawn を呼ぶとインスタンスを生成して保持（keepInstance=true の場合）。Despawn は破棄（または失活）します。
    /// - ネットワーク同期（Photon 等）は扱いません。ネットワーク上で同期したい場合は PhotonNetwork.Instantiate/Destroy を使う別実装が必要です。
    ///
    /// 例：
    ///  - ボタン A に SpawnController.Spawn を割り当てる
    ///  - ボタン B に SpawnController.Despawn を割り当てる
    ///  - ボタン C に SpawnController.Toggle を割り当てる
    /// </summary>
    [DisallowMultipleComponent]
    public class SpawnController : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("生成する Prefab（GameObject）")]
        public GameObject prefab;

        [Tooltip("生成位置（未設定の場合は this.transform の位置に生成）")]
        public Transform spawnPoint;

        [Tooltip("生成したインスタンスを保持するか（true の場合、既に生成済みなら再生成しません）")]
        public bool keepInstance = true;

        [Tooltip("起動時に自動で Spawn を実行するか")]
        public bool spawnOnStart = false;

        [Header("Auto Respawn")]
        [Tooltip("Despawn 後に自動で再生成する遅延時間（秒）。0 は自動再生成なし。")]
        public float autoRespawnDelay = 0f;

        [Header("Events")]
        public UnityEvent onSpawned;
        public UnityEvent onDespawned;

        // 生成されたインスタンス参照
        private GameObject _instance;

        void Start()
        {
            if (spawnOnStart)
            {
                Spawn();
            }
        }

        /// <summary>
        /// インスタンスが存在するか
        /// </summary>
        public bool IsSpawned() => _instance != null;

        /// <summary>
        /// 生成済みインスタンスを返します（存在しなければ null）
        /// </summary>
        public GameObject GetInstance() => _instance;

        /// <summary>
        /// Prefab を生成します。既にインスタンスがあり keepInstance=true の場合は何もしません。
        /// </summary>
        public GameObject Spawn()
        {
            if (prefab == null)
            {
                Debug.LogWarning("[SpawnController] prefab is not assigned.");
                return null;
            }

            if (keepInstance && _instance != null)
            {
                return _instance;
            }

            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            var go = Instantiate(prefab, pos, rot);
            _instance = go;

            onSpawned?.Invoke();
            Debug.Log("[SpawnController] Spawned: " + prefab.name);
            return go;
        }

        /// <summary>
        /// 生成物を破棄（完全削除）。autoRespawnDelay が設定されていれば再生成をスケジュールします。
        /// </summary>
        public void Despawn()
        {
            if (_instance == null)
            {
                return;
            }

            Destroy(_instance);
            _instance = null;
            onDespawned?.Invoke();
            Debug.Log("[SpawnController] Despawned");

            if (autoRespawnDelay > 0f)
            {
                StartCoroutine(CoRespawnAfter(autoRespawnDelay));
            }
        }

        /// <summary>
        /// 既にあれば消す、なければ生成するトグル
        /// </summary>
        public void Toggle()
        {
            if (IsSpawned()) Despawn();
            else Spawn();
        }

        /// <summary>
        /// Despawn してから指定秒後に Spawn します（現在のインスタンスが存在するなら先に破棄します）
        /// </summary>
        public void Respawn(float delaySeconds)
        {
            if (_instance != null)
            {
                Destroy(_instance);
                _instance = null;
            }
            StartCoroutine(CoRespawnAfter(delaySeconds));
        }

        private IEnumerator CoRespawnAfter(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            Spawn();
        }
    }
}
