// CM2_SwitchNearEndSmooth.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.SceneManagement;

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
