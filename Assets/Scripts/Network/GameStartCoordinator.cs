using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Photon.Pun.UtilityScripts;
using YubiSoccer.UI;

/// <summary>
/// Multi Player シーン内で各クライアントのロード完了を通知し、
/// マスターが全員ロード完了を確認したらネットワーク同期カウントダウンを開始する。
/// Multi Player シーンの任意の GameObject（例: GameManager）にアタッチしてください。
/// </summary>
public class GameStartCoordinator : MonoBehaviourPunCallbacks
{
    const string PROP_PLAYER_LOADED = "playerLoaded";
    public CountdownUI countDown;

    void Start()
    {
        // 自分の読み込み完了をルームのプレイヤープロパティに設定
        var props = new Hashtable { { PROP_PLAYER_LOADED, true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log("[GameStartCoordinator] Set playerLoaded=true");

        // マスターは即チェック（既に全員揃っている可能性がある）
        if (PhotonNetwork.IsMasterClient)
        {
            CheckAllPlayersLoadedAndStart();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (changedProps.ContainsKey(PROP_PLAYER_LOADED))
        {
            Debug.Log($"[GameStartCoordinator] PlayerPropertiesUpdate: {targetPlayer.NickName} loaded={changedProps[PROP_PLAYER_LOADED]}");
            CheckAllPlayersLoadedAndStart();
        }
    }

    void CheckAllPlayersLoadedAndStart()
    {
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

        countDown.Play();
    }

    void OnDestroy()
    {
        // オプション: シーン離脱時にフラグをクリアする場合
        try
        {
            var props = new Hashtable { { PROP_PLAYER_LOADED, false } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }
        catch { }
    }
}