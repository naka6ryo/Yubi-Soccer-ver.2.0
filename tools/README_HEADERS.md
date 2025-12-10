目的

Build フォルダを Netlify の UI（ドラッグ＆ドロップ）でアップロードする際に、WebGL のプリ圧縮ファイル（.br）を正しく配信するための `_headers` を自動で配置する方法を説明します。

セットアップ手順

1. Unity で WebGL ビルドを行い `Build` フォルダを生成します。
2. このリポジトリのルートで次のスクリプトを実行します（PowerShell）：

```powershell
# 通常実行（存在する場合は上書きしない）
.\tools\add_headers_to_build.ps1 -BuildPath .\Build

# 強制上書きする場合
.\tools\add_headers_to_build.ps1 -BuildPath .\Build -Force
```

3. スクリプトは `tools/_headers_build_template.txt` を `Build\_headers` ではなく `Build\_headers` のテンプレを直接 `_headers` としてコピーします。

4. `Build` フォルダのルートに `_headers` ファイルがあることを確認して、Netlify のサイトダッシュボード -> Deploys -> Drag and drop へフォルダをドロップします。

検証

デプロイ後、ブラウザ DevTools の Network タブで以下を確認してください：

- `Build/*.js.br` 等のリソースに対して `Content-Encoding: br` が存在すること
- `Content-Type` がそれぞれ期待される型（`application/javascript`, `application/wasm`, `application/octet-stream`）になっていること

補足

- Git ベースの自動デプロイを使用している場合は、既にリポジトリに追加済みの `netlify.toml` が適用されます。手動（ドラッグ＆ドロップ）でのデプロイには `_headers` が必要です。
- 一部の CDN / Edge（Netlify Edge 含む）は `Content-Length` ヘッダを付与しない挙動があるため、Unity のローダーがまだ警告を出す場合があります。その場合は非圧縮ファイルを配布する回避策も検討してください。
