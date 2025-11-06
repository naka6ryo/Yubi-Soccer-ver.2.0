# WebM 形式の動画を Unity WebGL で使用する手順

## 📹 動画の準備

### 1. 動画を WebM 形式に変換

#### オンラインツール（簡単）:

- https://cloudconvert.com/mp4-to-webm
- https://convertio.co/ja/mp4-webm/

設定:

- Video Codec: VP8
- Audio Codec: Vorbis
- Quality: High
- Resolution: 1920x1080 または 1280x720

#### FFmpeg（コマンドライン）:

```bash
# 基本的な変換
ffmpeg -i input.mp4 -c:v libvpx -b:v 2M -c:a libvorbis -b:a 128k output.webm

# 高品質変換
ffmpeg -i input.mp4 -c:v libvpx -crf 10 -b:v 2M -c:a libvorbis -q:a 6 output.webm

# 解像度を指定
ffmpeg -i input.mp4 -c:v libvpx -b:v 2M -vf scale=1280:720 -c:a libvorbis output.webm
```

パラメータ説明:

- `-c:v libvpx`: VP8 ビデオコーデック
- `-b:v 2M`: ビデオビットレート 2Mbps
- `-crf 10`: 品質（0-63, 低いほど高品質）
- `-c:a libvorbis`: Vorbis オーディオコーデック
- `-b:a 128k`: オーディオビットレート
- `-vf scale=1280:720`: 解像度変更

---

## 🎮 Unity での設定

### 2. 動画ファイルを配置

```
Assets/
└── Videos/
    └── opening.webm  ← ここに配置
```

**重要**: StreamingAssets ではなく、通常の Assets フォルダに配置

### 3. Unity Inspector で設定

#### VideoPlayerController:

```
Video Player Controller (Script)
├─ Video Settings
│  ├─ Video Clip: [opening.webm] ← ドラッグ＆ドロップ
│  ├─ Play On Awake: ✓
│  ├─ Loop: □
│  └─ Volume: 1.0
│
├─ Fade Settings
│  ├─ Fade In On Start: ✓
│  ├─ Fade Start Color: 白
│  ├─ Fade In Duration: 1.0
│  └─ Fade Image: [FadeImage]
│
├─ Loading Panel Settings
│  ├─ Loading Panel: [LoadingPanel]
│  └─ Loading Delay: 0.5
│
├─ Render Settings
│  ├─ Render Mode: Render Texture ⭐ WebGLではこれを推奨
│  └─ Target Texture: [VideoRenderTexture]
│
└─ WebGL Settings
   └─ Skip Video On WebGL: □ (チェックしない)
```

### 4. Render Texture を使用（WebGL 必須）

#### 4-1. Render Texture を作成:

1. Project → 右クリック → **Create > Render Texture**
2. 名前: `VideoRenderTexture`
3. Inspector 設定:
   - Size: 1920 x 1080（動画と同じ解像度）
   - Depth Buffer: No depth buffer
   - Anti-aliasing: None

#### 4-2. RawImage を作成:

```
Canvas
└── VideoDisplay (RawImage)
    ├─ Rect Transform: Anchor Stretch（全画面）
    ├─ Texture: [VideoRenderTexture]
    └─ Color: 白 (255, 255, 255, 255)
```

#### 4-3. VideoPlayer の設定:

```
Render Mode: Render Texture
Target Texture: [VideoRenderTexture]
```

---

## ⚙️ ビルド設定

### 5. WebGL Publishing Settings

1. **File > Build Settings > WebGL > Player Settings**

2. **Publishing Settings**:

   - Compression Format: Gzip または Brotli
   - Data Caching: ✓ （有効）

3. **Resolution and Presentation**:
   - Run In Background: ✓

---

## ✅ 動作確認

### 6. テスト手順

#### Unity エディタでテスト:

1. Opening シーンを開く
2. Play ボタンを押す
3. 動画が再生されるか確認

#### WebGL ビルドでテスト:

1. **File > Build Settings**
2. **Platform: WebGL** を選択
3. **Build And Run**
4. ブラウザで動作確認

#### 確認ポイント:

- ✅ 動画が表示される
- ✅ 音声が再生される
- ✅ 動画終了後にローディングパネルが表示される
- ✅ Photon 接続後に GameTitle シーンに遷移

---

## 🐛 トラブルシューティング

### 動画が表示されない場合:

#### 1. ブラウザコンソールを確認

- **F12** → **Console** タブ
- エラーメッセージを確認

#### 2. 動画ファイルを確認

```
正しい形式:
- 拡張子: .webm
- ビデオコーデック: VP8
- オーディオコーデック: Vorbis
```

#### 3. Render Texture 設定を確認

- VideoPlayer の Render Mode が **Render Texture**
- Target Texture が設定されている
- RawImage の Texture に **VideoRenderTexture** が設定されている

#### 4. VideoPlayer の設定を確認

```
Video Player (Component)
├─ Source: Video Clip
├─ Video Clip: [opening.webm]
├─ Render Mode: Render Texture
├─ Target Texture: [VideoRenderTexture]
└─ Audio Output Mode: Audio Source
```

### 音声が聞こえない場合:

```
Audio Source (Component)
├─ Volume: 1.0
├─ Mute: □ (チェックなし)
└─ Play On Awake: □
```

### 動画がカクつく場合:

- 動画の解像度を下げる（1280x720 推奨）
- ビットレートを下げる（1-2Mbps）
- フレームレートを 30fps に

---

## 📝 推奨設定まとめ

### 動画ファイル:

```
形式: WebM (VP8 + Vorbis)
解像度: 1280x720
ビットレート: 1-2 Mbps
フレームレート: 30 fps
長さ: 30秒以内推奨
```

### Unity 設定:

```
Render Mode: Render Texture
Compression: Gzip
Data Caching: ON
```

### ブラウザサポート:

- ✅ Chrome, Edge, Firefox, Opera
- ⚠️ Safari (一部制限あり)

---

この設定で WebGL ビルドでも動画が正常に再生されるはずです！
