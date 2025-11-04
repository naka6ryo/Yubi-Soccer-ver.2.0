using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace YubiSoccer.Player
{
    /// <summary>
    /// スペースキー入力でプレイヤーのキック判定(トリガー)を急拡大し、ボールへインパルスを与える。
    /// 将来の長押し(チャージ)拡張に対応できるよう、簡易ステートとチャージ係数を用意。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerKickController : MonoBehaviour
    {
        private enum KickState { Idle, Charging, Expanding, Shrinking }

        [Header("Kick Zone (SphereCollider/Trigger)")]
        [SerializeField] private SphereCollider kickCollider; // Triggerを想定
        [SerializeField] private bool createColliderIfMissing = true;
        [Tooltip("キック判定(トリガー)のローカル中心")]
        [SerializeField]
        private Vector3 localCenter = new Vector3(0f, 0.5f, 0.4f);
        [Tooltip("待機時の最小半径")][SerializeField] private float baseRadius = 0.2f;
        [Tooltip("拡大時の最大半径")][SerializeField] private float maxRadius = 1.6f;
        [Tooltip("拡大速度(半径/秒)")][SerializeField] private float expandSpeed = 12f;
        [Tooltip("縮小速度(半径/秒)")][SerializeField] private float shrinkSpeed = 16f;

        [Header("Kick Force")]
        [Tooltip("基礎キック力(インパルス)")]
        [SerializeField]
        private float kickForce = 10f;
        [Tooltip("キック方向の基準(未設定ならプレイヤー→ボール方向)")]
        [SerializeField]
        private Transform forceDirectionReference;
        [Tooltip("影響を与えるレイヤー(ボールなど)")]
        [SerializeField]
        private LayerMask affectLayers = ~0;
        [Tooltip("タグが設定されている場合のみキックする(空なら無視)")]
        [SerializeField]
        private string requiredTag = ""; // 例: "Ball"

        [Header("Input")]
        [SerializeField] private KeyCode kickKey = KeyCode.Space;

        [Header("Charge (future ready)")]
        [Tooltip("長押しチャージを有効化(将来の拡張向け)。無効時はタップ即発動")]
        [SerializeField] private bool enableCharge = true;
        [Tooltip("最大チャージ時間(秒)")][SerializeField] private float maxChargeTime = 1.0f;
        [Tooltip("チャージ率→出力倍率のカーブ(0..1)")]
        [SerializeField]
        private AnimationCurve chargeToForce = AnimationCurve.Linear(0, 1, 1, 1);

        [Header("Hitbox scaling by charge")]
        [Tooltip("チャージ量に応じて拡大到達半径をスケールする")]
        [SerializeField] private bool scaleHitboxWithCharge = true;
        [Tooltip("ゼロチャージ(タップ)時の到達半径")]
        [SerializeField] private float zeroChargeMaxRadius = 1.6f;
        [Tooltip("チャージ率→半径スケール(0..1)。0でゼロチャージ半径、1でmaxRadiusへ")]
        [SerializeField] private AnimationCurve chargeToRadius = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Lift (ball arc)")]
        [Tooltip("キック時の上方向インパルスの基礎値")]
        [SerializeField]
        private float liftImpulseBase = 0.5f;
        [Tooltip("チャージ1.0時に上乗せされる上方向インパルス")]
        [SerializeField]
        private float liftImpulsePerCharge = 1.0f;

        [Header("Speed scaling by charge")]
        [Tooltip("チャージに応じて拡大/縮小速度をスケールする")]
        [SerializeField] private bool scaleSpeedWithCharge = true;
        [Tooltip("チャージ率→速度倍率(0..1)。0=等倍, 1=最大倍率へ")]
        [SerializeField] private AnimationCurve chargeToSpeed = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("拡大速度の最大倍率(1以上を推奨)")]
        [SerializeField] private float expandSpeedChargeMultiplier = 2f;
        [Tooltip("縮小速度の最大倍率(1以上を推奨)")]
        [SerializeField] private float shrinkSpeedChargeMultiplier = 2f;

        [Header("Recoil (visual tilt)")]
        [Tooltip("キック発動時に見た目の反動で少し後ろへ倒す")]
        [SerializeField] private bool enableRecoil = true;
        [Tooltip("反動で傾ける対象(未設定なら自身のTransform)")]
        [SerializeField] private Transform recoilTransform;
        [Tooltip("反動の角度(度)。正の値で後ろへ倒れる(ローカルX軸-方向)")]
        [SerializeField] private float recoilAngleDeg = 12f;
        [Tooltip("反動(行き)の時間(秒)")]
        [SerializeField] private float recoilDuration = 0.08f;
        [Tooltip("反動(戻り)の時間(秒)")]
        [SerializeField] private float recoveryDuration = 0.15f;
        [Tooltip("行きのカーブ(0→1)")]
        [SerializeField] private AnimationCurve recoilCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("戻りのカーブ(0→1)。1に近づくほど元の角度へ戻る")]
        [SerializeField] private AnimationCurve recoveryCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("チャージ量に応じて反動角度をスケールする")]
        [SerializeField] private bool scaleRecoilWithCharge = true;
        [Tooltip("チャージ1.0時の反動角度倍率")]
        [SerializeField] private float recoilAngleChargeMultiplier = 1.4f;

        [Header("Ground Circle Indicator")]
        [Tooltip("チャージ中に地面上の円を表示する")]
        [SerializeField] private bool showIndicatorWhileCharging = true;
        [Tooltip("キック発動時に円を自動非表示にする")]
        [SerializeField] private bool hideIndicatorOnKick = true;
        [Tooltip("地面サークルの参照(任意)。未設定なら子から自動取得を試みる")]
        [SerializeField] private KickRadiusIndicator radiusIndicator;

        [Header("Indicator width by charge")]
        [Tooltip("チャージ率に応じて地面サークルの線幅を太くする")]
        [SerializeField] private bool scaleIndicatorWidthWithCharge = true;
        [Tooltip("ゼロチャージ時の線幅(LineRenderer.widthMultiplier)")]
        [SerializeField] private float indicatorWidthAtZeroCharge = 0.05f;
        [Tooltip("フルチャージ時の線幅(LineRenderer.widthMultiplier)")]
        [SerializeField] private float indicatorWidthAtFullCharge = 0.12f;
        [Tooltip("チャージ率→線幅補間用カーブ(0..1)。0でゼロ幅、1でフル幅への重み")]
        [SerializeField] private AnimationCurve chargeToIndicatorWidth = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Indicator color by charge")]
        [Tooltip("チャージ率に応じて地面サークルの色を寒色→暖色に補間する")]
        [SerializeField] private bool colorIndicatorWithCharge = true;
        [Tooltip("色の補間に Gradient を使用。オンのときは chargeToIndicatorColor で補正した t を Gradient.Evaluate に渡す")]
        [SerializeField] private bool useIndicatorGradient = false;
        [Tooltip("チャージ率(0→1)に対する色のグラデーション。useIndicatorGradient がオンの時に使用")]
        [SerializeField] private Gradient indicatorColorGradient;
        [Tooltip("ゼロチャージ時の色(寒色)")]
        [SerializeField] private Color indicatorColorAtZeroCharge = new Color(0.2f, 0.8f, 1f, 0.9f);
        [Tooltip("フルチャージ時の色(暖色)")]
        [SerializeField] private Color indicatorColorAtFullCharge = new Color(1f, 0.5f, 0.2f, 0.9f);
        [Tooltip("チャージ率→色補間重みのカーブ(0..1)")]
        [SerializeField] private AnimationCurve chargeToIndicatorColor = AnimationCurve.Linear(0, 0, 1, 1);

        private KickState state = KickState.Idle;
        private float currentRadius;
        private float chargeTime;
        private float lastKickPowerMultiplier = 1f;
        private float lastCharge01 = 0f;
        private float activeMaxRadius; // 今回キック時に到達する半径
        private float activeExpandSpeed; // 今回キックの拡大速度
        private float activeShrinkSpeed; // 今回キックの縮小速度
        private Quaternion recoilBaseLocalRotation;
        private Quaternion recoilInitialLocalRotation;
        private Coroutine recoilRoutine;
        private readonly HashSet<Rigidbody> kickedThisActivation = new HashSet<Rigidbody>();

        private void Awake()
        {
            EnsureCollider();
            DeactivateKickZone();
            if (recoilTransform == null) recoilTransform = transform;
            recoilInitialLocalRotation = recoilTransform.localRotation; // 累積しない基準姿勢

            if (radiusIndicator == null)
                radiusIndicator = GetComponentInChildren<KickRadiusIndicator>(true);
            if (radiusIndicator != null)
                radiusIndicator.SetCenter(transform);
        }

        private void OnValidate()
        {
            if (kickCollider == null)
                kickCollider = GetComponent<SphereCollider>();

            if (kickCollider != null)
            {
                kickCollider.isTrigger = true;
                kickCollider.center = localCenter;
                kickCollider.radius = Mathf.Max(0.001f, baseRadius);
            }

            baseRadius = Mathf.Max(0.001f, baseRadius);
            maxRadius = Mathf.Max(baseRadius, maxRadius);
            expandSpeed = Mathf.Max(0.001f, expandSpeed);
            shrinkSpeed = Mathf.Max(0.001f, shrinkSpeed);
            maxChargeTime = Mathf.Max(0.001f, maxChargeTime);
            kickForce = Mathf.Max(0f, kickForce);

            // 半径の整合性
            zeroChargeMaxRadius = Mathf.Clamp(zeroChargeMaxRadius, baseRadius, maxRadius);

            // 速度倍率の整合性
            expandSpeedChargeMultiplier = Mathf.Max(0.01f, expandSpeedChargeMultiplier);
            shrinkSpeedChargeMultiplier = Mathf.Max(0.01f, shrinkSpeedChargeMultiplier);

            if (recoilTransform == null)
                recoilTransform = transform;
            recoilAngleDeg = Mathf.Max(0f, recoilAngleDeg);
            recoilDuration = Mathf.Max(0f, recoilDuration);
            recoveryDuration = Mathf.Max(0f, recoveryDuration);
            recoilAngleChargeMultiplier = Mathf.Max(0.01f, recoilAngleChargeMultiplier);

            // インジケータ線幅の整合性
            indicatorWidthAtZeroCharge = Mathf.Max(0.001f, indicatorWidthAtZeroCharge);
            indicatorWidthAtFullCharge = Mathf.Max(0.001f, indicatorWidthAtFullCharge);
        }

        private void EnsureCollider()
        {
            if (kickCollider == null)
                kickCollider = GetComponent<SphereCollider>();

            if (kickCollider == null && createColliderIfMissing)
            {
                kickCollider = gameObject.AddComponent<SphereCollider>();
            }

            if (kickCollider != null)
            {
                kickCollider.isTrigger = true;
                kickCollider.center = localCenter;
                kickCollider.radius = Mathf.Max(0.001f, baseRadius);
            }
        }

        private void Update()
        {
            // キーボード入力は常に受け付ける（手の認識と共存）
            HandleInput();
            UpdateKickState(Time.deltaTime);
        }

        private void HandleInput()
        {
            if (enableCharge)
            {
                switch (state)
                {
                    case KickState.Idle:
                        if (Input.GetKeyDown(kickKey))
                        {
                            state = KickState.Charging;
                            chargeTime = 0f;
                        }
                        break;
                    case KickState.Charging:
                        if (Input.GetKey(kickKey))
                        {
                            chargeTime += Time.deltaTime;
                            chargeTime = Mathf.Min(chargeTime, maxChargeTime);
                            float c01 = Mathf.Clamp01(chargeTime / maxChargeTime);
                            UpdateChargingVisuals(c01, Time.deltaTime);
                        }
                        if (Input.GetKeyUp(kickKey))
                        {
                            float charge01 = Mathf.Clamp01(chargeTime / maxChargeTime);
                            lastCharge01 = charge01;
                            lastKickPowerMultiplier = Mathf.Max(0f, chargeToForce.Evaluate(charge01));
                            BeginKick();
                        }
                        break;
                }
            }
            else
            {
                if (state == KickState.Idle && Input.GetKeyDown(kickKey))
                {
                    lastKickPowerMultiplier = 1f; // タップは等倍
                    lastCharge01 = 0f; // 非チャージ時は基礎リフトのみ
                    BeginKick();
                }
            }
        }

        private void BeginKick()
        {
            if (kickCollider == null) return;

            // 発動準備
            kickedThisActivation.Clear();
            currentRadius = baseRadius;
            kickCollider.radius = currentRadius;
            // 今回の到達半径を決定
            if (scaleHitboxWithCharge)
            {
                float t = Mathf.Clamp01(chargeToRadius.Evaluate(Mathf.Clamp01(lastCharge01)));
                activeMaxRadius = Mathf.Lerp(zeroChargeMaxRadius, maxRadius, t);
            }
            else
            {
                activeMaxRadius = maxRadius;
            }
            activeMaxRadius = Mathf.Max(baseRadius, Mathf.Min(activeMaxRadius, maxRadius));

            // 今回の拡大/縮小速度を決定
            if (scaleSpeedWithCharge)
            {
                float s = Mathf.Clamp01(chargeToSpeed.Evaluate(Mathf.Clamp01(lastCharge01)));
                float expandMul = Mathf.Lerp(1f, expandSpeedChargeMultiplier, s);
                float shrinkMul = Mathf.Lerp(1f, shrinkSpeedChargeMultiplier, s);
                activeExpandSpeed = expandSpeed * expandMul;
                activeShrinkSpeed = shrinkSpeed * shrinkMul;
            }
            else
            {
                activeExpandSpeed = expandSpeed;
                activeShrinkSpeed = shrinkSpeed;
            }
            // 視覚的反動を開始
            StartRecoil();

            // 発動時に円を隠す
            if (hideIndicatorOnKick && radiusIndicator != null)
                radiusIndicator.Hide();

            ActivateKickZone();
            state = KickState.Expanding;
        }

        private void UpdateKickState(float dt)
        {
            switch (state)
            {
                case KickState.Expanding:
                    currentRadius += activeExpandSpeed * dt;
                    if (currentRadius >= activeMaxRadius)
                    {
                        currentRadius = activeMaxRadius;
                        state = KickState.Shrinking; // ピーク到達後は縮小
                    }
                    if (kickCollider != null) kickCollider.radius = currentRadius;
                    break;

                case KickState.Shrinking:
                    currentRadius = Mathf.MoveTowards(currentRadius, baseRadius, activeShrinkSpeed * dt);
                    if (kickCollider != null) kickCollider.radius = currentRadius;
                    if (Mathf.Approximately(currentRadius, baseRadius))
                    {
                        DeactivateKickZone();
                        state = KickState.Idle;
                    }
                    break;
            }
        }

        private void ActivateKickZone()
        {
            if (kickCollider != null)
            {
                kickCollider.enabled = true;
            }
        }

        private void DeactivateKickZone()
        {
            if (kickCollider != null)
            {
                kickCollider.enabled = true; // enabledは維持、半径を最小に保つ方式
                kickCollider.radius = baseRadius;
            }
            kickedThisActivation.Clear();
            if (radiusIndicator != null)
                radiusIndicator.Hide();
        }

        private void OnDisable()
        {
            // 無効化時は反動をクリアして姿勢を戻す
            bool wasRecoilRunning = recoilRoutine != null;
            if (recoilRoutine != null)
            {
                StopCoroutine(recoilRoutine);
                recoilRoutine = null;
            }
            if (recoilTransform != null && wasRecoilRunning)
            {
                // 反動途中で無効化された場合のみ、反動前の基準(開始時の向き)へ戻す
                recoilTransform.localRotation = recoilBaseLocalRotation;
            }
        }

        [Header("External Control")]
        [Tooltip("外部(ハンドステート等)からの制御を許可。キーボードと共存可能")]
        [SerializeField] private bool allowExternalControl = true;

        /// <summary>
        /// 外部制御: タップ(短押し)キックを即発動。
        /// </summary>
        public void ExternalKickTap()
        {
            if (!allowExternalControl) return;
            if (state != KickState.Idle) return;
            lastKickPowerMultiplier = 1f;
            lastCharge01 = 0f;
            BeginKick();
        }

        /// <summary>
        /// 外部制御: チャージ開始。
        /// </summary>
        public void ExternalChargeStart()
        {
            if (!allowExternalControl) return;
            if (state != KickState.Idle) return;
            state = KickState.Charging;
            chargeTime = 0f;
            UpdateChargingVisuals(0f, 0f);
        }

        /// <summary>
        /// 外部制御: チャージ継続。毎フレーム呼び出しを想定。
        /// </summary>
        public void ExternalChargeUpdate(float dt)
        {
            if (!allowExternalControl) return;
            if (state != KickState.Charging) return;
            chargeTime += Mathf.Max(0f, dt);
            chargeTime = Mathf.Min(chargeTime, maxChargeTime);
            float c01 = Mathf.Clamp01(chargeTime / maxChargeTime);
            UpdateChargingVisuals(c01, dt);
        }

        /// <summary>
        /// 外部制御: チャージ解放(発動)。
        /// </summary>
        public void ExternalChargeRelease()
        {
            if (!allowExternalControl) return;
            if (state != KickState.Charging) return;
            float charge01 = Mathf.Clamp01(chargeTime / maxChargeTime);
            lastCharge01 = charge01;
            lastKickPowerMultiplier = Mathf.Max(0f, chargeToForce.Evaluate(charge01));
            BeginKick();
        }

        /// <summary>
        /// チャージ中のプレビュー(半径/幅/色/パルス)を更新
        /// </summary>
        private void UpdateChargingVisuals(float c01, float dt)
        {
            if (!(showIndicatorWhileCharging && radiusIndicator != null)) return;

            float t = Mathf.Clamp01(chargeToRadius.Evaluate(c01));
            float previewRadius = scaleHitboxWithCharge
                ? Mathf.Lerp(zeroChargeMaxRadius, maxRadius, t)
                : maxRadius;
            radiusIndicator.Show();
            radiusIndicator.SetRadius(previewRadius);

            // 線幅
            if (scaleIndicatorWidthWithCharge)
            {
                float wt = Mathf.Clamp01(chargeToIndicatorWidth.Evaluate(c01));
                float w = Mathf.Lerp(indicatorWidthAtZeroCharge, indicatorWidthAtFullCharge, wt);
                radiusIndicator.SetWidth(w);
            }

            // 色
            Color currentColor = radiusIndicator.GetCurrentColor();
            if (colorIndicatorWithCharge)
            {
                float ct = Mathf.Clamp01(chargeToIndicatorColor.Evaluate(c01));
                Color col = (useIndicatorGradient && indicatorColorGradient != null)
                    ? indicatorColorGradient.Evaluate(ct)
                    : Color.Lerp(indicatorColorAtZeroCharge, indicatorColorAtFullCharge, ct);
                radiusIndicator.SetColor(col);
                currentColor = col;
            }

            // パルス
            radiusIndicator.UpdatePulseEffect(previewRadius, currentColor, c01, Mathf.Max(0f, dt));
        }

        private void StartRecoil()
        {
            if (!enableRecoil) return;
            if (recoilTransform == null) recoilTransform = transform;

            if (recoilRoutine != null)
            {
                StopCoroutine(recoilRoutine);
                recoilRoutine = null;
            }

            // 現在の向きを基準にピッチのみ加える(ヨーは保持)
            recoilBaseLocalRotation = recoilTransform.localRotation;

            float angle = recoilAngleDeg;
            if (scaleRecoilWithCharge)
            {
                float mul = Mathf.Lerp(1f, recoilAngleChargeMultiplier, Mathf.Clamp01(lastCharge01));
                angle *= mul;
            }

            recoilRoutine = StartCoroutine(CoRecoil(angle));
        }

        private IEnumerator CoRecoil(float angleDeg)
        {
            if (recoilTransform == null) yield break;

            // 行き(素早く傾ける)
            float t = 0f;
            while (t < recoilDuration)
            {
                float u = (recoilDuration > 0f) ? (t / recoilDuration) : 1f;
                float f = Mathf.Clamp01(recoilCurve.Evaluate(u));
                ApplyTilt(angleDeg * f);
                t += Time.deltaTime;
                yield return null;
            }
            ApplyTilt(angleDeg);

            // 行きのみ：最大角で停止（戻りは行わない）
            recoilRoutine = null;
        }

        private void ApplyTilt(float angleDeg)
        {
            if (recoilTransform == null) return;
            Quaternion q = Quaternion.Euler(-angleDeg, 0f, 0f); // 後ろに倒す: ローカルX負方向
            recoilTransform.localRotation = recoilBaseLocalRotation * q;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (kickCollider == null) return;
            if (other == null || other == kickCollider) return;

            if (!(state == KickState.Expanding)) return; // 拡大中のみ有効

            // レイヤーチェック
            int layerMask = 1 << other.gameObject.layer;
            if ((affectLayers.value & layerMask) == 0) return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;
            if (kickedThisActivation.Contains(rb)) return; // 多重キック防止

            Vector3 dir;
            if (forceDirectionReference != null)
                dir = forceDirectionReference.forward;
            else
                dir = (other.transform.position - transform.position).normalized;

            float power = kickForce * Mathf.Max(0.05f, lastKickPowerMultiplier);
            Vector3 impulse = dir.normalized * power;

            // 上方向リフト: チャージに応じて増やす
            float lift = Mathf.Max(0f, liftImpulseBase + liftImpulsePerCharge * Mathf.Clamp01(lastCharge01));
            impulse += Vector3.up * lift;

            rb.AddForce(impulse, ForceMode.Impulse);

            kickedThisActivation.Add(rb);
        }

        private void OnDrawGizmosSelected()
        {
            if (kickCollider != null)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.3f);
                Matrix4x4 m = Matrix4x4.TRS(transform.TransformPoint(kickCollider.center), Quaternion.identity, Vector3.one);
                Gizmos.matrix = m;
                Gizmos.DrawWireSphere(Vector3.zero, kickCollider.radius);
            }
            else
            {
                // 配置目安
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(localCenter, baseRadius);
            }
        }
    }
}
