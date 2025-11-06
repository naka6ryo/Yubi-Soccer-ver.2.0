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

    Vector3 networkPosition;
    Quaternion networkRotation;
    public FixedJoystick joystick;

    HandStateReceiver receiver;

    // 手のジェスチャー状態を保持
    private bool isRunning = false;
    private float runConfidence = 0f;
    private bool isCharging = false;
    private string currentHandState = "NONE";

    void Start()
    {
        // Find HandStateReceiver in the scene
        receiver = FindFirstObjectByType<HandStateReceiver>();

        // イベントリスナーを登録
        if (receiver != null)
        {
            receiver.onStateChanged.AddListener(OnHandStateChanged);
        }
        else
        {
            Debug.LogWarning("[PlayerController] HandStateReceiver not found in scene!");
        }

        // ジョイスティックが見つからない場合は自動検索
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
        // KickController の自動取得
        if (kickController == null)
        {
            kickController = GetComponent<PlayerKickController>();
            if (kickController == null)
            {
                kickController = FindFirstObjectByType<PlayerKickController>();
            }
        }
        // KickHitboxExpander の自動取得
        if (kickHitbox == null)
        {
            kickHitbox = GetComponentInChildren<YubiSoccer.Player.KickHitboxExpander>();
        }
        // 外部制御は PlayerKickController 側で許可されていれば共存するため、強制切替は不要

        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    // 手の状態が変化したときに呼ばれるコールバック
    void OnHandStateChanged(string state, float confidence)
    {
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
        if (receiver != null)
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
}
