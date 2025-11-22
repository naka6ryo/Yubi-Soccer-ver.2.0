using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

namespace YubiSoccer.UI
{
    /// <summary>
    /// Photon接続完了後、指定シーンにフェード遷移する
    /// </summary>
    public class NetworkSceneTransition : MonoBehaviourPunCallbacks
    {
        [Header("Scene Settings")]
        [SerializeField] private string targetSceneName = "GameTitleEdition";
        [SerializeField] private bool transitionOnConnected = true;

        [Header("Fade Settings")]
        [SerializeField] private Image fadeImage;
        [SerializeField] private Color fadeColor = Color.black;
        [SerializeField] private float fadeOutDuration = 1f; // 暗転時間
        [SerializeField] private float fadeInDuration = 1f;  // 明転時間
        [SerializeField] private float delayBeforeTransition = 0.5f; // 接続完了後の待機時間

        [Header("Loading UI")]
        [SerializeField] private GameObject loadingPanel; // 遷移時に非表示にする

        private bool isTransitioning = false;
        private bool videoFinished = false; // 動画終了フラグ
        private bool connectedToMaster = false; // サーバー接続完了フラグ

        private void Awake()
        {
            // フェードImageを初期状態（透明）にする
            if (fadeImage != null)
            {
                Color transparent = fadeColor;
                transparent.a = 0f;
                fadeImage.color = transparent;
                fadeImage.gameObject.SetActive(true);
            }
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("[NetworkSceneTransition] Connected to Master Server!");
            connectedToMaster = true;

            // 動画終了済みかつ接続完了したらシーン遷移
            if (transitionOnConnected && !isTransitioning && videoFinished)
            {
                Debug.Log("[NetworkSceneTransition] Video finished and connected. Starting transition...");
                StartCoroutine(TransitionToScene());
            }
            else if (!videoFinished)
            {
                Debug.Log("[NetworkSceneTransition] Waiting for video to finish...");
            }
        }

        /// <summary>
        /// 動画終了時に VideoPlayerController から呼ばれる
        /// </summary>
        public void OnVideoFinished()
        {
            Debug.Log("[NetworkSceneTransition] Video finished!");
            videoFinished = true;

            // 接続済みならシーン遷移開始
            if (connectedToMaster && transitionOnConnected && !isTransitioning)
            {
                Debug.Log("[NetworkSceneTransition] Connected and video finished. Starting transition...");
                StartCoroutine(TransitionToScene());
            }
            else if (!connectedToMaster)
            {
                Debug.Log("[NetworkSceneTransition] Waiting for connection to Master Server...");
            }
        }

        /// <summary>
        /// シーン遷移を実行
        /// </summary>
        private IEnumerator TransitionToScene()
        {
            isTransitioning = true;

            // 接続完了後、少し待機
            if (delayBeforeTransition > 0f)
            {
                yield return new WaitForSeconds(delayBeforeTransition);
            }

            // ローディングパネルを非表示
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            // フェードアウト（暗転）
            yield return StartCoroutine(FadeOut());

            // シーン遷移
            Debug.Log($"[NetworkSceneTransition] Loading scene: {targetSceneName}");
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);

            // シーンロード完了まで待機
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // フェードイン（明転）は次のシーンで行うか、ここで行うか選択可能
            // ここではフェードインまで行う
            yield return StartCoroutine(FadeIn());

            // フェードImageを非表示
            if (fadeImage != null)
            {
                fadeImage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// フェードアウト（暗転）
        /// </summary>
        private IEnumerator FadeOut()
        {
            if (fadeImage == null)
            {
                Debug.LogWarning("[NetworkSceneTransition] Fade Image is not assigned!");
                yield break;
            }

            fadeImage.gameObject.SetActive(true);

            float elapsed = 0f;
            Color startColor = fadeColor;
            startColor.a = 0f;
            Color endColor = fadeColor;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                fadeImage.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }

            fadeImage.color = endColor;
        }

        /// <summary>
        /// フェードイン（明転）
        /// </summary>
        private IEnumerator FadeIn()
        {
            if (fadeImage == null)
            {
                yield break;
            }

            float elapsed = 0f;
            Color startColor = fadeColor;
            Color endColor = fadeColor;
            endColor.a = 0f;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                fadeImage.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }

            fadeImage.color = endColor;
        }

        /// <summary>
        /// 手動でシーン遷移を開始
        /// </summary>
        public void StartTransition()
        {
            if (!isTransitioning)
            {
                StartCoroutine(TransitionToScene());
            }
        }
    }
}
