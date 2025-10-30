# Photon PUN 2 導入と最初の設定

このプロジェクトに Photon PUN 2 を導入し、1 対 1 の簡易マッチング（Quick Match）を行うための手順とサンプルスクリプトを含みます。

ファイル追加:

- `Assets/Scripts/Network/NetworkManager.cs` : QuickMatch と基本的な Photon コールバックを提供するスクリプト（AppId を Inspector で上書き可能）。
- `Assets/Scripts/Network/MatchmakingUI.cs` : UI ボタンと NetworkManager をつなぐ簡易スクリプト。

手順:

1. Photon PUN 2 を導入する

   - Unity の Package Manager か Asset Store から "Photon PUN 2 - Free" をインポートしてください。
   - (Asset Store) Window -> Asset Store で検索して Import
   - (UPM) Window -> Package Manager -> 右上の + -> "Add package from git URL..." は不要です。Asset Store の提供手順に従ってください。

2. Photon Dashboard で App ID を取得

   - https://dashboard.photonengine.com に登録して、新しいアプリを作成してください（PUN 用の App Id を取得）。

3. App ID の設定方法（どちらか）
   A) 推奨: PhotonServerSettings に直接貼る

   - Unity メニューの Photon -> PUN Wizard を開き、App ID を貼り付けてセットアップしてください。
     B) 代替: NetworkManager の Inspector に AppId を貼る
   - `Assets/Scenes/Handtrack test.unity` のシーンに `NetworkManager` コンポーネントを追加し、Inspector の `appId` フィールドに貼り付けてください。
   - このスクリプトは Play 時に PhotonServerSettings の AppSettings を上書きします（実行時のみ）。

4. シーンセットアップ

   - シーンに空の GameObject を作成し、`NetworkManager` をアタッチ。
   - Canvas を作り、Button（QuickMatch）と Text（Status）を作成。
   - `MatchmakingUI` を Canvas に追加し、Button と Text、NetworkManager を割り当てる。

5. Player prefab（後で使用）

   - `PhotonNetwork.Instantiate("Player", ...)` を使うには、Resources フォルダ内に `Player.prefab` を置くか、Photon の Prefab Pool に登録してください。

6. 実行 & テスト
   - 2 クライアントで接続テストを行います。Editor + Build 1 つが簡単です：
     - Build を一つ作成して起動し、Editor とビルドの両方で QuickMatch を押してください。

注意点:

- App ID をソース管理に登録する場合は秘密扱いに注意してください（公開リポジトリでは避ける）。
- Photon PUN のバージョンによって API が若干異なることがあります。Asset インポート後にコンソールにエラーが出たらログを教えてください。

次のタスク:

- Player prefab と `PlayerController`（W/A/D 入力）を作成し、PhotonTransformView またはカスタム同期で位置/回転同期を実装します（これを進めますか？）。
