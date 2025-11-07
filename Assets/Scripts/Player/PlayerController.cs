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
    private SoundManager soundManager;

    // 移動検出とSE制御
    private bool isMoving = false;
    private float runSETimer = 0f;

    void Start()
    {
        // Start は必ずログを出してインスタンスの所有状態を確認しやすくする
        Debug.Log($"[PlayerController] Start called on {gameObject.name} IsMine={photonView.IsMine}");

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
                if (verboseLog) Debug.Log($"[PlayerController] Registered HandStateReceiver listener on {gameObject.name} (IsMine=true)");
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
            Debug.LogError($"[PlayerController] Miswired kickController (mine={photonView.ViewID}, theirs={(other ? other.ViewID : -1)}). Clearing reference.");
            kickController = null;
        }
        if (kickHitbox != null && !BelongsToThisPlayer(kickHitbox))
        {
            var other = kickHitbox.GetComponentInParent<PhotonView>();
            Debug.LogError($"[PlayerController] Miswired kickHitbox (mine={photonView.ViewID}, theirs={(other ? other.ViewID : -1)}). Clearing reference.");
            kickHitbox = null;
        }

        networkPosition = transform.position;
        networkRotation = transform.rotation;

        soundManager = SoundManager.Instance;

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
                    kickController.ExternalChargeStart();
                    isCharging = true;
                }
                break;
            default:
                // CHARGE 以外へ遷移したら、チャージ中なら解放
                if (isCharging)
                {
                    kickController.ExternalChargeRelease();
                    isCharging = false;
                }
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
                    // サウンドマネージャがあれば再生（null 安全）
                    soundManager?.PlaySE("走る");
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
        }
    }

    void HandleInput()
    {
        float forward = 0f;
        if (Input.GetKey(KeyCode.W)) forward = 1f;

        // イベントベースで更新された isRunning フラグを使用
        // confidence の閾値を下げて、より確実に反応するように改善
        if (isRunning && runConfidence > 0.5f)
        {
            forward = 1f;
        }

        float turn = 0f;
        if (Input.GetKey(KeyCode.A)) turn = -1f;
        else if (Input.GetKey(KeyCode.D)) turn = 1f;

        // ジョイスティックの入力（キーボード入力がない場合）
        if (turn == 0f && joystick != null)
        {
            float joyInput = joystick.Horizontal;
            turn = 2 * joyInput;
        }

        if (forward != 0f)
        {
            transform.Translate(Vector3.forward * forward * moveSpeed * Time.deltaTime);
        }
        if (turn != 0f)
        {
            transform.Rotate(Vector3.up, turn * rotationSpeed * Time.deltaTime);
        }

        // 移動中フラグを更新
        isMoving = Mathf.Abs(forward) > 0.001f;
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
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }

    // ---- Helper ----
    // 渡されたコンポーネントが「このプレイヤー（同じ PhotonView ルート）」に属しているか検証
    private bool BelongsToThisPlayer(Component c)
    {
        if (c == null) return false;
        var pv = c.GetComponentInParent<PhotonView>();
        return pv != null && pv == photonView;
    }
}
