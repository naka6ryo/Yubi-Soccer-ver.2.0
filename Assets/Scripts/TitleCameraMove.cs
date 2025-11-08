// CM2_SwitchNearEndSmooth.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.SceneManagement;
using System.Reflection;
using Photon.Pun;
using YubiSoccer;

public class CM2_SwitchNearEndSmooth : MonoBehaviour
{
    [Header("0=1番, 1=2番, 2=3番")]
    public List<CinemachineVirtualCamera> cams = new List<CinemachineVirtualCamera>(3);

    [Header("Priority")]
    public int lowPriority = 10;
    public int highPriority = 20;

    [Header("トリガー時期（“直前”の定義）")]
    [Tooltip("1→2ブレンドの進捗(0〜1)。これを超えたら 3 へ切替。例: 0.85〜0.95")]
    [Range(0f, 1f)] public float progressThreshold = 0.90f;

    [Header("2→3 のブレンド設定（瞬間だけ適用）")]
    public float twoToThreeBlendTime = 0.25f; // 0.15〜0.4 あたりが“止まらず滑らか”
    public CinemachineBlendDefinition.Style twoToThreeStyle =
        CinemachineBlendDefinition.Style.EaseInOut;

    [Header("参照（未設定なら自動取得）")]
    public CinemachineBrain brain;

    [Header("Initial Camera Selection")]
    [Tooltip("true の場合、まずオブジェクト名でカメラを検索して CinemachineBrain を取得します（Inspector で名前を指定）。")]
    public bool preferNamedInitialCamera = true;
    [Tooltip("優先して探すオブジェクト名（デフォルト: Camera）")]
    public string preferredInitialCameraName = "Camera";
    [Tooltip("true の場合、preferredInitialCameraName のロジックは targetSceneName に指定したシーン名のときだけ適用されます。空の場合は常に適用されます。")]
    public bool applyNamedPreferenceOnlyForScene = false;
    [Tooltip("applyNamedPreferenceOnlyForScene が true のときに適用するシーン名（Build Settings のシーン名）")]
    public string targetSceneName = "";
    [Tooltip("Inspector で直接割り当てできる初期カメラ。設定されている場合はこれを優先して使います（オブジェクト検索は行いません）。")]
    public Camera initialCamera;
    [Header("Player Camera Startup Behavior")]
    [Tooltip("true の場合、シーケンス完了まで playerCamera を無効化しておきます（起動時にプレイヤーカメラが表示されないようにする）。")]
    public bool deactivatePlayerCameraAtStart = true;

    // internal: whether we explicitly deactivated the player camera so we know to enable later
    private bool _playerCameraWasDeactivated = false;

    void Awake()
    {
        if (deactivatePlayerCameraAtStart && switchToPlayerCameraOnComplete)
        {
            // try find playerCamera if not assigned
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

            if (playerCamera != null)
            {
                // only deactivate if currently active
                if (playerCamera.gameObject.activeSelf || playerCamera.enabled)
                {
                    playerCamera.enabled = false;
                    playerCamera.gameObject.SetActive(false);
                    _playerCameraWasDeactivated = true;
                    Debug.Log("[TitleCameraMove] PlayerCamera deactivated at start to allow title sequence to show.");
                }
            }
        }
    }

    [Header("Finish Behavior")]
    [Tooltip("シーケンス完了後に PlayerCamera に切り替えるか。PlayerCamera は Inspector で割当可能。未設定なら名前 \"PlayerCamera\" の GameObject を探します。")]
    public bool switchToPlayerCameraOnComplete = true;
    [Tooltip("切り替え先の Camera（未設定ならシーン内で 'PlayerCamera' という名前の GameObject を探します）")]
    public Camera playerCamera;

    bool _running;
    // Tracks cameras we disabled when switching to player camera so we can safely re-enable them later
    private System.Collections.Generic.List<Camera> _disabledByPlayerSwitch = new System.Collections.Generic.List<Camera>();

    /// <summary>UIボタンの OnClick から呼ぶ</summary>
    public void Play()
    {
        if (!_running) StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        _running = true;

        if (cams.Count < 3 || cams[0] == null || cams[1] == null || cams[2] == null)
        { Debug.LogWarning("cams[0..2] に vcam を割り当ててください。"); _running = false; yield break; }

        if (brain == null)
        {
            // Optionally prefer a camera found by object name in this scene
            bool useNamed = preferNamedInitialCamera;
            if (applyNamedPreferenceOnlyForScene && !string.IsNullOrEmpty(targetSceneName))
            {
                useNamed = useNamed && (SceneManager.GetActiveScene().name == targetSceneName);
            }

            // If an explicit initialCamera is provided via the inspector, prefer it
            if (initialCamera != null)
            {
                brain = initialCamera.GetComponent<CinemachineBrain>() ?? initialCamera.GetComponentInChildren<CinemachineBrain>();
            }
            else if (useNamed && !string.IsNullOrEmpty(preferredInitialCameraName))
            {
                var go = GameObject.Find(preferredInitialCameraName);
                if (go != null)
                {
                    // Try to get CinemachineBrain on that GameObject or its children
                    brain = go.GetComponent<CinemachineBrain>() ?? go.GetComponentInChildren<CinemachineBrain>();
                    if (brain == null)
                    {
                        // If it has a Camera but no brain, try to get brain from that camera
                        var cam = go.GetComponent<Camera>() ?? go.GetComponentInChildren<Camera>();
                        if (cam != null) brain = cam.GetComponent<CinemachineBrain>();
                    }
                }
            }

            // fallback to Camera.main
            if (brain == null) brain = Camera.main ? Camera.main.GetComponent<CinemachineBrain>() : null;
        }
        if (brain == null) { Debug.LogWarning("CinemachineBrain が見つかりません。"); _running = false; yield break; }

        // まず 2 を最優先にして 1→2 ブレンド開始
        SetHighest(1);

        // ブレンド開始を待機
        float t = 0f, safety = 3f;
        while ((brain.ActiveBlend == null || !brain.ActiveBlend.IsValid) && t < safety)
        { t += Time.deltaTime; yield return null; }

        // “直前”で 3 へ切替（その瞬間だけ 2→3 のブレンド時間を短く強制）
        if (brain.ActiveBlend != null && brain.ActiveBlend.IsValid)
        {
            while (true)
            {
                var b = brain.ActiveBlend;
                if (b == null || !b.IsValid) break;

                float progress = (b.Duration > 0f) ? (b.TimeInBlend / b.Duration) : 1f;
                if (progress >= progressThreshold)
                {
                    // 一時的に DefaultBlend を置き換え
                    var old = brain.m_DefaultBlend;
                    brain.m_DefaultBlend = new CinemachineBlendDefinition(twoToThreeStyle, twoToThreeBlendTime);

                    SetHighest(2); // 3 を最優先 → 2→3 ブレンドがすぐ始まる

                    // 指定時間＋少し待ってから元のブレンド設定に戻す
                    yield return new WaitForSeconds(twoToThreeBlendTime + 0.05f);
                    brain.m_DefaultBlend = old;
                    break;
                }
                yield return null;
            }
        }
        else
        {
            // Cut などでブレンドが検出できなかったときのフォールバック
            var old = brain.m_DefaultBlend;
            brain.m_DefaultBlend = new CinemachineBlendDefinition(twoToThreeStyle, twoToThreeBlendTime);
            SetHighest(2);
            yield return new WaitForSeconds(twoToThreeBlendTime + 0.05f);
            brain.m_DefaultBlend = old;
        }

        _running = false;
        // シーケンス終了時にプレイヤーカメラへ切り替える
        if (switchToPlayerCameraOnComplete)
        {
            TrySwitchToPlayerCamera();
        }
    }

    private void TrySwitchToPlayerCamera()
    {
        Debug.Log("[TitleCameraMove] TrySwitchToPlayerCamera invoked");

        // Ensure we have a valid playerCamera reference; try multiple fallbacks
        if (playerCamera == null)
        {
            Debug.Log("[TitleCameraMove] playerCamera field is null, attempting to find by name/tag/main camera...");
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
            if (playerCamera == null && Camera.main != null)
            {
                Debug.Log("[TitleCameraMove] Falling back to Camera.main");
                playerCamera = Camera.main;
            }
        }

        if (playerCamera == null)
        {
            Debug.LogWarning("[TitleCameraMove] PlayerCamera not found; cannot switch.");
            return;
        }

        Debug.Log($"[TitleCameraMove] Enabling PlayerCamera (wasActive={playerCamera.gameObject.activeInHierarchy}, enabled={playerCamera.enabled}) name={playerCamera.name}");

        // Disable Cinemachine brain so the player camera can render directly
        if (brain != null)
        {
            try
            {
                brain.enabled = false;
                Debug.Log("[TitleCameraMove] CinemachineBrain disabled to allow PlayerCamera to render");
            }
            catch { Debug.LogWarning("[TitleCameraMove] Failed to disable CinemachineBrain"); }
        }

        // Activate player camera
        try
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;
            _playerCameraWasDeactivated = false;
            Debug.Log("[TitleCameraMove] Switched to PlayerCamera: " + playerCamera.name);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[TitleCameraMove] Failed to activate PlayerCamera: " + ex);
        }

        // Ensure no other Camera is still rendering on top of the player camera.
        try
        {
            // Find all cameras (including inactive) and canvases to avoid disabling UI render cameras
            var camsAll = Object.FindObjectsOfType<Camera>(true);
            var canvases = Object.FindObjectsOfType<Canvas>(true);
            float maxDepth = float.MinValue;
            foreach (var c in camsAll)
            {
                if (c == null) continue;
                if (c == playerCamera) continue;

                // Skip cameras that are explicitly used by any Canvas (ScreenSpace - Camera)
                bool usedByCanvas = false;
                foreach (var canvas in canvases)
                {
                    try
                    {
                        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == c)
                        {
                            usedByCanvas = true;
                            break;
                        }
                    }
                    catch { }
                }
                if (usedByCanvas)
                {
                    Debug.Log($"[TitleCameraMove] Skipping disabling camera used by Canvas: {c.name}");
                    if (c.depth > maxDepth) maxDepth = c.depth;
                    continue;
                }

                // disable camera and record it to re-enable later
                Debug.Log($"[TitleCameraMove] Disabling other Camera: {c.name} (enabled={c.enabled}, depth={c.depth})");
                try
                {
                    if (!_disabledByPlayerSwitch.Contains(c)) _disabledByPlayerSwitch.Add(c);
                    c.enabled = false;
                }
                catch { }
                if (c.depth > maxDepth) maxDepth = c.depth;
            }

            // Bring player camera to front by increasing its depth if needed
            if (playerCamera != null)
            {
                float desired = maxDepth + 1f;
                if (playerCamera.depth < desired)
                {
                    Debug.Log($"[TitleCameraMove] Raising PlayerCamera depth from {playerCamera.depth} to {desired}");
                    playerCamera.depth = desired;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[TitleCameraMove] Error while disabling other cameras: " + ex);
        }

        // NOTE: ball spawn logic removed. This method now only activates the player camera.
    }

    private void TrySpawnBallFallback()
    {
        const string defaultPrefabName = "Soccer Ball";
        var prefab = Resources.Load<GameObject>(defaultPrefabName);
        if (prefab == null)
        {
            Debug.LogWarning($"[TitleCameraMove] Fallback prefab '{defaultPrefabName}' not found in Resources. Cannot spawn ball.");
            return;
        }

        // spawn position - reuse same default coordinates used elsewhere
        var spawnPos = new Vector3(1826.69f, 12.95f, 1821.66f);

        GameObject inst = null;
        try
        {
            if (Photon.Pun.PhotonNetwork.IsConnected && Photon.Pun.PhotonNetwork.InRoom)
            {
                if (Photon.Pun.PhotonNetwork.IsMasterClient)
                {
                    inst = Photon.Pun.PhotonNetwork.Instantiate(defaultPrefabName, spawnPos, Quaternion.identity);
                    Debug.Log("[TitleCameraMove] Fallback: PhotonNetwork.Instantiate used to spawn ball.");
                }
                else
                {
                    Debug.Log("[TitleCameraMove] Fallback: Not MasterClient, skipping network instantiate.");
                }
            }
            else
            {
                inst = Instantiate(prefab, spawnPos, Quaternion.identity);
                Debug.Log("[TitleCameraMove] Fallback: Local Instantiate used to spawn ball.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[TitleCameraMove] Fallback spawn failed: " + ex);
        }

        if (inst != null)
        {
            // Get Rigidbody once and reuse
            Rigidbody rbRef = null;
            try
            {
                rbRef = inst.GetComponent<Rigidbody>();
                // If this was a local Instantiate (not PhotonNetwork.Instantiate), the BallNetworkSync
                // component may treat this object as a non-owner and continuously zero velocities
                // (followVelocities). When Photon is not connected, disable the network sync so
                // physics runs normally.
                try
                {
                    if (!Photon.Pun.PhotonNetwork.IsConnected)
                    {
                        var bns = inst.GetComponent<YubiSoccer.Network.BallNetworkSync>();
                        if (bns != null)
                        {
                            bns.enabled = false;
                            Debug.Log("[TitleCameraMove] Disabled BallNetworkSync on locally-instantiated ball to allow physics.");
                        }
                    }
                }
                catch { }
                if (rbRef != null)
                {
                    // Force physics-on state to avoid runtime scripts or networking components leaving it kinematic
                    rbRef.isKinematic = false;
                    rbRef.useGravity = true;
                    rbRef.constraints = RigidbodyConstraints.None;
                    rbRef.WakeUp();
                    Debug.Log($"[TitleCameraMove] Spawned ball Rigidbody state: isKinematic={rbRef.isKinematic}, useGravity={rbRef.useGravity}, velocity={rbRef.linearVelocity}");
                }
                else
                {
                    Debug.LogWarning("[TitleCameraMove] Spawned ball has no Rigidbody component.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[TitleCameraMove] Error while enforcing Rigidbody settings on spawned ball: " + ex);
            }

            // Register with systems similar to SoccerBallCreator
            try
            {
                var glassType = typeof(YubiSoccer.Environment.BreakableProximityGlass);
                var rm = inst.transform;
                YubiSoccer.Environment.BreakableProximityGlass.RegisterBallForAll(rm);
            }
            catch { }
            try { YubiSoccer.UI.BallOffScreenIndicator.RegisterBallForAll(inst.transform); } catch { }
            try
            {
                var goalResetManager = Object.FindObjectOfType<YubiSoccer.Game.GoalResetManager>();
                if (goalResetManager != null && rbRef != null)
                {
                    goalResetManager.RegisterBall(rbRef);
                }
            }
            catch { }

            // Workaround: some components may overwrite Rigidbody state on their Enable/Start.
            // Re-apply desired physics settings for a few FixedUpdate frames to override later changes.
            if (rbRef != null)
            {
                StartCoroutine(EnforcePhysicsForFrames(rbRef, 30)); // ~0.5s at 60Hz
            }
        }
    }

    private IEnumerator EnforcePhysicsForFrames(Rigidbody rb, int frames)
    {
        if (rb == null || frames <= 0) yield break;
        int i = 0;
        while (i < frames)
        {
            try
            {
                if (rb == null) yield break;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.None;
                rb.WakeUp();
                // Log only occasionally to avoid spamming
                if (i == 0 || i == frames - 1)
                {
                    Debug.Log($"[TitleCameraMove] EnforcePhysicsForFrames #{i} on {rb.name}: isKinematic={rb.isKinematic}, useGravity={rb.useGravity}, pos={rb.position}");
                }
            }
            catch { }
            i++;
            yield return new WaitForFixedUpdate();
        }
    }

    void SetHighest(int index)
    {
        for (int i = 0; i < cams.Count; i++)
        {
            var v = cams[i];
            if (v == null) continue;
            v.Priority = (i == index) ? highPriority : lowPriority;
        }
    }

    /// <summary>
    /// Public API: called by UI Home button to smoothly return from PlayerCamera to the title camera.
    /// This temporarily overrides the default blend with the configured twoToThreeBlendTime/style
    /// and sets the title camera (index 0) to highest priority, restoring the old blend after the transition.
    /// </summary>
    public void OnHomeButton()
    {
        Debug.Log("[TitleCameraMove] OnHomeButton invoked");
        StartCoroutine(CoSwitchToTitle());
    }

    private IEnumerator CoSwitchToTitle()
    {
        Debug.Log("[TitleCameraMove] CoSwitchToTitle start");
        if (cams == null || cams.Count == 0)
        {
            // try to auto-find virtual cameras if none assigned
            var found = Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>();
            if (found != null && found.Length > 0)
            {
                cams = new System.Collections.Generic.List<Cinemachine.CinemachineVirtualCamera>(found);
                Debug.LogWarning($"[TitleCameraMove] cams was empty — auto-populated with {found.Length} virtual cameras. Ensure order is correct in inspector.");
            }
            else
            {
                Debug.LogWarning("[TitleCameraMove] No virtual cameras assigned and none found in scene; aborting Home switch.");
                yield break;
            }
        }

        // log cams contents
        try
        {
            for (int i = 0; i < cams.Count; i++)
            {
                var n = cams[i] != null ? cams[i].name : "<null>";
                Debug.Log($"[TitleCameraMove] cams[{i}] = {n}, priority={ (cams[i]!=null?cams[i].Priority:0) }");
            }
        }
        catch { }

        if (brain == null)
        {
            brain = Camera.main ? Camera.main.GetComponent<Cinemachine.CinemachineBrain>() : null;
            if (brain == null)
            {
                // fallback: find any CinemachineBrain in scene
                brain = Object.FindObjectOfType<Cinemachine.CinemachineBrain>();
            }
            if (brain == null)
            {
                Debug.LogWarning("[TitleCameraMove] No CinemachineBrain found; cannot switch to title camera smoothly.");
                yield break;
            }
        }

        // If PlayerCamera was active and brain was disabled, re-enable brain and deactivate PlayerCamera
        try
        {
            if (playerCamera != null && (playerCamera.gameObject.activeInHierarchy || playerCamera.enabled))
            {
                Debug.Log("[TitleCameraMove] Deactivating PlayerCamera so Cinemachine brain can take control");
                playerCamera.enabled = false;
                playerCamera.gameObject.SetActive(false);
            }
        }
        catch { }

        // Ensure brain is enabled so priority changes take effect
        try
        {
            if (brain != null && !brain.enabled)
            {
                brain.enabled = true;
                Debug.Log("[TitleCameraMove] Re-enabled CinemachineBrain");
            }
        }
        catch { }

        // Re-enable any Cameras that we explicitly disabled when switching to the player camera.
        // Also ensure Cinemachine virtual cameras are active so brain can blend between them.
        try
        {
            foreach (var c in _disabledByPlayerSwitch)
            {
                if (c == null) continue;
                try
                {
                    c.enabled = true;
                    Debug.Log($"[TitleCameraMove] Re-enabled Camera (was disabled by player switch): {c.name}");
                }
                catch { }
            }
            _disabledByPlayerSwitch.Clear();

            if (cams != null)
            {
                foreach (var v in cams)
                {
                    if (v == null) continue;
                    if (!v.gameObject.activeInHierarchy)
                    {
                        v.gameObject.SetActive(true);
                        Debug.Log($"[TitleCameraMove] Re-activated vcam GameObject: {v.name}");
                    }
                    if (!v.enabled)
                    {
                        v.enabled = true;
                        Debug.Log($"[TitleCameraMove] Enabled vcam component: {v.name}");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[TitleCameraMove] Error re-enabling cameras/vcams: " + ex);
        }

        // Before switching cameras: invert UIInteractableSwitchers and swap fade durations on NetworkSceneTransition
        // We'll restore these after the camera sequence completes.
        var restoredTransitions = new System.Collections.Generic.List<System.Tuple<object, float, float>>();
        var modifiedSwitchers = new System.Collections.Generic.List<System.Tuple<object, bool>>();
        try
        {
            // Invert UIInteractableSwitcher state via reflection (AreTargetsInteractable is private)
            var switcherType = typeof(YubiSoccer.UI.UIInteractableSwitcher);
            var areMethod = switcherType.GetMethod("AreTargetsInteractable", BindingFlags.NonPublic | BindingFlags.Instance);
            var setMethod = switcherType.GetMethod("SetEnabled", BindingFlags.Public | BindingFlags.Instance);
            if (areMethod != null && setMethod != null)
            {
                var allSwitchers = Object.FindObjectsOfType<YubiSoccer.UI.UIInteractableSwitcher>();
                foreach (var s in allSwitchers)
                {
                    try
                    {
                        bool cur = (bool)areMethod.Invoke(s, null);
                        // store to restore later
                        modifiedSwitchers.Add(System.Tuple.Create((object)s, cur));
                        // set opposite
                        setMethod.Invoke(s, new object[] { !cur });
                        Debug.Log($"[TitleCameraMove] Inverted UIInteractableSwitcher {s.name}: {cur} -> {!cur}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("[TitleCameraMove] Failed to invert UIInteractableSwitcher: " + ex);
                    }
                }
            }

            // Swap fade durations on NetworkSceneTransition instances (private serialized fields)
            var transType = typeof(YubiSoccer.UI.NetworkSceneTransition);
            if (transType != null)
            {
                var finField = transType.GetField("fadeInDuration", BindingFlags.NonPublic | BindingFlags.Instance);
                var foutField = transType.GetField("fadeOutDuration", BindingFlags.NonPublic | BindingFlags.Instance);
                if (finField != null && foutField != null)
                {
                    var allTrans = Object.FindObjectsOfType<YubiSoccer.UI.NetworkSceneTransition>();
                    foreach (var t in allTrans)
                    {
                        try
                        {
                            float inVal = (float)finField.GetValue(t);
                            float outVal = (float)foutField.GetValue(t);
                            // store original to restore later
                            restoredTransitions.Add(System.Tuple.Create((object)t, inVal, outVal));
                            // swap
                            finField.SetValue(t, outVal);
                            foutField.SetValue(t, inVal);
                            Debug.Log($"[TitleCameraMove] Swapped NetworkSceneTransition fade durations on {t.name}: in={inVal}<->{outVal}");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning("[TitleCameraMove] Failed to swap fade durations: " + ex);
                        }
                    }
                }
            }
        }
        catch { }

        // Temporarily override default blend
        var old = brain.m_DefaultBlend;
        Debug.Log($"[TitleCameraMove] Overriding brain.m_DefaultBlend for {twoToThreeBlendTime}s (style={twoToThreeStyle})");
        brain.m_DefaultBlend = new CinemachineBlendDefinition(twoToThreeStyle, twoToThreeBlendTime);

        // If we have 3 cameras, perform reverse smooth switch: 3 -> 2 -> 1 (indices 2 -> 1 -> 0)
        if (cams.Count >= 3)
        {
            // Step: cams[2]
            Debug.Log("[TitleCameraMove] Step to cams[2]");
            SetHighest(2);
            // Wait for the blend to actually start toward cams[2], then chain quickly to avoid pauses
            yield return StartCoroutine(WaitForBlendStartedOrTimeout(2, Mathf.Max(0.25f, twoToThreeBlendTime * 0.5f)));

            // Step: cams[1]
            Debug.Log("[TitleCameraMove] Step to cams[1]");
            SetHighest(1);
            // Wait for blend toward cams[1] to start, then immediately chain to final. This reduces
            // visible pauses vs waiting for near-completion of the intermediate blend.
            yield return StartCoroutine(WaitForBlendStartedOrTimeout(1, Mathf.Max(0.25f, twoToThreeBlendTime * 0.5f)));

            // Final: cams[0]
            Debug.Log("[TitleCameraMove] Step to cams[0]");
            SetHighest(0);
            // For the final camera, wait until the blend completes (or times out) so rendering is stable
            yield return StartCoroutine(WaitForTargetOrTimeout(0, Mathf.Max(0.5f, twoToThreeBlendTime * 2f)));
        }
        else
        {
            // Fallback: directly set title camera
            Debug.Log("[TitleCameraMove] Not enough cams for stepwise return; setting cams[0] directly");
            SetHighest(0);
            yield return new WaitForSeconds(twoToThreeBlendTime + 0.05f);
        }

        // Restore original blend
        brain.m_DefaultBlend = old;
        Debug.Log("[TitleCameraMove] Restored brain.m_DefaultBlend and finished CoSwitchToTitle");

        // NOTE: Intentionally NOT restoring UIInteractableSwitcher or NetworkSceneTransition
        // states here. The user's request is to keep the inverted UI and swapped fade durations
        // after Home-button action, so we persist the modified state.
    }

    /// <summary>
    /// Wait until the CinemachineBrain reports the target virtual camera as active or until timeout.
    /// This attempts to avoid fixed WaitForSeconds and instead waits for the blend / activation to settle,
    /// which prevents visible "gaps" between staged priority changes.
    /// </summary>
    private IEnumerator WaitForTargetOrTimeout(int targetIndex, float timeout)
    {
        if (brain == null || cams == null || targetIndex < 0 || targetIndex >= cams.Count)
        {
            yield break;
        }

        var target = cams[targetIndex];
        float t = 0f;
        const float finishEpsilon = 0.02f; // allow small remainder on blended duration

        while (t < timeout)
        {
            try
            {
                var active = brain.ActiveVirtualCamera as Cinemachine.CinemachineVirtualCamera;
                var blend = brain.ActiveBlend;

                // If the active virtual camera is already the target
                if (active == target)
                {
                    // If there is no active blend, we're settled
                    if (blend == null || !blend.IsValid) yield break;

                    // If blend is essentially finished, we're settled
                    if (blend.Duration > 0f && blend.TimeInBlend + finishEpsilon >= blend.Duration) yield break;
                }

                // If blend exists and seems to be blending toward the target, wait for it to finish
                if (blend != null && blend.IsValid)
                {
                    // As an additional heuristic, if TimeInBlend reaches Duration, consider done
                    if (blend.Duration > 0f && blend.TimeInBlend + finishEpsilon >= blend.Duration) yield break;
                }
            }
            catch { }

            t += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"[TitleCameraMove] WaitForTargetOrTimeout timed out waiting for cams[{targetIndex}] after {timeout}s (active={brain.ActiveVirtualCamera?.Name})");
    }

    /// <summary>
    /// Wait until the CinemachineBrain's active blend has progressed at least targetProgress
    /// (0..1) toward the given virtual camera, or until timeout. This lets us chain camera
    /// priority changes without waiting for a full blend completion, removing visible pauses.
    /// </summary>
    private IEnumerator WaitForBlendProgressOrTimeout(int targetIndex, float targetProgress, float timeout)
    {
        if (brain == null || cams == null || targetIndex < 0 || targetIndex >= cams.Count)
            yield break;

        var target = cams[targetIndex];
        float t = 0f;
        while (t < timeout)
        {
            try
            {
                var active = brain.ActiveVirtualCamera as Cinemachine.CinemachineVirtualCamera;
                var blend = brain.ActiveBlend;

                // If the active virtual camera is already the target and there is no blend, we're settled
                if (active == target && (blend == null || !blend.IsValid)) yield break;

                if (blend != null && blend.IsValid && blend.Duration > 0f)
                {
                    float progress = blend.TimeInBlend / blend.Duration;
                    if (progress >= targetProgress) yield break;
                }
                else if (active == target)
                {
                    // If there's no valid blend but active == target, consider it ready
                    yield break;
                }
            }
            catch { }

            t += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"[TitleCameraMove] WaitForBlendProgressOrTimeout timed out waiting for cams[{targetIndex}] progress >= {targetProgress} after {timeout}s (active={brain.ActiveVirtualCamera?.Name})");
    }

    /// <summary>
    /// Wait until a blend that targets the given virtual camera has started (TimeInBlend > small epsilon)
    /// or until timeout. This lets us begin chaining priority changes while the current blend is
    /// still progressing, avoiding pauses between staged camera steps.
    /// </summary>
    private IEnumerator WaitForBlendStartedOrTimeout(int targetIndex, float timeout)
    {
        if (brain == null || cams == null || targetIndex < 0 || targetIndex >= cams.Count)
            yield break;

        var target = cams[targetIndex];
        float t = 0f;
        const float startEpsilon = 0.02f; // small time into blend to consider it "started"

        while (t < timeout)
        {
            try
            {
                var blend = brain.ActiveBlend;
                var active = brain.ActiveVirtualCamera as Cinemachine.CinemachineVirtualCamera;

                // If ActiveVirtualCamera is already the target and there is no blend, consider started
                if (active == target && (blend == null || !blend.IsValid)) yield break;

                // If any valid blend exists and has progressed a bit, consider the blend "started".
                if (blend != null && blend.IsValid && blend.TimeInBlend >= startEpsilon)
                {
                    // We set the target to highest priority just before calling this; if a blend
                    // is active and has progressed, assume it's the blend toward our target and proceed.
                    yield break;
                }
            }
            catch { }

            t += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"[TitleCameraMove] WaitForBlendStartedOrTimeout timed out waiting for cams[{targetIndex}] to start after {timeout}s (active={brain.ActiveVirtualCamera?.Name})");
    }
}
