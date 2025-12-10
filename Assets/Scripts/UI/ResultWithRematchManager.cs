using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using YubiSoccer.Game;

namespace YubiSoccer.UI
{
    /// <summary>
    /// リザルト表示と再戦機能全体を統括するマネージャー
    /// </summary>
    public class ResultWithRematchManager : MonoBehaviourPunCallbacks
    {
        [Header("Scene Names")]
        [SerializeField] private string gameSceneName = "Matching";
        [SerializeField] private string titleSceneName = "GameTitleEdition";

        [Header("Dependencies")]
        [SerializeField] private ResultUI resultUI;
        [SerializeField] private RematchNetworkHandler networkHandler;

        [Header("Button References")]
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button titleButton;

        private void OnEnable()
        {
            // MatchTimer の試合終了イベントを購読
            MatchTimer.OnMatchFinished += HandleMatchFinished;
        }

        private void OnDisable()
        {
            // イベント購読解除
            MatchTimer.OnMatchFinished -= HandleMatchFinished;
        }

        private void Start()
        {
            // UIのボタンイベントを登録
            if (rematchButton != null)
                resultUI.RegisterRematchButton(rematchButton);
            if (titleButton != null)
                resultUI.RegisterTitleButton(titleButton);

            // UIイベントの購読
            resultUI.OnRematchRequested += HandleRematchRequested;
            resultUI.OnTitleRequested += HandleTitleRequested;

            // ネットワークイベントの購読
            networkHandler.OnAllPlayersReady += HandleAllPlayersReady;
            networkHandler.OnPlayerLeft += HandlePlayerLeft;
        }

        private void OnDestroy()
        {
            // イベント購読解除（メモリリーク防止）
            if (resultUI != null)
            {
                resultUI.OnRematchRequested -= HandleRematchRequested;
                resultUI.OnTitleRequested -= HandleTitleRequested;
            }

            if (networkHandler != null)
            {
                networkHandler.OnAllPlayersReady -= HandleAllPlayersReady;
                networkHandler.OnPlayerLeft -= HandlePlayerLeft;
            }
        }

        // ---------------------------------------------------
        // イベントハンドラー
        // ---------------------------------------------------

        /// <summary>
        /// 試合終了時の処理
        /// </summary>
        private void HandleMatchFinished()
        {
            // ScoreManager からスコア情報を取得
            var scoreManager = ScoreManager.Instance;
            if (scoreManager == null)
            {
                Debug.LogError("[ResultRematchManager] ScoreManager not found!");
                return;
            }

            int redScore = scoreManager.GetScore(Team.TeamA);
            int blueScore = scoreManager.GetScore(Team.TeamB);

            // リザルトUIを表示
            resultUI.ShowResult(redScore, blueScore);
        }

        /// <summary>
        /// 再戦ボタンがクリックされた時の処理
        /// </summary>
        private void HandleRematchRequested()
        {
            resultUI.ShowWaitingForPlayers();
            networkHandler.SetRematchStatus(true);
        }

        /// <summary>
        /// タイトルボタンがクリックされた時の処理
        /// </summary>
        private void HandleTitleRequested()
        {
            networkHandler.RequestBackToTitle();
        }

        /// <summary>
        /// 全プレイヤーが再戦準備完了した時の処理
        /// </summary>
        private void HandleAllPlayersReady()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                // 再戦時もスコアとタイマーをリセットする
                if (ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.ResetScores();
                }
                
                // タイマー情報とスコア（ScoreManagerがない場合の保険）をリセット
                var props = new ExitGames.Client.Photon.Hashtable 
                { 
                    { "ScoreA", 0 },
                    { "ScoreB", 0 },
                    { "StartTime", null },
                    { "Duration", null }
                };
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);

                PhotonNetwork.LoadLevel(gameSceneName);
            }
        }

        /// <summary>
        /// プレイヤーが退出した時の処理
        /// </summary>
        private void HandlePlayerLeft()
        {
            // 相手が退出したら自分も退出してタイトルへ（全員解散）
            PhotonNetwork.LeaveRoom();
        }

        // ---------------------------------------------------
        // Photonコールバック（部屋から出た後の処理）
        // ---------------------------------------------------

        public override void OnLeftRoom()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(titleSceneName);
        }
    }
}