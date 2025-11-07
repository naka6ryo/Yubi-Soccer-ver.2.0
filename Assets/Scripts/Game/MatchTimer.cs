using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YubiSoccer.Game
{
    /// <summary>
    /// 試合用のカウントダウンタイマー。
    /// - Inspector から時間(秒)を設定
    /// - TextMeshProUGUI / TextMeshPro(3D) / UI.Text に mm:ss 形式で表示
    /// - Start/Stop/Clear を外部から呼び出し可能
    /// </summary>
    public class MatchTimer : MonoBehaviour
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

        private Coroutine timerRoutine;
        public float RemainingSeconds { get; private set; }
        public bool IsRunning => timerRoutine != null;

        /// <summary>
        /// Inspector の durationSeconds で開始
        /// </summary>
        public void StartTimer()
        {
            try { Debug.Log($"[MatchTimer] StartTimer() duration={durationSeconds:F1}s"); } catch { }
            StartTimer(durationSeconds);
        }

        /// <summary>
        /// 指定秒数で開始
        /// </summary>
        public void StartTimer(float seconds)
        {
            if (timerRoutine != null)
            {
                StopCoroutine(timerRoutine);
                timerRoutine = null;
            }
            try { Debug.Log($"[MatchTimer] StartTimer({seconds:F1})"); } catch { }
            timerRoutine = StartCoroutine(CoRunTimer(seconds));
        }

        public void StopTimer()
        {
            if (timerRoutine != null)
            {
                StopCoroutine(timerRoutine);
                timerRoutine = null;
            }
            try { Debug.Log("[MatchTimer] StopTimer()"); } catch { }
        }

        public void ClearTimerText()
        {
            SetText("");
        }

        private IEnumerator CoRunTimer(float seconds)
        {
            RemainingSeconds = Mathf.Max(0f, seconds);
            UpdateText(RemainingSeconds);
            yield return null;

            while (RemainingSeconds > 0f)
            {
                RemainingSeconds -= Time.deltaTime;
                if (RemainingSeconds < 0f) RemainingSeconds = 0f;
                UpdateText(RemainingSeconds);
                yield return null;
            }

            if (clearTextWhenFinished)
            {
                SetText("");
            }
            try { Debug.Log("[MatchTimer] Finished"); } catch { }

            // 試合終了イベントを発火
            OnMatchFinished?.Invoke();

            timerRoutine = null;
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
