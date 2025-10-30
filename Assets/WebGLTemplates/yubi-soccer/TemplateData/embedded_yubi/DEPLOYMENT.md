# Netlify デプロイ手順

このリポジトリを Netlify にデプロイすると、WASM と MediaPipe の `.task` ファイルを適切なヘッダ（COOP/COEP）付きで配信できます。以下は簡単な手順です。

## 1) 簡単な Drag & Drop（短時間で確認）
- ビルドは不要です（静的ファイル群をそのまま配信）。
- https://app.netlify.com にログイン → Sites → "Add new site" → "Deploy manually" → public フォルダ（このリポジトリのルート）を ZIP 化してアップロードします。
- アップロード完了後、サイトの URL で http/https 配信が始まります。

注意: Netlify は HTTPS を自動で付与するため、ブラウザのカメラアクセス要件（secure context）を満たします。

## 2) GitHub 連携（継続的デプロイ）
- GitHub と連携し、リポジトリを選択してデプロイするだけで、push するたびに公開されます。
- build コマンドは不要なので `build command` は空、publish directory は `.` を指定します（`netlify.toml` に既に設定済み）。

## 3) COOP/COEP と MIME 設定
- ルートに `_headers` と `netlify.toml` を追加しています。これにより:
  - `Cross-Origin-Opener-Policy: same-origin` と `Cross-Origin-Embedder-Policy: require-corp` が全ページに付与されます（WASM を `ImageBitmap` などで使う場合に必要）。
  - `.wasm` と `.task` の MIME を適切に返すようヘッダを追加しています。

## 4) 公開後の確認
- サイト URL をスマホで開いて `index.html` を表示し、Start をタップしてカメラを許可します。HTTPS なのでカメラ許可はより安定します。
- DevTools が必要な場合、Chrome の remote debugging（`chrome://inspect`）でスマホを接続してコンソールやネットワークを確認してください。

## 5) トラブルシュート
- MediaPipe の `.task` が 404 になる場合: `vendor/mediapipe/wasm/hand_landmarker.task` がアップロードされているか確認してください。大きいファイルは Git LFS を使うか、別途 CDN に置く方法を検討してください。
- CORS / COOP エラーが出る場合: `_headers` と `netlify.toml` の設定を確認してください（変更後は再デプロイが必要）。

---

必要なら私の方で Netlify にデプロイした際の設定ファイルアップデートや、`package.json` ベースのビルドフロー（将来的にバンドルや最適化を加えたい場合）を追加で作成します。
