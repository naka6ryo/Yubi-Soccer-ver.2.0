using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using Photon.Pun;

namespace YubiSoccer.UI
{
    /// <summary>
    /// マッチング中に「現在入っている人数/ルーム最大人数」を表示するシンプルなコンポーネント。
    /// 表示例: "2/4"。
    /// - `playersText` に `TMP_Text` をセットしてください（TMP 未使用時は `legacyPlayersText` に通常の Text を割り当ててください）。
    /// - オプションで有効化時に自動で更新を開始します。
    /// </summary>
    public class MatchingPlayersDisplay : MonoBehaviour
    {
        [Tooltip("表示対象の TextMeshPro テキストコンポーネント（優先して使われます）")]
        [SerializeField] private TMP_Text playersText;
        [Tooltip("表示対象の標準 UI Text（TMP が未割当のときに使われます）")]
        [SerializeField] private Text legacyPlayersText;

        [Tooltip("更新頻度（秒）")]
        [SerializeField] private float updateInterval = 0.5f;

        [Tooltip("有効化時に自動で更新を開始するか（true の場合 OnEnable で開始、OnDisable で停止）")]
        [SerializeField] private bool autoStart = true;

        private Coroutine updateCoroutine;

        private void OnEnable()
        {
            if (autoStart) StartUpdating();
        }

        private void OnDisable()
        {
            if (autoStart) StopUpdating();
        }

        /// <summary>
        /// 更新を開始する（外部から手動で開始したい場合に呼ぶ）
        /// </summary>
        public void StartUpdating()
        {
            if (playersText == null && legacyPlayersText == null) return;
            if (updateCoroutine == null) updateCoroutine = StartCoroutine(CoUpdateLoop());
        }

        /// <summary>
        /// 更新を停止する
        /// </summary>
        public void StopUpdating()
        {
            if (updateCoroutine != null)
            {
                try { StopCoroutine(updateCoroutine); } catch { }
                updateCoroutine = null;
            }
        }

        private IEnumerator CoUpdateLoop()
        {
            while (true)
            {
                UpdateDisplay();
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, updateInterval));
            }
        }

        /// <summary>
        /// 即座に表示を更新する（UI から直接呼べます）
        /// </summary>
        public void UpdateDisplay()
        {
            if (playersText == null && legacyPlayersText == null) return;
            string outText = "-/-";
            try
            {
                if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
                {
                    int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
                    int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
                    outText = $"{playerCount}/{maxPlayers}";
                }
            }
            catch
            {
                outText = "-/-";
            }

            if (playersText != null)
            {
                playersText.text = outText;
            }
            else if (legacyPlayersText != null)
            {
                legacyPlayersText.text = outText;
            }
        }
    }
}
