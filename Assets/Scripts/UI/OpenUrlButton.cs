using System;
using UnityEngine;

namespace YubiSoccer.UI
{
    /// <summary>
    /// ボタンから外部 URL を開くためのヘルパーコンポーネント。
    /// - インスペクタの `url` を設定し、Button の OnClick に `OpenUrl()` を割り当ててください。
    /// - 静的メソッド `OpenUrlStatic(string)` も利用可能です。
    /// </summary>
    public class OpenUrlButton : MonoBehaviour
    {
        [Tooltip("外部リンク先の URL を入力してください（例: https://example.com）。")]
        public string url;

        [Tooltip("デバッグログを出力するか。")]
        public bool verbose = true;

        /// <summary>
        /// インスペクタ / UnityEvent から呼び出す用。インスペクタで url を設定しておくこと。
        /// </summary>
        public void OpenUrl()
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                if (verbose) Debug.LogWarning("[OpenUrlButton] URL が設定されていません。");
                return;
            }

            if (verbose) Debug.Log($"[OpenUrlButton] Opening URL: {url}");
            Application.OpenURL(url);
        }

        /// <summary>
        /// コード側から直接 URL を開く場合に使用可能な静的ヘルパー。
        /// </summary>
        public static void OpenUrlStatic(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogWarning("[OpenUrlButton] OpenUrlStatic: URL が設定されていません。");
                return;
            }
            Debug.Log($"[OpenUrlButton] OpenUrlStatic Opening URL: {url}");
            Application.OpenURL(url);
        }
    }
}