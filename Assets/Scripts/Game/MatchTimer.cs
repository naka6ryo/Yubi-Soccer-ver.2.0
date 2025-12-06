using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace YubiSoccer.Game
{
    /// <summary>
    /// 試合用のカウントダウンタイマー。
    /// PhotonNetwork.ServerTimestamp を使用して全クライアントで時間を同期します。
    /// </summary>
    public class MatchTimer : MonoBehaviourPunCallbacks
    {
        /// <summary>
        /// 試合終了時に発火するイベント
        /// </summary>
        public static event Action OnMatchFinished;
        [Header("Timer Settings")]
        [Tooltip("タイマーの長さ（秒）。StartTimer() で使用されます")]
        [SerializeField, Min(0f)] private float durationSeconds = 60f;
        [Tooltip("0秒になったときにテキストを消す")]
        [SerializeField] private bool clearTextWhenFinished = false;

        [Header("UI Display (Optional)")]
        [Tooltip("UGUI 用 TextMeshProUGUI（Canvas 上）")]
        [SerializeField] private TextMeshProUGUI tmpTextUGUI;
        [Tooltip("ワールド空間用 TextMeshPro (3D)")]
        [SerializeField] private TextMeshPro tmpText3D;
        [Tooltip("UI.Text フォールバック（TMP未使用時）")]
        [SerializeField] private Text uiText;

        // ルームプロパティのキー
        private const string START_TIME_KEY = "StartTime";
        private const string DURATION_KEY = "Duration";

        public float RemainingSeconds { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; } = true;

        private bool isFinished = false;

        private int _cachedStartTime;
        private float _cachedDuration;

        public override void OnEnable()
        {
            base.OnEnable();
            // 途中参加時などに現在の状態を確認
            CheckTimerState();
        }

        private void Update()
        {
            if (IsPaused) return;

            if (IsRunning)
            {
                UpdateTimer();
            }
        }

        /// <summary>
        /// タイマーを開始（マスタークライアントのみ実行可能）
        /// </summary>
        public void StartTimer()
        {
            StartTimer(durationSeconds);
        }

        /// <summary>
        /// 指定秒数で開始（マスタークライアントのみ実行可能）
        /// </summary>
        public void StartTimer(float seconds)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int startTime = PhotonNetwork.ServerTimestamp;
            var props = new Hashtable
            {
                { START_TIME_KEY, startTime },
                { DURATION_KEY, seconds }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            
            Debug.Log($"[MatchTimer] Master started timer: duration={seconds:F1}s, start={startTime}");
        }

                public void StopTimer()
                {
                    IsPaused = true;
                    IsRunning = false;
                    
                    // 停止＝プロパティ削除または無効値をセット
                    if (!PhotonNetwork.IsMasterClient) return;
        
                    // StartTimeを削除して停止扱いにする
                    var props = new Hashtable
                    {
                        { START_TIME_KEY, null },
                        { DURATION_KEY, null }
                    };
                    PhotonNetwork.CurrentRoom.SetCustomProperties(props);
                }
        
                public void ClearTimerText()
                {
                    SetText("");
                }
        
                public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
                {
                    if (propertiesThatChanged.ContainsKey(START_TIME_KEY) || propertiesThatChanged.ContainsKey(DURATION_KEY))
                    {
                        CheckTimerState();
                    }
                }
        
                private void CheckTimerState()
                {
                    if (!PhotonNetwork.InRoom) return;
        
                    var props = PhotonNetwork.CurrentRoom.CustomProperties;
                    if (props.TryGetValue(START_TIME_KEY, out object startObj) && props.TryGetValue(DURATION_KEY, out object durObj))
                    {
                        int startTime = (int)startObj;
                        float duration = (float)durObj;
        
                        _cachedStartTime = startTime;
                        _cachedDuration = duration;
        
                        // 開始時刻と現在時刻から残り時間を計算
                        int elapsedMs = unchecked(PhotonNetwork.ServerTimestamp - startTime);
                        float elapsedSec = elapsedMs / 1000f;
                        RemainingSeconds = duration - elapsedSec;
        
                        if (RemainingSeconds > 0)
                        {
                            IsRunning = true;
                            isFinished = false;
                            IsPaused = false; // タイマーが正常に開始できるので、ここで一時停止を解除
                            UpdateText(RemainingSeconds);
                        }
                        else
                        {
                            // 既に終わっている
                            FinishTimer();
                        }
                    }
                    else
                    {
                        // タイマー情報がない＝停止中
                        IsRunning = false;
                        // 表示をクリアするか、初期状態にするかは要件次第だが、ここでは何もしないかクリア
                        // ClearTimerText(); 
                    }
                }
        
                private void UpdateTimer()
                {
                    // _cachedStartTimeと_cachedDurationが設定されていることを前提とする
                    // CheckTimerState()で初期化される想定
                    if (!IsRunning) return; // タイマーが実行中でない場合は何もしない
        
                    int elapsedMs = unchecked(PhotonNetwork.ServerTimestamp - _cachedStartTime);
                    float elapsedSec = elapsedMs / 1000f;
                    RemainingSeconds = _cachedDuration - elapsedSec;
        
                    if (RemainingSeconds <= 0f)
                    {
                        RemainingSeconds = 0f;
                        FinishTimer();
                    }
                    
                    UpdateText(RemainingSeconds);
                }
        
                private void FinishTimer()
                {
                    IsRunning = false;
                    if (isFinished) return; // 既に終了処理済みならスキップ
        
                    isFinished = true;
                    UpdateText(0f);
        
                    if (clearTextWhenFinished)
                    {
                        SetText("");
                    }
                    Debug.Log("[MatchTimer] Finished (Synced)");
        
                    // 試合終了イベントを発火
                    OnMatchFinished?.Invoke();
                }
        private void UpdateText(float remain)
        {
            int sec = Mathf.CeilToInt(remain);
            if (sec < 0) sec = 0;
            int m = sec / 60;
            int s = sec % 60;
            string text = $"{m:00}:{s:00}";
            SetText(text);
        }

        private void SetText(string text)
        {
            // 優先順位: TMP UGUI → TMP 3D → UI.Text
            if (tmpTextUGUI != null)
            {
                tmpTextUGUI.text = text;
                return;
            }
            if (tmpText3D != null)
            {
                tmpText3D.text = text;
                return;
            }
            if (uiText != null)
            {
                uiText.text = text;
            }
        }
    }
}
