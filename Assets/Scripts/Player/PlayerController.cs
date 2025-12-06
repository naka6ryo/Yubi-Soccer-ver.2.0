using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using YubiSoccer.Player;

// Simple player controller with Photon networking.
// - W: move forward
// - A/D: rotate left/right (yaw)
// - Local player controls movement; remote players are smoothly interpolated using networked position/rotation from OnPhotonSerializeView.
[RequireComponent(typeof(PhotonView))]
public class PlayerController : MonoBehaviourPun, IPunObservable
{
    [Tooltip("物理移動に使う Rigidbody。未設定なら自動取得")]
    [SerializeField] private Rigidbody rb;
    [Tooltip("登りたくないレイヤー（これらに当たったら進まない）")]
    [SerializeField] private LayerMask climbBlockMask = 0;
    [Tooltip("登れる最大傾斜角")]
    [SerializeField] private float maxSlopeAngle = 50f;
    [Tooltip("正面の段差・壁判定距離")]
    [SerializeField] private float frontCheckDistance = 0.6f;

    // 入力バッファ（FixedUpdate で使用）
    private float bufferedForward = 0f;
    private float bufferedTurn = 0f;

    // デバッグログを詳細に出すかどうか（Inspector から切り替え可能）
    [SerializeField]
    [Tooltip("詳細ログを有効にすると HandState イベント受信などのデバッグログを出します。通常は OFF にしてください。")]
    private bool verboseLog = false;

    [Header("Movement")]
    public float moveSpeed = 3f; // units per second
    public float rotationSpeed = 120f; // degrees per second

    [Header("Smoothing (remote)")]
    public float lerpRate = 10f;

    [Header("Runtime refs")]
    public Camera playerCamera; // assignable in prefab; will be enabled only for local player

    [Header("Kick Control")]
    public PlayerKickController kickController;

    [Tooltip("true の場合、AddForce を使う KickController を呼ばず、物理衝突用のヒットボックス拡大のみでキックします。")]
    public bool physicsKickOnly = false;

    [Tooltip("物理キック専用: コライダー拡大型のコンポーネント。auto-find します。")]
    public YubiSoccer.Player.KickHitboxExpander kickHitbox;

    [Header("Sound")]
    [Tooltip("走行中に再生するSEの間隔(秒)")]
    [SerializeField] private float runSEInterval = 1.0f;

    Vector3 networkPosition;
    Quaternion networkRotation;
    public FixedJoystick joystick;

    HandStateReceiver receiver;

    // 手のジェスチャー状態を保持
    private bool isRunning = false;
    private float runConfidence = 0f;
    private bool isCharging = false;
    private string currentHandState = "NONE";
    // 最後に処理した HandStateReceiver の状態（ポーリング同期用）
    private string lastProcessedHandState = "NONE";
    private SoundManager soundManager;
    // チャージ要求が発行されたが KickController がすぐには受け付けられなかった場合に保持するフラグ
    private bool pendingChargeRequest = false;

    // プレイヤー固有の AudioController (存在すれば利用して空間再生を行う)
    private YubiSoccer.Player.PlayerAudioController playerAudio;

    // 移動検出とSE制御
    private bool isMoving = false;
    private float runSETimer = 0f;
    // リモート側の走行フラグ（OnPhotonSerializeView から受信する）
    private bool remoteIsMoving = false;

    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
        else
        {
            Debug.LogWarning("[PlayerController] Rigidbody not found; movement will not work.");
        }

        // Start は必ずログを出してインスタンスの所有状態を確認しやすくする
        // Debug.Log($"[PlayerController] Start called on {gameObject.name} IsMine={photonView.IsMine}");

        // Find HandStateReceiver in the scene（購読はローカルのみ）
        receiver = FindFirstObjectByType<HandStateReceiver>();

        // イベントリスナーはローカルプレイヤーのインスタンスにのみ登録する
        // これにより、ブラウザ埋め込み等から来る手の入力が全プレイヤーのコントローラに
        // 伝播してしまい、他プレイヤーのキックが勝手に発動する問題を防ぐ
        if (receiver != null)
        {
            if (photonView.IsMine)
            {
                receiver.onStateChanged.AddListener(OnHandStateChanged);
                // if (verboseLog) Debug.Log($"[PlayerController] Registered HandStateReceiver listener on {gameObject.name} (IsMine=true)");
            }
            else
            {
                if (verboseLog) Debug.Log($"[PlayerController] Skipped registering HandStateReceiver on {gameObject.name} (IsMine=false)");
            }
        }
        else
        {
            if (photonView.IsMine)
                Debug.LogWarning("[PlayerController] HandStateReceiver not found in scene!");
        }

        // ジョイスティックが見つからない場合は自動検索（UI入力なのでシーン検索OK）
        if (joystick == null)
        {
            joystick = FindFirstObjectByType<FixedJoystick>();
            if (joystick == null)
            {
                Debug.LogWarning("[PlayerController] FixedJoystick not found in scene!");
            }
        }

        // Ensure camera is only active for the local player
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogWarning("[PlayerController] Main Camera not found in scene!");
            }
        }

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(photonView.IsMine);

            // ローカルプレイヤーのカメラを全 BallOffScreenIndicator に配布
            if (photonView.IsMine)
            {
                try
                {
                    YubiSoccer.UI.BallOffScreenIndicator.RegisterCameraForAll(playerCamera);
                }
                catch { /* 環境により未参照でも問題なし */ }
            }
        }

        // --- KickController / KickHitbox の安全な自動取得（自分のルート配下限定）---
        var myRoot = photonView.transform.root;

        if (kickController == null)
            kickController = myRoot.GetComponentInChildren<PlayerKickController>(true);

        if (kickHitbox == null)
            kickHitbox = myRoot.GetComponentInChildren<YubiSoccer.Player.KickHitboxExpander>(true);

        // 所有者一致チェック（誤配線検出）
        if (kickController != null && !BelongsToThisPlayer(kickController))
        {
            var other = kickController.GetComponentInParent<PhotonView>();
            // Debug.LogError($"[PlayerController] Miswired kickController (mine={photonView.ViewID}, theirs={(other ? other.ViewID : -1)}). Clearing reference.");
            kickController = null;
        }
        if (kickHitbox != null && !BelongsToThisPlayer(kickHitbox))
        {
            var other = kickHitbox.GetComponentInParent<PhotonView>();
            // Debug.LogError($"[PlayerController] Miswired kickHitbox (mine={photonView.ViewID}, theirs={(other ? other.ViewID : -1)}). Clearing reference.");
            kickHitbox = null;
        }

        networkPosition = transform.position;
        networkRotation = transform.rotation;

        soundManager = SoundManager.Instance;

        // PlayerAudioController があれば取得（プレハブにアタッチされている想定）
        playerAudio = myRoot.GetComponentInChildren<YubiSoccer.Player.PlayerAudioController>(true);

        // AudioListener の設定（非所有インスタンスでは無効化）
        ConfigureAudioListenerForOwnership();

        // 開発時や外部から明示的にログを有効化できるようにする (PlayerCreator などから呼ぶ)
        // 注意: デフォルトは Inspector の値を尊重します。
    }

    /// <summary>
    /// 外部から詳細ログの有効/無効を切り替えるための公開メソッド。
    /// PlayerCreator などがインスタンス化直後に呼び出してデバッグログを出せるようにします。
    /// </summary>
    public void SetVerboseLogging(bool enabled)
    {
        verboseLog = enabled;
        if (verboseLog)
        {
            Debug.Log($"[PlayerController] Verbose logging enabled on {gameObject.name} IsMine={photonView.IsMine}");
        }
    }

    // 手の状態が変化したときに呼ばれるコールバック
    void OnHandStateChanged(string state, float confidence)
    {
        if (verboseLog) Debug.Log($"[PlayerController] OnHandStateChanged called on {gameObject.name} IsMine={photonView.IsMine} state={state} confidence={confidence}");
        // RUN 状態の場合は即座にフラグを立てる
        if (state == "RUN")
        {
            isRunning = true;
            runConfidence = confidence;
        }
        else
        {
            // RUN 以外の状態では即座に停止
            isRunning = false;
            runConfidence = 0f;
        }

        // KICK / CHARGE への対応
        currentHandState = state ?? "NONE";
        if (!photonView.IsMine) return;

        // 物理キックオンの場合は、KickController を呼ばずにヒットボックス拡大を操作
        if (physicsKickOnly)
        {
            if (kickHitbox == null) return;
            HandlePhysicsKickByState(currentHandState);
            return;
        }

        if (kickController == null) return;

        var s = currentHandState.ToUpperInvariant();
        switch (s)
        {
            case "KICK":
                // チャージ中なら解放してからタップ
                if (isCharging)
                {
                    kickController.ExternalChargeRelease();
                    isCharging = false;
                }
                kickController.ExternalKickTap();
                break;
            case "CHARGE":
                if (!isCharging)
                {
                    // Try to start charge; if it fails (controller busy), set pending flag
                    bool started = false;
                    try { started = kickController.ExternalChargeStart(); } catch { started = false; }
                    if (started)
                    {
                        isCharging = true;
                        pendingChargeRequest = false;
                    }
                    else
                    {
                        pendingChargeRequest = true;
                    }
                }
                break;
            default:
                // CHARGE 以外へ遷移したら、チャージ中なら解放
                if (isCharging)
                {
                    kickController.ExternalChargeRelease();
                    isCharging = false;
                }
                pendingChargeRequest = false;
                break;
        }
    }

    void HandlePhysicsKickByState(string state)
    {
        var s = (state ?? "NONE").ToUpperInvariant();
        switch (s)
        {
            case "KICK":
                // 単発拡大（Tap）
                kickHitbox.KickTap();
                // チャージ解除相当（安全にリセット）
                if (isCharging)
                {
                    kickHitbox.ChargeRelease();
                    isCharging = false;
                }
                break;
            case "CHARGE":
                if (!isCharging)
                {
                    kickHitbox.ChargeStart();
                    isCharging = true;
                }
                break;
            default:
                if (isCharging)
                {
                    kickHitbox.ChargeRelease();
                    isCharging = false;
                }
                break;
        }
    }

    void OnDestroy()
    {
        // イベントリスナーを解除
        if (receiver != null && photonView.IsMine)
        {
            receiver.onStateChanged.RemoveListener(OnHandStateChanged);
        }
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            // --- HandStateReceiver の表示（UI）と実際のプレイヤー状態を常に同期する ---
            // Web->Unity の送信が稀に欠落するケースに備え、Receiver の public な currentState を
            // 毎フレームチェックして、表示が CHARGE なら必ずチャージ開始処理を呼ぶ。
            if (receiver != null)
            {
                var recvState = (receiver.currentState ?? "NONE").ToUpperInvariant();
                var recvConf = receiver.currentConfidence;
                // 状態が変化した、または表示が CHARGE なのにローカルでチャージが始まっていない場合は強制同期
                if (recvState != lastProcessedHandState || (recvState == "CHARGE" && !isCharging))
                {
                    // call the same handler as the event to reuse logic
                    OnHandStateChanged(recvState, recvConf);
                    lastProcessedHandState = recvState;
                }
                // 受信が CHARGE でかつチャージ開始が保留中なら、改めて開始を試みる
                if (recvState == "CHARGE" && pendingChargeRequest && !isCharging && kickController != null)
                {
                    try
                    {
                        if (kickController.CanStartCharge())
                        {
                            bool started = kickController.ExternalChargeStart();
                            if (started)
                            {
                                isCharging = true;
                                pendingChargeRequest = false;
                            }
                        }
                    }
                    catch { /* ignore errors and keep pending */ }
                }
                else if (recvState != "CHARGE")
                {
                    // 表示が CHARGE でないなら保留は解除
                    pendingChargeRequest = false;
                }
            }

            HandleInput();
            // ハンドステートによる長押しチャージ継続
            if (isCharging)
            {
                if (physicsKickOnly)
                {
                    if (kickHitbox != null) kickHitbox.ChargeUpdate(Time.deltaTime);
                }
                else if (kickController != null)
                {
                    kickController.ExternalChargeUpdate(Time.deltaTime);
                }
            }
            // 走行中のSEを 1 秒ごとに再生
            if (isMoving)
            {
                runSETimer -= Time.deltaTime;
                if (runSETimer <= 0f)
                {
                    // PlayerAudioController があればローカル非空間再生、無ければ従来の SoundManager を使用
                    if (playerAudio != null)
                    {
                        playerAudio.PlayRunLocalOneShot();
                    }
                    else
                    {
                        soundManager?.PlaySE("走る");
                    }
                    runSETimer = Mathf.Max(0.001f, runSEInterval);
                }
            }
            else
            {
                // 停止時はタイマーをリセットして即時再生を防ぐ
                runSETimer = 0f;
            }
        }
        else
        {
            // smooth remote transforms
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpRate);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * lerpRate);

            // リモートの走行SE を空間再生する (PlayerAudioController があればそちらを利用)
            if (remoteIsMoving)
            {
                runSETimer -= Time.deltaTime;
                if (runSETimer <= 0f)
                {
                    if (playerAudio != null)
                    {
                        playerAudio.PlayRunRemoteOneShot();
                    }
                    else
                    {
                        // フォールバック: グローバル再生 (2D)
                        soundManager?.PlaySE("走る");
                    }
                    runSETimer = Mathf.Max(0.001f, runSEInterval);
                }
            }
            else
            {
                runSETimer = 0f;
            }
        }
    }
    private void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        if (rb == null) return;
        ApplyMovementPhysics();
    }

    void HandleInput()
    {
        if (!inputEnabled)
        {
            // 入力無効時は移動を停止
            bufferedForward = 0f;
            bufferedTurn = 0f;
            isMoving = false;
            return;
        }

        float forward = 0f;
        if (Input.GetKey(KeyCode.W)) forward = 1f;

        if (isRunning && runConfidence > 0.5f)
        {
            forward = 1f;
        }

        float turn = 0f;
        if (Input.GetKey(KeyCode.A)) turn = -1f;
        else if (Input.GetKey(KeyCode.D)) turn = 1f;

        if (turn == 0f && joystick != null)
        {
            float joyInput = joystick.Horizontal;
            turn = 2 * joyInput;
        }

        // 入力をバッファリングし、移動フラグを更新
        bufferedForward = forward;
        bufferedTurn = turn;
        isMoving = Mathf.Abs(forward) > 0.001f;
    }

    private void ApplyMovementPhysics()
    {
        // 回転を先に適用
        if (Mathf.Abs(bufferedTurn) > 0.001f)
        {
            var deltaRot = Quaternion.Euler(0f, bufferedTurn * rotationSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * deltaRot);
        }

        if (Mathf.Abs(bufferedForward) <= 0.001f) return;

        // 接地法線を取得（地面に投影して登りを防ぐ）
        Vector3 moveDir = rb.transform.forward;
        if (Physics.Raycast(rb.position + Vector3.up * 0.1f, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (IsBlockedLayer(hit.collider.gameObject.layer))
            {
                return;
            }
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle > maxSlopeAngle)
            {
                // 急斜面なら移動しない
                return;
            }
            moveDir = Vector3.ProjectOnPlane(moveDir, hit.normal).normalized;
        }

        // 正面の壁・他プレイヤーへの乗り上がり防止（段差が高いときは進まない）
        if (Physics.Raycast(rb.position + Vector3.up * 0.2f, moveDir, frontCheckDistance, climbBlockMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Vector3 targetPos = rb.position + moveDir * bufferedForward * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPos);
    }

    // AudioListener の多重存在による警告を抑制するため、プレイヤー(prefab)に含まれる AudioListener を
    // 非オーナーでは無効化、オーナーのみ有効化する。これにより "There are 2 audio listeners" の警告を回避する。
    // （シーン側の Main Camera に AudioListener がある前提）
    private void ConfigureAudioListenerForOwnership()
    {
        var al = GetComponentInChildren<AudioListener>(true);
        if (al != null)
        {
            // Let the centralized manager decide which listener should be enabled to guarantee exactly one.
            try
            {
                AudioListenerManager.Instance.RegisterLocalListener(al, photonView.IsMine);
            }
            catch
            {
                // Fallback to previous behavior if the manager is not available for some reason
                al.enabled = photonView.IsMine;
            }
            if (verboseLog) Debug.Log($"[PlayerController] AudioListener on {gameObject.name} enabled={al.enabled}");
        }
    }

    // Photon serialization - send/receive transform
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            // 所有者は走行フラグを送る
            stream.SendNext(isMoving);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            try
            {
                remoteIsMoving = (bool)stream.ReceiveNext();
            }
            catch
            {
                remoteIsMoving = false;
            }
        }
    }

    private bool IsBlockedLayer(int layer)
    {
        return (climbBlockMask.value & (1 << layer)) != 0;
    }

    // ---- Helper ----
    // 渡されたコンポーネントが「このプレイヤー（同じ PhotonView ルート）」に属しているか検証
    private bool BelongsToThisPlayer(Component c)
    {
        if (c == null) return false;
        var pv = c.GetComponentInParent<PhotonView>();
        return pv != null && pv == photonView;
    }

    /// <summary>
    /// 外部からプレイヤーの操作（移動・回転）を有効/無効にする
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        // ここではフラグ管理を追加せず、単純にコンポーネントの有効無効で制御するか、
        // あるいは専用のフラグを設けるのが安全です。
        // 今回はシンプルに enabled プロパティで制御すると PhotonView の同期まで止まる可能性があるため、
        // 入力処理を行う Update/FixedUpdate 内でチェックするフラグを追加します。
        inputEnabled = enabled;
    }
    private bool inputEnabled = true; // デフォルトは有効
}
