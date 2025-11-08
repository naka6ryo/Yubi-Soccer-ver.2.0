// CM2_SwitchNearEndSmooth.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.SceneManagement;
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
        if (playerCamera == null)
        {
            // try find by name
            var go = GameObject.Find("PlayerCamera");
            if (go != null) playerCamera = go.GetComponent<Camera>();
            // try find by tag
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

        if (playerCamera == null)
        {
            Debug.LogWarning("[TitleCameraMove] PlayerCamera not found; cannot switch.");
            return;
        }

        // Disable Cinemachine brain so the player camera can render directly
        if (brain != null)
        {
            brain.enabled = false;
        }

        // Activate player camera
        playerCamera.gameObject.SetActive(true);
        playerCamera.enabled = true;

        Debug.Log("[TitleCameraMove] Switched to PlayerCamera: " + playerCamera.name);

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
}
