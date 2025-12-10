using UnityEngine;
using System.Collections.Generic;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 画面外にあるターゲット(ボール)の方向をCanvas上の矢印で示すコンポーネント（pixelRectベースでクランプ）。
    /// - ターゲットが画面内にある間は矢印を非表示
    /// - 画面外(またはカメラ背面)にあるとき、Canvas.pixelRect の内側に沿って矢印を表示
    /// - CanvasのRenderMode(Screen Space - Overlay / Camera / World Space)に対応
    /// </summary>
    public class BallOffScreenIndicator : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private Transform target;                 // 追従するターゲット(ボール)
        [SerializeField] private RectTransform arrowIndicator;     // Canvas上の矢印(上向きがデフォルト)
        [SerializeField] private Canvas canvas;                    // 矢印が属するCanvas
        [SerializeField] private Camera referenceCamera;           // 投影に使うカメラ(未指定ならMainCamera)

        // 全インスタンスの管理（自動配布用）
        private static readonly HashSet<BallOffScreenIndicator> s_instances = new HashSet<BallOffScreenIndicator>();

        [Header("表示設定")]
        [Tooltip("画面端からどれだけ内側に表示するか(ピクセル) - X=横, Y=縦")]
        [SerializeField] private Vector2 edgePadding = new Vector2(32f, 32f);

        [Tooltip("ターゲットが画面内かどうか判定する際のビューポートの余白(0〜0.5程度)。正の値で少し早めに表示/非表示が切り替わる")]
        [Range(0f, 0.2f)]
        [SerializeField] private float viewportMargin = 0.02f;

        [Tooltip("プレイヤーの Transform。プレイヤーとボールの距離が閾値を超れている場合、矢印を画面内でも表示します（未設定の場合、この機能は無効）")]
        [SerializeField] private Transform player;

        [Tooltip("プレイヤーとボールの距離がこの値（ワールド単位）より大きいとき、画面内でも矢印を表示する（player が設定されている場合のみ有効）")]
        [SerializeField] private float showIfFartherThan = 10f;

        [Tooltip("ボールの上に表示する矢印をボールの上端からこの高さだけワールド単位で離して表示します（正の値で矢印がさらに上に、負の値で下に寄せます）")]
        [SerializeField] private float verticalOffsetAboveBall = 0.15f;

        [Tooltip("ターゲットが未設定または参照が欠ける場合にコンポーネントを自動で無効化する")]
        [SerializeField] private bool disableIfReferencesMissing = true;

        private RectTransform canvasRect;

        private void Awake()
        {
            s_instances.Add(this);
            EnsureReferences();
        }

        private void OnDestroy()
        {
            s_instances.Remove(this);
        }

        private void Reset()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            // エディタ上での参照補完
            if (referenceCamera == null)
                referenceCamera = Camera.main;

            if (arrowIndicator != null && canvas == null)
                canvas = arrowIndicator.GetComponentInParent<Canvas>();

            canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        }

        private void EnsureReferences()
        {
            if (referenceCamera == null)
                referenceCamera = Camera.main;

            if (canvas == null && arrowIndicator != null)
                canvas = arrowIndicator.GetComponentInParent<Canvas>();

            canvasRect = canvas != null ? canvas.transform as RectTransform : null;

            // player が未設定なら自動検出を試みる
            if (player == null)
            {
                // 1) 参照カメラが既に設定されているなら、そのカメラを持つ PlayerController を優先して探す
                if (referenceCamera != null)
                {
                    var pcsForCam = FindObjectsOfType<global::PlayerController>();
                    if (pcsForCam != null && pcsForCam.Length > 0)
                    {
                        foreach (var pc in pcsForCam)
                        {
                            if (pc.playerCamera == referenceCamera)
                            {
                                player = pc.transform;
                                break;
                            }
                        }
                    }
                }

                // 2) カメラ一致が見つからなければタグ検索（Player タグが使われているケース）
                if (player == null)
                {
                    try
                    {
                        var go = GameObject.FindWithTag("Player");
                        if (go != null)
                            player = go.transform;
                    }
                    catch { /* タグが存在しない場合の例外を無視 */ }
                }

                // 3) それでも無ければ PlayerController を探し、Photon を使っている場合は IsMine のものを優先
                if (player == null)
                {
                    var pcs = FindObjectsOfType<global::PlayerController>();
                    if (pcs != null && pcs.Length > 0)
                    {
                        foreach (var pc in pcs)
                        {
                            // Photon を使っている場合は所有者を優先
                            var pv = pc.GetComponentInParent<Photon.Pun.PhotonView>();
                            if (pv != null && pv.IsMine)
                            {
                                player = pc.transform;
                                break;
                            }
                        }

                        if (player == null)
                        {
                            // 所有者が見つからなければ最初の PlayerController を使う
                            player = pcs[0].transform;
                        }
                    }
                }
            }

            // 参照が無い場合は矢印を隠し、必要なら自身を無効化
            if (arrowIndicator != null)
                arrowIndicator.gameObject.SetActive(false);

            // camera は RegisterCameraForAll で後設定され得るため、arrow と canvas のみ厳格チェック
            if (disableIfReferencesMissing && (arrowIndicator == null || canvas == null))
            {
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (target == null || arrowIndicator == null || canvasRect == null || referenceCamera == null)
            {
                if (arrowIndicator != null)
                    arrowIndicator.gameObject.SetActive(false);
                return;
            }

            // 画面内判定（カメラのビューポート基準）
            Vector3 viewport = referenceCamera.WorldToViewportPoint(target.position);
            bool behindCamera = viewport.z < 0f; // 背面にあるか

            bool insideViewport = !behindCamera &&
                                  viewport.x > 0f + viewportMargin && viewport.x < 1f - viewportMargin &&
                                  viewport.y > 0f + viewportMargin && viewport.y < 1f - viewportMargin;

            // スクリーン座標は on-screen 判定後にも必要になるため早めに計算しておく
            Vector3 screenPos = referenceCamera.WorldToScreenPoint(target.position);

            // 画面内判定後、プレイヤーとの距離条件で画面内でも表示するか決定
            bool isFar = false;
            if (player != null)
            {
                float sq = (player.position - target.position).sqrMagnitude;
                isFar = sq > (showIfFartherThan * showIfFartherThan);
            }

            if (insideViewport)
            {
                if (!isFar)
                {
                    if (arrowIndicator.gameObject.activeSelf)
                        arrowIndicator.gameObject.SetActive(false);
                    return;
                }

                // insideViewport && isFar: ボールの上に下向きの矢印を表示
                if (!arrowIndicator.gameObject.activeSelf)
                    arrowIndicator.gameObject.SetActive(true);

                Camera uiCamForOnScreen = null;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    uiCamForOnScreen = canvas.worldCamera != null ? canvas.worldCamera : referenceCamera;

                // ボールの「上端」を厳密に計算して矢印の下端がその点に来るように配置
                float worldRadius = 0f;
                var sc = target.GetComponentInChildren<SphereCollider>();
                if (sc != null)
                {
                    // SphereCollider.radius はローカルスケール基準。Collider の transform の lossyScale を使う。
                    float scale = (sc.transform.lossyScale.x + sc.transform.lossyScale.y + sc.transform.lossyScale.z) / 3f;
                    worldRadius = sc.radius * scale;
                }
                else
                {
                    var rend = target.GetComponentInChildren<Renderer>();
                    if (rend != null)
                        worldRadius = rend.bounds.extents.y;
                    else
                        worldRadius = 0.5f; // フォールバック
                }

                Vector3 ballTopWorld = target.position + Vector3.up * (worldRadius + verticalOffsetAboveBall);
                Vector3 ballTopScreen = referenceCamera.WorldToScreenPoint(ballTopWorld);


                // --- 横方向のクランプを Canvas ローカル空間で適用（edgePadding.x と矢印幅を考慮） ---
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    new Vector2(ballTopScreen.x, ballTopScreen.y),
                    uiCamForOnScreen,
                    out Vector2 canvasLocalPosOnTop
                );

                // edgePadding はピクセル単位なので、Canvas の scaleFactor でローカル単位に変換
                float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
                float edgePaddingLocal = edgePadding.x / Mathf.Max(1e-6f, canvasScale);

                float halfWidthLocal = canvasRect.rect.width * 0.5f;
                float arrowWidthLocal = arrowIndicator.rect.size.x;
                float pivotX = arrowIndicator.pivot.x;

                float leftLocal = -halfWidthLocal + edgePaddingLocal + arrowWidthLocal * pivotX;
                float rightLocal = halfWidthLocal - edgePaddingLocal - arrowWidthLocal * (1f - pivotX);

                if (leftLocal > rightLocal)
                {
                    // 余白が無理な場合は中央に寄せる
                    canvasLocalPosOnTop.x = 0f;
                }
                else
                {
                    canvasLocalPosOnTop.x = Mathf.Clamp(canvasLocalPosOnTop.x, leftLocal, rightLocal);
                }

                // 矢印をボールの上（ボトム）に合わせる: pivot とサイズを考慮
                Vector2 arrowSizeOnScreen = arrowIndicator.rect.size;
                Vector2 pivotOnScreen = arrowIndicator.pivot;
                float topMargin = 4f; // ボールと矢印の間の余白（ピクセル）

                // ピボットからボトムまでの距離 = arrowSize.y * pivot.y
                float pivotToBottom = arrowSizeOnScreen.y * pivotOnScreen.y;

                // ピボット位置は (ballTop + margin + pivotToBottom)
                arrowIndicator.anchoredPosition = canvasLocalPosOnTop + new Vector2(0f, topMargin + pivotToBottom);
                arrowIndicator.localRotation = Quaternion.Euler(0f, 0f, 180f); // 下向き
                return;
            }

            // 画面外: 矢印を表示して位置と向きを更新
            if (!arrowIndicator.gameObject.activeSelf)
                arrowIndicator.gameObject.SetActive(true);

            // ======== ここから pixelRect ベースのクランプ実装 ========

            // 1) スクリーン座標（全画面基準）
            // (screenPos は上で既に計算済み)

            // 2) クランプに使うピクセル矩形を取得（Canvas が無ければ camera.pixelRect → それも無ければ Screen 全体）
            Rect pxRect;
            if (canvas != null)
                pxRect = canvas.pixelRect;
            else if (referenceCamera != null)
                pxRect = referenceCamera.pixelRect;
            else
                pxRect = new Rect(0f, 0f, Screen.width, Screen.height);

            // 3) pixelRect ローカルへ原点合わせ（矩形の左下を (0,0) に）
            Vector2 rectPos = new Vector2(screenPos.x - pxRect.x, screenPos.y - pxRect.y);
            Vector2 rectCenter = new Vector2(pxRect.width * 0.5f, pxRect.height * 0.5f);

            // 4) 中心からターゲットへのベクトル
            Vector2 fromCenter = rectPos - rectCenter;

            // 背面なら直感的な向きにするため反転
            if (behindCamera)
                fromCenter = -fromCenter;

            if (fromCenter.sqrMagnitude < 1e-2f)
                fromCenter = Vector2.up; // ゼロ割回避

            // 5) 矩形内（edgePadding を除いた内側）との交点にクランプ
            // 注: Inspector の edgePadding は「矢印の外端（top/right 等）からの距離」を期待しているため
            // 矢印の pivot とサイズを考慮してピボット位置を補正する。
            Vector2 arrowSize = arrowIndicator.rect.size; // ローカルピクセル単位
            Vector2 pivot = arrowIndicator.pivot;

            // 各軸で、矢印の外端からピボットまでの追加距離を決定（上端/右端方向は 1 - pivot）
            float extraY = (fromCenter.y > 0f) ? (arrowSize.y * (1f - pivot.y)) : (arrowSize.y * pivot.y);
            float extraX = (fromCenter.x > 0f) ? (arrowSize.x * (1f - pivot.x)) : (arrowSize.x * pivot.x);

            float halfW = Mathf.Max(1f, pxRect.width * 0.5f - (edgePadding.x + extraX));
            float halfH = Mathf.Max(1f, pxRect.height * 0.5f - (edgePadding.y + extraY));

            float dx = Mathf.Abs(fromCenter.x);
            float dy = Mathf.Abs(fromCenter.y);
            float tx = dx > 1e-4f ? (halfW / dx) : float.PositiveInfinity;
            float ty = dy > 1e-4f ? (halfH / dy) : float.PositiveInfinity;
            float t = Mathf.Min(tx, ty);

            Vector2 clampedRectPos = rectCenter + fromCenter * t;

            // 6) 再びスクリーン座標系に戻す（pixelRect の原点を戻す）
            Vector2 clampedScreenPos = new Vector2(clampedRectPos.x + pxRect.x, clampedRectPos.y + pxRect.y);

            // 7) Canvas 座標(ローカル)に変換して配置
            Camera uiCam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                uiCam = null;
            }
            else
            {
                // Screen Space - Camera または World Space
                uiCam = canvas.worldCamera != null ? canvas.worldCamera : referenceCamera;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                clampedScreenPos,
                uiCam,
                out Vector2 canvasLocalPos
            );

            arrowIndicator.anchoredPosition = canvasLocalPos;

            // 矢印の回転（デフォルトが上向きのため 90 度引く）
            float angleDeg = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
            float zRot = angleDeg - 90f;
            arrowIndicator.localRotation = Quaternion.Euler(0f, 0f, zRot);
        }

        /// <summary>
        /// 追従ターゲットを動的に設定
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;

            // 参照が揃ったらコンポーネントを有効化
            if (target != null && arrowIndicator != null && canvasRect != null && referenceCamera != null)
            {
                enabled = true;
            }
        }

        /// <summary>
        /// 矢印の参照を動的に設定
        /// </summary>
        public void SetArrow(RectTransform newArrow)
        {
            arrowIndicator = newArrow;
            if (arrowIndicator != null && canvas == null)
                canvas = arrowIndicator.GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        }

        /// <summary>
        /// 参照カメラの設定
        /// </summary>
        public void SetCamera(Camera cam)
        {
            referenceCamera = cam;

            // canvasRect が未設定の場合、再取得を試みる
            if (canvasRect == null && canvas != null)
            {
                canvasRect = canvas.transform as RectTransform;
            }

            // カメラが設定されたら player の自動検出を再試行（参照カメラと同じプレイヤーを優先）
            EnsureReferences();

            // 参照が揃ったらコンポーネントを有効化
            if (target != null && arrowIndicator != null && canvasRect != null && referenceCamera != null)
            {
                enabled = true;
            }
        }

        /// <summary>
        /// シーン内すべての BallOffScreenIndicator にボール Transform を一括配布
        /// </summary>
        public static void RegisterBallForAll(Transform ball)
        {
            foreach (var inst in s_instances)
            {
                if (inst == null) continue;
                inst.SetTarget(ball);
            }
        }

        /// <summary>
        /// シーン内すべての BallOffScreenIndicator にカメラを一括配布
        /// </summary>
        public static void RegisterCameraForAll(Camera cam)
        {
            foreach (var inst in s_instances)
            {
                if (inst == null) continue;
                inst.SetCamera(cam);
            }
        }
    }
}
