
# 描画と物理（ゲームロジック）分離のための引継ぎ資料（更新版）

このドキュメントは、描画（Renderer）と物理／ゲーム状態（Physics/Game）を分離して統合するための手順と API 仕様を示します。
このリポジトリでは最近以下の変更を行いました。ドキュメントはそれらを反映しています。

- 描画初期化を `renderer.js` に分離（Three.js の初期化、ライト、フィールド、ボール作成）。
- 物理・ゲームロジックは `main.js` に集約（`setBall`, `updatePhysics`, `setRunBoost`, `kickImpulse` など）。
- 手検出は `hand.js` に残し、負荷対策として検出スロットル（detectIntervalMs）を導入、デフォルトで片手検出（`numHands=1`）に変更。ジョイスティック（グー検出）機能は削除して簡素化しました。

## 目的
- 描画（レンダリング）と物理（ゲーム状態）を分離して、別々のモジュール・チームが独立して変更できるようにする。描画エンジン（Three.js など）を差し替えても物理ロジックをそのまま再利用できるようにすることが目的です。

## 新しいファイル構成（要点）
- `renderer.js` - Three.js によるシーン構築、ライト、フィールド、ボールメッシュ作成。`setupRenderer(canvas)` をエクスポートして、{ scene, camera, renderer, clock, ball, field } を返します。
- `main.js` - 物理（重力・摩擦・衝突）と外部からの入力インターフェースを管理。`setBall(mesh)` で `renderer.js` が作成した ball メッシュを受け取り、`updatePhysics(dt)` が ball.position を更新します。
- `hand.js` - MediaPipe HandLandmarker を使った手検出。最新の実装では検出をスロットルし、`numHands=1`（片手）で軽量化しています。ジョイスティック機能は削除されています。
- `index.html` - 起動側。`setupRenderer()` で得た ball を `main.setBall()` に渡し、`HandTracker` の `onResult` から `setRunBoost`/`kickImpulse` を呼びます。

## API 仕様（短い参照）

### renderer.js
- setupRenderer(canvas: HTMLCanvasElement)
  - 返却: { scene, camera, renderer, clock, ball, field }
  - 説明: canvas を使って Three.js の Renderer を作成し、フィールドとボールをシーンに追加します。呼び出し元は返された `ball` を `main.setBall()` に渡します。

- resizeRendererToDisplaySize(renderer, camera)
  - 説明: ウィンドウリサイズ時に renderer と camera のアスペクト比を更新するユーティリティ。

### main.js
- setBall(mesh: THREE.Mesh)
  - 説明: 描画側が作成したボールメッシュ参照を受け取り、以後 physics が直接位置を更新します。

- updatePhysics(dt: number)
  - 説明: 重力や摩擦等を適用して `ball.position` を更新します。外部のレンダラはこの更新後に `renderer.render(scene, camera)` を呼ぶことで描画が反映されます。

- setRunBoost(conf: number), kickImpulse(conf: number)
  - 説明: `hand.js`（入力/ジェスチャ判定）から呼ばれる関数。RUN/KICK の強度を physics に与えます。

### hand.js
- HandTracker クラス
  - constructor({ video, overlay, mirror = false, onResult })
    - `onResult` は { fps, state, confidence } を受け取るコールバック。
  - detectIntervalMs: 検出実行間隔（ミリ秒）。デフォルトは約 1000/15 ms（15 FPS 相当）に設定されています。
  - 設定: 初期化時に `numHands` は 1 に設定されています（軽量化）。
  - 注意: ジョイスティック（グー検出）機能は削除され、片手のランドマーク描画と RUN/KICK 判定にフォーカスしています。

## 動作フロー（index.html サンプル）

1. `three = setupRenderer(threeCanvas)` を呼ぶ。
2. `setBall(three.ball)` を呼んで物理に参照を渡す。
3. `tracker = new HandTracker({ video, overlay, mirror, onResult })` を作成し、`await tracker.init(); tracker.start();` を呼ぶ。
4. ループ内で `updatePhysics(three.clock.getDelta())` を呼び、`three.renderer.render(three.scene, three.camera)` で描画する。

`onResult` の例（既存コード）:

```js
onResult: ({ fps, state, confidence }) => {
  // HUD 更新
  fpsEl.textContent = fps.toFixed(0);
  stateEl.textContent = state;
  confEl.textContent = confidence.toFixed(2);
  // RUN/KICK を physics に伝播
  if (state === 'RUN') setRunBoost(confidence);
  if (state === 'KICK') kickImpulse(confidence);
}
```

## 設定の変更箇所（よく触る場所）

- `hand.js`:
  - `this.detectIntervalMs` を変更すると検出頻度を変えられます（大きくすると負荷が下がる）。
  - `numHands` を 2 に戻せば両手検出に戻りますがコストが増えます（`HandLandmarker.createFromOptions` の `numHands`）。

- `main.js`:
  - `PARAMS` 内の `gravity`, `friction`, `runAccel`, `kickUp`, `kickForward`, `restitution` を調整して挙動をチューニングしてください。

## モバイル（スマホ）での確認

- ローカルで確認する場合: サーバを `--bind 0.0.0.0` で起動し（`serve.py --bind 0.0.0.0 --port 3002`）、ホスト PC の LAN IP（例: `http://192.168.x.y:3002`）にスマホのブラウザでアクセスします。
- 安定的にカメラ権限を得たい場合: HTTPS が必要なブラウザがあるため、Netlify 等で公開するか `ngrok` で HTTPS トンネルを張ると良いです（`DEPLOYMENT.md` 参照）。

## Netlify / 配信上の注意

- リポジトリには `_headers` / `netlify.toml` を追加してあり、COOP/COEP と WASM/.task の MIME を正しく返すように設定しています。Netlify にデプロイすると HTTPS 下で安定して動作します。

## トラブルシュート（よくある問題）

- FPS が低い
  - `hand.js` の `detectIntervalMs` を大きくして推論回数を減らす。例えば 1000/10（10 FPS 相当）にすると CPU 負荷がさらに下がります。
  - `numHands` を 1 にする（既にデフォルトで 1 になっています）。

- `.task` ファイルが 404
  - `vendor/mediapipe/wasm/hand_landmarker.task` がリポジトリに含まれているか確認。大きなバイナリを Git 管理したくない場合は CDN を使い、`hand.js` の modelCandidates を編集して優先先を変えてください。

- カメラが許可されない/動かない
  - ローカルホスト以外の HTTP では扱いが厳しいブラウザがあります。スマホでの安定検証は HTTPS（Netlify/ngrok）を推奨します。

## 追加の改善案（短く）

- 自動調整: 実行中の FPS 平均を監視し、負荷が高ければ `detectIntervalMs` を自動で拡大／縮小する適応スロットリングの実装。
- イベントベース: 物理更新結果をイベントとして公開し、複数の描画層が購読できるようにする（`onBallMoved(cb)` など）。

---

このドキュメントは現在の実装（2025-10-17 時点）に合わせて更新しました。追加で「POV コントローラの実装例」や「自動スロットリング」のサンプルが要れば作成します。

## API 仕様

### renderer.js
- setupRenderer(canvas: HTMLCanvasElement)
  - 返却: { scene, camera, renderer, clock, ball, field }
  - 説明: canvas を使って Three.js の Renderer を作成し、フィールドとボールをシーンに追加する。ボールメッシュを返すため、物理モジュールはこの参照を受け取って位置更新を行える。

- resizeRendererToDisplaySize(renderer, camera)
  - 説明: ウィンドウリサイズ時に renderer と camera のアスペクト比を更新するユーティリティ。

### main.js
- setBall(mesh: THREE.Mesh)
  - 説明: 描画側が作成したボールメッシュ参照を受け取り、以後 physics が直接位置を更新する。

- updatePhysics(dt: number)
  - 説明: 重力や摩擦等を適用して `ball.position` を更新する。外部のレンダラはこの更新後に `renderer.render(scene, camera)` を呼ぶことで描画が反映される。

- setRunBoost(conf: number), kickImpulse(conf: number)
  - 説明: `hand.js`（入力/ジェスチャ判定）から呼ばれる関数。RUN/KICK の強度を physics に与える。

## 状態引継ぎ（RUN / KICK / NONE）

入力検出は `hand.js` に残ります（HandTracker が `classify()` で状態を返す）。`index.html` の起動スクリプトでは `HandTracker` の `onResult` コールバックで状態を受け取り、以下のように物理モジュールへ伝えます。

- RUN: `setRunBoost(confidence)` を呼ぶ。これにより `velocity` に前進加速が入る。
- KICK: `kickImpulse(confidence)` を呼ぶ。一度だけのインパルスを与える。
- NONE: 何もしない（物理は自然減衰や摩擦で停止する）。

これにより、描画モジュールは一切ジェスチャ判定に依存せず、`ball` の位置更新だけを受け取って表示する。

## 他の描画処理・ゲーム処理への引継ぎ例

1. 別のレンダラ（例: WebGL2 で独自描画）に差し替える場合:
   - `renderer.js` を編集して `setupRenderer()` が返す `ball` を取り除き任意の描画要素へ差し替える。
   - `main.js` の `setBall()` に渡すオブジェクトを、`{ position: { x, y, z } }` のような最小インターフェースに合わせることで互換性を持たせられる。

2. ゲームロジックを別スコープで走らせたい場合:
   - `main.js` の `updatePhysics(dt)` をエクスポートし、外部のゲームループ（例: server-synced tick）から呼ぶ。
   - `setBall()` は物理インテグレーション結果を反映するフックとして使える。

## サンプル呼び出しフロー（index.html）

1. `three = setupRenderer(threeCanvas)` を呼ぶ。
2. `setBall(three.ball)` を呼んで物理に参照を渡す。
3. ループ内で `updatePhysics(three.clock.getDelta())` を呼び、`three.renderer.render(three.scene, three.camera)` で描画する。

## 注意点と拡張案

- ball メッシュへの参照を直接渡す方式は簡単だが、参照整合性に注意。将来的には物理結果を返す関数（例: `getPhysicsState()`）やイベント発火（例: `onBallMoved(cb)`）を作ると疎結合になる。
- POV コントローラ（カメラ操作）を追加する場合は、`hand.js` の joystick 値を `index.html` のループで受け取り `three.camera.position` や `three.camera.lookAt` に適用する。

---

このドキュメントは最低限の使い方を示しています。必要ならサンプルコード（POV 用の camera controller、物理同期用のイベント API 等）を追加します。
