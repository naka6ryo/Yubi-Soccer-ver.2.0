using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

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
        [Header("Camera Switch Settings")]
        [Tooltip("Player Camera to watch for activation. If null the script will try to find 'PlayerCamera' by name or by Player tag.")]
        public Camera playerCamera;

        [Tooltip("Only run the kinematic-off action once after the first camera switch (recommended).")]
        public bool runOnce = true;

        // internal state
        private bool _hasRun = false;
        private bool _prevPlayerCameraActive = false;

        void Awake()
        {
            if (playerCamera == null)
            {
                var go = GameObject.Find("PlayerCamera");
                if (go != null) playerCamera = go.GetComponent<Camera>();

                if (playerCamera == null)
                {
                    var byTag = GameObject.FindWithTag("Player");
                    if (byTag != null)
                    {
                        var cam = byTag.GetComponentInChildren<Camera>(true);
                        if (cam != null) playerCamera = cam;
                    }
                }
            }

            _prevPlayerCameraActive = IsPlayerCameraActive();
        }

        void Update()
        {
            if (_hasRun && runOnce) return;

            bool isActive = IsPlayerCameraActive();
            // detect transition from inactive -> active
            if (!_prevPlayerCameraActive && isActive)
            {
                // PlayerCamera became active: perform kinematic-off on soccer ball(s)
                DisableKinematicOnSceneBalls();
                _hasRun = true;
                if (runOnce) enabled = false; // stop updating
            }
            _prevPlayerCameraActive = isActive;
        }

        private bool IsPlayerCameraActive()
        {
            if (playerCamera == null) return false;
            try
            {
                return playerCamera.gameObject.activeInHierarchy && playerCamera.enabled;
            }
            catch { return false; }
        }

        /// <summary>
        /// Find soccer ball(s) in the current scene and set their Rigidbody.isKinematic = false.
        /// This method is conservative: it tries tag-based lookup first, then name-based fallback,
        /// then finally checks all Rigidbodies and picks likely candidates by name.
        /// </summary>
        private void DisableKinematicOnSceneBalls()
        {
            Debug.Log("[SpawnController] PlayerCamera activated — disabling kinematic on soccer ball(s)");
            var handled = false;

            // Try common tags first
            string[] tagsToTry = new[] { "SoccerBall", "Ball" };
            foreach (var tag in tagsToTry)
            {
                try
                {
                    var objs = GameObject.FindGameObjectsWithTag(tag);
                    if (objs != null && objs.Length > 0)
                    {
                        foreach (var o in objs)
                        {
                            TryDisableKinematic(o);
                        }
                        handled = true;
                    }
                }
                catch { /* tag may not exist; ignore */ }
            }

            if (handled) return;

            // Fallback: search by name patterns
            var candidates = new System.Collections.Generic.List<GameObject>();
            var allRoots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in allRoots)
            {
                foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    var go = rb.gameObject;
                    var name = go.name.ToLower();
                    if (name.Contains("soccer") || name.Contains("soccer ball") || name.Contains("ball"))
                    {
                        candidates.Add(go);
                    }
                }
            }

            if (candidates.Count > 0)
            {
                foreach (var g in candidates)
                {
                    TryDisableKinematic(g);
                }
                return;
            }

            // Last resort: find any Rigidbody that looks like a ball by having a SphereCollider and a reasonable mass
            foreach (var rb in Object.FindObjectsOfType<Rigidbody>())
            {
                try
                {
                    var sc = rb.GetComponent<SphereCollider>();
                    if (sc != null && rb.mass > 0f)
                    {
                        TryDisableKinematic(rb.gameObject);
                    }
                }
                catch { }
            }
        }

        private void TryDisableKinematic(GameObject go)
        {
            if (go == null) return;
            try
            {
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.WakeUp();
                    Debug.Log($"[SpawnController] Disabled kinematic on: {go.name}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[SpawnController] Failed to disable kinematic on " + go.name + ": " + ex);
            }
        }
    }
}
