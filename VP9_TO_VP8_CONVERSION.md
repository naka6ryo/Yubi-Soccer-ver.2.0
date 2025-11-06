# VP9 → VP8 変換ガイド

## ❌ 問題

```
Error: Unsupported video codec 'VP9' found
```

**原因**: Unity は VP9 コーデックをサポートしていません。VP8 のみ対応です。

---

## ✅ 解決方法

### 方法 1: オンラインツール（最も簡単）

#### CloudConvert を使用:

1. https://cloudconvert.com/webm-to-webm にアクセス
2. **Settings** をクリック
3. **Video Codec** を `VP8` に変更
4. **Audio Codec** を `Vorbis` に変更
5. 動画ファイルをアップロード
6. **Convert** をクリック
7. 変換後のファイルをダウンロード
8. 元のファイルと置き換える

**対象ファイル**:

- `Assets/Videos/18_1.webm`
- `Assets/Videos/Opening.webm`

---

### 方法 2: FFmpeg（コマンドライン）

#### FFmpeg のインストール:

**Windows (PowerShell 管理者権限)**:

```powershell
# Chocolateyを使用（推奨）
choco install ffmpeg

# または winget を使用
winget install ffmpeg
```

**インストール後、PowerShell を再起動してください。**

#### 変換コマンド:

```bash
# Opening.webm を変換
ffmpeg -i Opening.webm -c:v libvpx -b:v 2M -c:a libvorbis -b:a 128k Opening_VP8.webm

# 18_1.webm を変換
ffmpeg -i 18_1.webm -c:v libvpx -b:v 2M -c:a libvorbis -b:a 128k 18_1_VP8.webm
```

**元のファイルと置き換える**:

```bash
# 元のファイルを削除して新しいファイルをリネーム
Remove-Item Opening.webm
Rename-Item Opening_VP8.webm Opening.webm

Remove-Item 18_1.webm
Rename-Item 18_1_VP8.webm 18_1.webm
```

---

### 方法 3: VLC Media Player

1. **VLC をダウンロード**: https://www.videolan.org/
2. VLC を開く
3. **Media > Convert / Save**
4. **Add** で動画を選択
5. **Convert / Save** をクリック
6. **Profile** で **Video - VP8 + Vorbis (Webm)** を選択
7. **Destination file** を設定
8. **Start** をクリック

---

## 📋 正しい設定

### Unity 対応 WebM 形式:

```
✅ ビデオコーデック: VP8 (libvpx)
✅ オーディオコーデック: Vorbis
❌ ビデオコーデック: VP9 (非対応)
```

### 推奨設定:

```
形式: WebM
ビデオコーデック: VP8
オーディオコーデック: Vorbis
解像度: 1280x720 または 1920x1080
ビットレート: 1-2 Mbps
フレームレート: 30 fps
```

---

## 🔍 変換後の確認方法

### MediaInfo で確認（推奨）:

1. https://mediaarea.net/en/MediaInfo/Download/Windows からダウンロード
2. インストール後、動画ファイルを右クリック → **MediaInfo**
3. **Format**: WebM
4. **Video Codec**: VP8 ← これを確認
5. **Audio Codec**: Vorbis

### FFprobe で確認:

```bash
ffprobe Opening.webm
# "Stream #0:0: Video: vp8" と表示されればOK
```

---

## ⚡ クイック手順（CloudConvert 推奨）

1. https://cloudconvert.com/webm-to-webm にアクセス
2. Settings → Video Codec: `VP8`
3. Settings → Audio Codec: `Vorbis`
4. `Opening.webm` をアップロード
5. Convert → ダウンロード
6. 元のファイルと置き換え
7. `18_1.webm` も同様に変換
8. Unity で再生テスト

---

変換完了後、Unity エディタで動画を再生して確認してください！
