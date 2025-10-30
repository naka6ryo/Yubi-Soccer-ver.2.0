この WebGL テンプレート拡張は、Unity（Yubi-Soccer）から送られる JSON 認識結果をブラウザ上に表示するための簡易オーバーレイを追加します。

使い方（概要）

- テンプレートに含まれる `TemplateData/recognition_overlay.js` はグローバル関数 `window.updateRecognition(jsonString)` を公開します。
- Unity の WebGL ビルドから JSON を渡す方法の例を下に示します。

C# から呼ぶ（簡単な例）

1. DllImport を使う方法（推奨）

```csharp
using System.Runtime.InteropServices;
public static class RecognitionBridge {
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void updateRecognition(string json);

    public static void Send(string json){
        updateRecognition(json);
    }
    #else
    public static void Send(string json){ UnityEngine.Debug.Log("updateRecognition: "+json); }
    #endif
}
```

2. Application.ExternalCall（古い API）

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    Application.ExternalCall("updateRecognition", jsonString);
#endif
```

3. Unity から直接 JS を呼ぶ別の手段として `webgl.jslib` を用いる方法もあります。

備考

- `recognition_overlay.js` は受け取った JSON を整形表示します。JSON でない文字列でもそのまま表示します。
- ブラウザで定期的に外部 URL から JSON を取得したい場合は、`window.fetchRecognitionFromUrl(url, intervalMs)` を呼んでください。

この変更は WebGL テンプレート側の静的実装です。Unity 側（Yubi-Soccer のスクリプト）で認識結果を文字列化して送信するコードの追加が必要です。

---

埋め込みで Yubi-Soccer を表示する方法

このテンプレートに Yubi-Soccer をそのまま「小さく」埋め込み表示する仕組みを追加しました。

1. リポジトリの Web ビルドを配置する

   - 手順: GitHub リポジトリ `https://github.com/naka6ryo/Yubi-Soccer` をローカルで WebGL ビルド（または Web 向けのビルドファイル）にします。ビルド成果物の `index.html` と必要な `Build/` フォルダ等をまとめて、
     `Assets/WebGLTemplates/yubi-soccer/TemplateData/embedded_yubi/` に置いてください。
   - 例: `embedded_yubi/index.html`、`embedded_yubi/Build/...` のような構成にします。

2. テンプレートの動作

   - `index.html` に小さな iframe を追加しました。デフォルトは小さく（320×180）表示され、ボタンで展開/格納できます。
   - Unity の WebGL 本体（Unity のキャンバス）はそのまま表示されます。iframe は Unity キャンバスの上に重なるよう配置してあるため、サイズを小さくすれば Unity 画面も同時に見えます。

3. 注意点

   - 埋め込む Yubi-Soccer のコンテンツがクロスオリジンを要求する場合、同一オリジンでホストするか CORS の対応が必要です。ローカルファイルで iframe を読み込むとブラウザ制限により動作しないことがあります。実際の検証はローカルサーバー（`python -m http.server` 等）やビルド済みの Web サーバー上で行ってください。
   - Yubi-Soccer の Web ビルドが大きい場合、WebGL Build の容量に影響します。必要なら Yubi-Soccer 側を軽量化してから埋め込むことを検討してください（不要なアセットを外す、圧縮を行う等）。

4. 次のステップ（私が代行可能）
   - Yubi-Soccer のビルドをこのテンプレートに実際に配置して統合・動作確認。あなたのローカル環境に配置するか、私がファイルを受け取って配置する方法のどちらでも対応します。ファイルの提供方法を教えてください。
   - iframe 埋め込みではなく、MediaPipe / ゲームロジックを Unity 側に組み込み、結果だけを overlay 表示する方法に最適化することも可能です。
