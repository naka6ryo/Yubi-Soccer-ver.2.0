using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Photon.Pun.UtilityScripts;
using YubiSoccer.UI;
using System.Collections;

/// <summary>
/// Multi Player シーン内で各クライアントのロード完了を通知し、
/// マスターが全員ロード完了を確認したらネットワーク同期カウントダウンを開始する。
/// Multi Player シーンの任意の GameObject（例: GameManager）にアタッチしてください。
/// </summary>
public class GameStartCoordinator : MonoBehaviourPunCallbacks
{
    const string PROP_PLAYER_LOADED = "playerLoaded";
    const string PROP_COUNTDOWN_START = "countdownStartTime";
    [Tooltip("カウントダウン開始までの猶予時間（秒）— ネットワーク遅延を吸収")]
    [SerializeField] private float countdownStartDelay = 1.0f;
    public CountdownUI countDown;

    private SoundManager soundManager;

    void Start()
    {
        // 自分の読み込み完了をルームのプレイヤープロパティに設定
        var props = new ExitGames.Client.Photon.Hashtable { { PROP_PLAYER_LOADED, true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log("[GameStartCoordinator] Set playerLoaded=true");
        soundManager = SoundManager.Instance;

        // マスターは即チェック（既に全員揃っている可能性がある）
        if (PhotonNetwork.IsMasterClient)
        {
            CheckAllPlayersLoadedAndStart();
        }
        else
        {
            // 非マスターは既にカウントダウンが始まっているかチェック
            CheckCountdownStart();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (changedProps.ContainsKey(PROP_PLAYER_LOADED))
        {
            Debug.Log($"[GameStartCoordinator] PlayerPropertiesUpdate: {targetPlayer.NickName} loaded={changedProps[PROP_PLAYER_LOADED]}");
            CheckAllPlayersLoadedAndStart();
        }
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        // 全クライアントがルームプロパティ更新を受け取る
        if (propertiesThatChanged.ContainsKey(PROP_COUNTDOWN_START))
        {
            Debug.Log("[GameStartCoordinator] OnRoomPropertiesUpdate: countdown start detected");
            CheckCountdownStart();
        }
    }

    void CheckAllPlayersLoadedAndStart()
    {
        // 既にカウントダウン開始済みならスキップ（二重実行防止）
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(PROP_COUNTDOWN_START))
        {
            Debug.Log("[GameStartCoordinator] Countdown already started - skipping");
            return;
        }

        foreach (var p in PhotonNetwork.PlayerList)
        {
            object v;
            if (!p.CustomProperties.TryGetValue(PROP_PLAYER_LOADED, out v) || !(v is bool) || !(bool)v)
            {
                Debug.Log($"[GameStartCoordinator] Player not ready yet: {p.NickName}");
                return;
            }
        }

        Debug.Log("[GameStartCoordinator] All players loaded -> starting networked countdown");

        // マスターが未来のサーバ時刻（現在 + 猶予時間）をカウント開始時刻として設定
        int startTimeMs = PhotonNetwork.ServerTimestamp + Mathf.RoundToInt(countdownStartDelay * 1000f);
        var roomProps = new ExitGames.Client.Photon.Hashtable
        {
            { PROP_COUNTDOWN_START, startTimeMs }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
        Debug.Log($"[GameStartCoordinator] Master set countdown start time: {startTimeMs} (ServerNow={PhotonNetwork.ServerTimestamp}, Delay={countdownStartDelay}s)");
    }

    /// <summary>
    /// ルームプロパティを確認してカウントダウンを開始する（全クライアント共通）
    /// </summary>
    void CheckCountdownStart()
    {
        object val;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PROP_COUNTDOWN_START, out val))
        {
            int startTimeMs = (int)val;
            int nowMs = PhotonNetwork.ServerTimestamp;
            int remainMs = startTimeMs - nowMs;

            Debug.Log($"[GameStartCoordinator] Countdown start property detected: startTime={startTimeMs} now={nowMs} remain={remainMs}ms");

            if (remainMs <= 0)
            {
                soundManager.PlayBGM("試合中");
                // 既に開始時刻を過ぎている → 即座に開始
                Debug.Log("[GameStartCoordinator] Start time already passed - starting immediately");
                if (countDown != null) countDown.Play();
                else Debug.LogWarning("[GameStartCoordinator] CountdownUI が割り当てられていません。");
            }
            else
            {
                // 指定時刻まで待ってから開始
                StartCoroutine(CoWaitAndStartCountdown(remainMs / 1000f));
            }
        }
    }

    private IEnumerator CoWaitAndStartCountdown(float waitSeconds)
    {
        Debug.Log($"[GameStartCoordinator] Waiting {waitSeconds:F2}s before starting countdown UI");
        yield return new WaitForSecondsRealtime(waitSeconds);
        Debug.Log("[GameStartCoordinator] Starting countdown UI now (synchronized)");
        if (countDown != null)
        {
            countDown.Play();
        }
        else
        {
            Debug.LogWarning("[GameStartCoordinator] CountdownUI が割り当てられていません。");
        }
    }

    void OnDestroy()
    {
        // オプション: シーン離脱時にフラグをクリアする場合
        try
        {
            var props = new ExitGames.Client.Photon.Hashtable { { PROP_PLAYER_LOADED, false } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }
        catch { }
    }
}