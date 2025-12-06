using UnityEngine;
using UnityEngine.UI;

namespace YubiSoccer.UI
{
    /// <summary>
    /// UI Image を左右に途切れなく流すコンポーネント。
    /// - 対象 Image の Sprite を複製して左右に並べ、親のローカル空間で移動させることでシームレスに見せます。
    /// - 回転（RectTransform.localRotation）を親に設定している場合でも、その回転に沿って動作します。
    /// - Inspector で速度（ピクセル/秒）を指定できます。正の値で右方向、負で左方向に流れます。
    /// 注意: 使用するスプライトは水平方向で継ぎ目なく連続できる (タイル可能) であることを推奨します。
    /// </summary>
    [DisallowMultipleComponent]
    public class ContinuousScrollImage : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("スクロールさせる元の Image。未設定なら同 GameObject の Image を使います。")]
        [SerializeField] private Image sourceImage;

        [Header("Scroll Settings")]
        [Tooltip("流れる速さ（ピクセル/秒）。正の値で右方向、負の値で左方向。デフォルトは右→左を表す負の値です。")]
        [SerializeField] private float speed = -100f;
        [Tooltip("タイル間の間隔（ピクセル）。0 の場合は隙間無しで連続表示します。")]
        [SerializeField] private float tileSpacing = 0f;
        [Tooltip("OnEnable 時に自動で再生するか")]
        [SerializeField] private bool playOnEnable = true;
        [Tooltip("時間に unscaledTime を使う（ポーズ中も進める）")]
        [SerializeField] private bool useUnscaledTime = false;

        // 内部タイル
        private RectTransform _containerRt;
        private RectTransform _tileA;
        private RectTransform _tileB;
        private Image _imgA;
        private Image _imgB;

        private float _width; // 親の幅（px）
        private bool _playing = false;

        void Reset()
        {
            speed = -100f; // デフォルトは右から左へ流す
            tileSpacing = 0f;
            playOnEnable = true;
        }

        void Awake()
        {
            if (sourceImage == null) sourceImage = GetComponent<Image>();
            _containerRt = GetComponent<RectTransform>();
            EnsureTiles();
        }

        void OnEnable()
        {
            if (playOnEnable) Play();
        }

        void OnDisable()
        {
            Pause();
        }

        void Update()
        {
            if (!_playing) return;

            if (_containerRt == null) return;

            // If rect width changed (e.g. layout), rebuild tiles layout
            float w = _containerRt.rect.width;
            if (!Mathf.Approximately(w, _width))
            {
                _width = w;
                LayoutTiles();
            }

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float delta = speed * dt;

            // move tiles in local space (x axis)
            _tileA.localPosition += new Vector3(delta, 0f, 0f);
            _tileB.localPosition += new Vector3(delta, 0f, 0f);

            // Wrap tiles to maintain seamless loop
            // Wrap tiles to maintain seamless loop using total width (tile width + spacing)
            float totalW = _width + tileSpacing;
            if (totalW <= 0f) totalW = _width;

            if (speed > 0f)
            {
                if (_tileA.localPosition.x >= totalW) _tileA.localPosition -= new Vector3(totalW * 2f, 0f, 0f);
                if (_tileB.localPosition.x >= totalW) _tileB.localPosition -= new Vector3(totalW * 2f, 0f, 0f);
            }
            else if (speed < 0f)
            {
                if (_tileA.localPosition.x <= -totalW) _tileA.localPosition += new Vector3(totalW * 2f, 0f, 0f);
                if (_tileB.localPosition.x <= -totalW) _tileB.localPosition += new Vector3(totalW * 2f, 0f, 0f);
            }
        }

        private void EnsureTiles()
        {
            // Create child tiles if missing
            Transform tA = transform.Find("_ScrollTileA");
            Transform tB = transform.Find("_ScrollTileB");

            if (tA == null)
            {
                GameObject go = new GameObject("_ScrollTileA", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                tA = go.transform;
            }
            if (tB == null)
            {
                GameObject go = new GameObject("_ScrollTileB", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                tB = go.transform;
            }

            _tileA = tA as RectTransform;
            _tileB = tB as RectTransform;

            _imgA = _tileA.GetComponent<Image>();
            _imgB = _tileB.GetComponent<Image>();

            // Copy sprite and settings from sourceImage
            if (sourceImage != null)
            {
                _imgA.sprite = sourceImage.sprite;
                _imgA.type = sourceImage.type;
                _imgA.preserveAspect = sourceImage.preserveAspect;
                _imgA.color = sourceImage.color;

                _imgB.sprite = sourceImage.sprite;
                _imgB.type = sourceImage.type;
                _imgB.preserveAspect = sourceImage.preserveAspect;
                _imgB.color = sourceImage.color;

                // Hide original image renderer so only tiles are visible
                sourceImage.enabled = false;
            }

            // Initial layout
            _width = _containerRt.rect.width;
            LayoutTiles();
        }

        private void LayoutTiles()
        {
            if (_tileA == null || _tileB == null || _containerRt == null) return;

            // Set anchors to top-left stretched vertically for stable layout
            SetupTileRect(_tileA);
            SetupTileRect(_tileB);

            // Place tiles side-by-side in local space with configurable spacing
            _tileA.localPosition = new Vector3(0f, 0f, 0f);
            _tileB.localPosition = new Vector3(_width + tileSpacing, 0f, 0f);
        }

        private void SetupTileRect(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(_width, 0f); // stretch vertically
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity; // child rotation kept identity; parent rotation controls tilt
        }

        /// <summary>再生開始</summary>
        public void Play()
        {
            _playing = true;
            // ensure tiles exist
            EnsureTiles();
        }

        /// <summary>停止（現在位置を維持）</summary>
        public void Pause()
        {
            _playing = false;
        }

        /// <summary>速度を設定する（ピクセル/秒）</summary>
        public void SetSpeed(float newSpeed)
        {
            speed = newSpeed;
        }

        void OnValidate()
        {
            if (speed == 0f) speed = 0f; // allow zero
            if (_containerRt == null) _containerRt = GetComponent<RectTransform>();
        }
    }
}
