using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

namespace YubiSoccer.Network
{
    /// <summary>
    /// Photon ルームにいる場合は LeaveRoom を待ってからシーンをロードする安全なローダー。
    /// WebGL で退室コールバックが来ない場合に備えタイムアウトでフォールバックします。
    /// シーンに 1 個置いておくか、NetworkManager の子にアタッチしてください。
    /// </summary>
    public class SceneLoader : MonoBehaviourPunCallbacks
    {
        public static SceneLoader Instance { get; private set; }

        [Tooltip("ルーム退室待ちのタイムアウト秒数")]
        [SerializeField] private float leaveTimeoutSeconds = 5f;

        private string pendingSceneName;
        private Coroutine timeoutCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 安全にシーンをロードする（Photon ルームに入っていれば LeaveRoom -> OnLeftRoom でロード）。
        /// </summary>
        public void LoadSceneSafely(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (PhotonNetwork.InRoom)
            {
                Debug.Log($"[SceneLoader] InRoom -> leave first then load '{sceneName}'");
                pendingSceneName = sceneName;
                try
                {
                    PhotonNetwork.LeaveRoom();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SceneLoader] LeaveRoom threw: {ex}. Loading scene immediately as fallback.");
                    ForceLoadPendingScene();
                    return;
                }

                if (timeoutCoroutine != null) StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = StartCoroutine(LeaveTimeout());
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        public override void OnLeftRoom()
        {
            if (!string.IsNullOrEmpty(pendingSceneName))
            {
                Debug.Log($"[SceneLoader] OnLeftRoom -> loading '{pendingSceneName}'");
                if (timeoutCoroutine != null) { StopCoroutine(timeoutCoroutine); timeoutCoroutine = null; }
                SceneManager.LoadScene(pendingSceneName);
                pendingSceneName = null;
            }
        }

        private IEnumerator LeaveTimeout()
        {
            yield return new WaitForSeconds(leaveTimeoutSeconds);
            Debug.LogWarning("[SceneLoader] LeaveRoom timeout; loading pending scene anyway.");
            ForceLoadPendingScene();
        }

        private void ForceLoadPendingScene()
        {
            if (!string.IsNullOrEmpty(pendingSceneName))
            {
                SceneManager.LoadScene(pendingSceneName);
                pendingSceneName = null;
            }
        }
    }
}