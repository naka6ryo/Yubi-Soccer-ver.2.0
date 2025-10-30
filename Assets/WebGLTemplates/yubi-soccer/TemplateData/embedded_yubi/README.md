# 指サッカー (Three.js + MediaPipe Hands)

モバイルブラウザ向けに、フロントカメラ映像から MediaPipe Hands (HandLandmarker) を用いて RUN / KICK ジェスチャを推定し、Three.js のボールに反映する最小実装です。

## 使い方

- HTTPS でホストしてください（`getUserMedia` と WASM 読み込みのため）。
- iOS Safari / Android Chrome を想定。
- `index.html` を配信して、開始ボタンからカメラ許可してください。

## 構成

- `index.html` — UI と起動、video/canvas、ミラー反転トグル、状態表示。
- `main.js` — Three.js シーン初期化、ボールの簡易物理、RUN/KICK 反映。
- `hand.js` — MediaPipe HandLandmarker の初期化、推論ループ、ランドマーク描画、ジェスチャ分類。
- `utils.js` — シグナル処理ユーティリティ（移動平均、相関、角度、微分、RMS、リングバッファ等）。

## ジェスチャ定義（最小ルール）

- RUN: 人差し指(8)と中指(12)の y 速度の相関が負で |r|>0.5、かつ速度振幅が閾値以上。
- KICK: 親指(4)–人差し指(8)の角速度ピーク > しきい値、かつ手首(0)の速度ピーク > しきい値。
- 出力: `state = 'NONE' | 'RUN' | 'KICK'` と `confidence (0-1)`。
- 安定化: 0.3s デバウンスとヒステリシス（発火/解除でしきい値を分離）。

## パフォーマンス

- 入力解像度は 320px（FPS 低下時は 256px）に自動調整。
- 推論は描画ループから分離。最新の判定結果のみ UI/物理に反映。
- フレーム毎に 2D オーバーレイへランドマークを簡易描画。

## 注意

- モデルと WASM は CDN から非同期ロードします。初期化完了後に推論が開始されます。
- 角度計算・相関・微分はゼロ割/NaN を避ける対策を入れています。
- フロントカメラの左右反転はトグル可能。縦横切替は video/canvas の実サイズに追従。

## ライセンス

- Three.js: MIT License
- MediaPipe (@mediapipe/tasks-vision): Apache License 2.0
- 本リポジトリ: [ライセンスをここに追記してください]
