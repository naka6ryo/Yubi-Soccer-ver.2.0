using UnityEngine;
using System.Collections.Generic;

namespace YubiSoccer.UI
{
    /// <summary>
    /// 画面外にあるターゲット(ボール)の方向をCanvas上の矢印で示すコンポーネント。
    /// - ターゲットが画面内にある間は矢印を非表示
    /// - 画面外(またはカメラ背面)にあるとき、画面端に沿って矢印を表示
    /// - CanvasのRenderMode(Screen Space - Overlay / Camera)に対応
    /// </summary>
    public class BallOffScreenIndicator : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private Transform target; // 追従するターゲット(ボール)
        [SerializeField] private RectTransform arrowIndicator; // Canvas上の矢印(上向きがデフォルト)
        [SerializeField] private Canvas canvas; // 矢印が属するCanvas
        [SerializeField] private Camera referenceCamera; // 投影に使うカメラ(未指定ならMainCamera)

        // 全インスタンスの管理（自動配布用）
        private static readonly HashSet<BallOffScreenIndicator> s_instances = new HashSet<BallOffScreenIndicator>();

        [Header("表示設定")]
        [Tooltip("画面端からどれだけ内側に表示するか(ピクセル)")]
        [SerializeField] private float edgePadding = 32f;

        [Tooltip("ターゲットが画面内かどうか判定する際のビューポートの余白(0〜0.5程度)。正の値で少し早めに表示/非表示が切り替わる")]
        [Range(0f, 0.2f)]
        [SerializeField] private float viewportMargin = 0.02f;

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

            // 参照が無い場合は矢印を隠し、必要なら自身を無効化
            if (arrowIndicator != null)
                arrowIndicator.gameObject.SetActive(false);

            // カメラは後から RegisterCameraForAll で設定される可能性があるため、
            // arrow と canvas のみチェック（camera は動的に設定される）
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

            // 画面内判定
            Vector3 viewport = referenceCamera.WorldToViewportPoint(target.position);
            bool behindCamera = viewport.z < 0f; // 背面にあるか

            bool insideViewport = !behindCamera &&
                                  viewport.x > 0f + viewportMargin && viewport.x < 1f - viewportMargin &&
                                  viewport.y > 0f + viewportMargin && viewport.y < 1f - viewportMargin;

            if (insideViewport)
            {
                if (arrowIndicator.gameObject.activeSelf)
                    arrowIndicator.gameObject.SetActive(false);
                return;
            }

            // 画面外: 矢印を表示して位置と向きを更新
            if (!arrowIndicator.gameObject.activeSelf)
                arrowIndicator.gameObject.SetActive(true);
            ;

            Vector3 screenPos = referenceCamera.WorldToScreenPoint(target.position);
            float screenW = Screen.width;
            float screenH = Screen.height;
            Vector2 screenCenter = new Vector2(screenW * 0.5f, screenH * 0.5f);

            // 画面中心からターゲットへのベクトル
            Vector2 fromCenter = (Vector2)screenPos - screenCenter;

            // 背面にある場合は方向を反転させることで直感的な向きにする
            if (behindCamera)
                fromCenter = -fromCenter;

            if (fromCenter.sqrMagnitude < 1e-2f)
                fromCenter = Vector2.up; // ゼロ割回避用の適当な向き

            // 画面端(パディングを除いた矩形)との交点にクランプ
            float halfW = Mathf.Max(1f, screenW * 0.5f - edgePadding);
            float halfH = Mathf.Max(1f, screenH * 0.5f - edgePadding);

            float dx = Mathf.Abs(fromCenter.x);
            float dy = Mathf.Abs(fromCenter.y);
            float tx = dx > 1e-4f ? (halfW / dx) : float.PositiveInfinity;
            float ty = dy > 1e-4f ? (halfH / dy) : float.PositiveInfinity;
            float t = Mathf.Min(tx, ty);

            Vector2 clampedScreenPos = screenCenter + fromCenter * t;

            // Canvas座標(ローカル/アンカー)に変換
            Vector2 canvasLocalPos;
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
                out canvasLocalPos
            );

            arrowIndicator.anchoredPosition = canvasLocalPos;

            // 矢印の回転(デフォルトが上向きのため、X軸基準角度から90度引く)
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
        }        /// <summary>
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

            // 参照が揃ったらコンポーネントを有効化
            if (target != null && arrowIndicator != null && canvasRect != null && referenceCamera != null)
            {
                enabled = true;
            }
        }        /// <summary>
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

