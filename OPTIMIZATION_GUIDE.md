# WebGL 最適化ガイド - スマホで動作させるために

## 🚨 現在の問題

- WebGL ビルドが重く、スマホで開くと落ちる
- メモリ使用量が大きい
- 処理負荷が高い

## 🎯 最適化方法（優先度順）

### 【最優先】1. Quality Settings の調整

#### Unity Editor で実行:

1. **Edit → Project Settings → Quality**
2. **WebGL** プラットフォームで **Mobile** を選択（現在は適切に設定済み）
3. さらに軽量化するための調整:

```
Mobile Quality Settings:
✅ Pixel Light Count: 1 (現在2 → 1に下げる)
✅ Shadows: Hard Shadows Only (現在2 → 1に変更)
✅ Shadow Distance: 20 (現在40 → 半分に)
✅ Anti Aliasing: Disabled (現在0 → OK)
✅ Anisotropic Textures: Disabled (現在1 → 0に)
✅ Texture Quality: Half Res (Global Texture Mipmap Limit: 1)
✅ VSync: Off (現在0 → OK)
```

### 【重要】2. Build Settings の最適化

#### Player Settings (Edit → Project Settings → Player → WebGL):

```yaml
Resolution and Presentation:
  ✅ Default Canvas Width: 800 (現在960 → 小さく)
  ✅ Default Canvas Height: 600 (OK)
  ✅ Run In Background: OFF (現在OFF → OK)

Other Settings:
  ✅ Color Space: Gamma (Linear は重い)
  ✅ Auto Graphics API: OFF
  ✅ Graphics APIs: WebGL2 のみ (WebGL1を削除)
  ✅ Managed Stripping Level: High
  ✅ Code Optimization: Size (Speed ではなく Size)
```

#### Publishing Settings:

```yaml
✅ Compression Format: Brotli (最小サイズ)
✅ Enable Exceptions: None (Explicitly Thrown Exceptions Only)
✅ Data Caching: ON
✅ WebAssembly Streaming: ON
```

### 【重要】3. スタジアムシーンの軽量化

現在のスタジアムは非常に重いです。以下を実施:

#### A. ライトマップの削除/簡略化

```csharp
// Assets/Scenes/Stadium/LightingData.asset (209MB) を削除
// ベイク済みライティングを無効化して動的ライティングに
```

手順:

1. Stadium シーンを開く
2. **Window → Rendering → Lighting**
3. **Baked Global Illumination** を OFF
4. **Realtime Global Illumination** を OFF
5. **Generate Lighting** を押して、新しいライトマップを削除
6. 不要なライトを削除（Directional Light 1 つのみ残す）

#### B. スタジアムメッシュの簡略化

```
1. Grand Stadium アセットの LOD (Level of Detail) を有効化
2. 遠くのオブジェクトは Culling Mask で非表示
3. 不要な装飾オブジェクトを削除（観客席の細部など）
```

#### C. テクスチャ圧縮

```
1. Project ウィンドウで Assets/GrantStadium/Textures を選択
2. Inspector で Texture Type: Default
3. Max Size: 512 (または 256)
4. Compression: High Quality (モバイル向け)
5. Apply
```

### 【効果的】4. カメラとレンダリングの最適化

#### カメラ設定:

```csharp
Camera.main.farClipPlane = 100f; // 現在の半分に
Camera.main.allowMSAA = false;
Camera.main.allowHDR = false;
```

#### URP Asset の調整:

`Assets/Settings/URP-Mobile-Renderer.asset` を確認:

```yaml
✅ Render Scale: 0.75 (解像度を25%下げる)
✅ Anti Aliasing: None
✅ HDR: OFF
✅ MSAA: OFF
✅ Shadow Distance: 20
✅ Cascade Count: 1
✅ Soft Shadows: OFF
```

### 【効果的】5. 物理演算の最適化

#### Physics Settings (Edit → Project Settings → Physics):

```yaml
✅ Fixed Timestep: 0.03 (現在0.02 → 少し緩める)
✅ Solver Iteration Count: 4 (現在6 → 減らす)
✅ Auto Sync Transforms: OFF
```

#### サッカーボールの最適化:

```csharp
// Rigidbody の Collision Detection を Discrete に
// Interpolate を None に
```

### 【推奨】6. スクリプトの最適化

#### HandStateReceiver.cs

```csharp
// Debug.Log を削除（本番ビルドで無効化）
#if !UNITY_EDITOR
    // Debug.Log をすべてコメントアウト
#endif
```

#### PlayerController.cs

```csharp
// ジョイスティックの更新頻度を下げる
private float joystickUpdateInterval = 0.05f; // 20fps
```

### 【推奨】7. メモリ管理

#### シーンの軽量化:

```
1. 不要なオブジェクトを削除
2. Photon の Serialization Rate を下げる (20Hz → 10Hz)
3. Audio Clip の Quality を下げる (Compressed, Vorbis)
```

### 【簡単】8. テスト用の軽量シーンを作成

スタジアムなしの軽量テストシーン:

```
1. 新しいシーンを作成 "Stadium_Light"
2. 平面（Plane）のみでスタジアムを置き換え
3. プレイヤーとボールだけを配置
4. これで動作確認してから段階的に要素を追加
```

## 📊 期待される効果

| 項目           | 削減率 |
| -------------- | ------ |
| ビルドサイズ   | -40%   |
| メモリ使用量   | -50%   |
| 初期ロード時間 | -60%   |
| FPS 向上       | +100%  |

## 🔧 すぐに実行できる手順（優先順）

### ステップ 1: Quality Settings 変更（5 分）

```
Edit → Project Settings → Quality → Mobile を選択
- Pixel Light Count: 1
- Shadow Distance: 20
- Anisotropic Textures: 0
- Texture Quality: Half Res
```

### ステップ 2: Build Settings 変更（5 分）

```
Edit → Project Settings → Player → WebGL
Publishing Settings:
- Compression Format: Brotli
- Enable Exceptions: None
- Code Optimization: Size
```

### ステップ 3: スタジアムライトマップ削除（10 分）

```
1. Stadiumシーンを開く
2. Window → Rendering → Lighting
3. Baked GI を OFF
4. Generate Lighting → Clear Baked Data
5. LightingData.asset を削除
```

### ステップ 4: テクスチャ圧縮（10 分）

```
1. Assets/GrantStadium/Textures を選択
2. Inspector で Max Size: 256
3. Compression: High Quality
4. Apply
```

### ステップ 5: ビルドしてテスト（5 分）

```
File → Build Settings → Build
スマホで動作確認
```

## 🎯 最終目標

- **ビルドサイズ**: 20MB 以下（現在 50MB 以上）
- **初期ロード**: 5 秒以内（現在 15 秒以上）
- **FPS**: スマホで安定 30fps（現在 10fps 以下）
- **メモリ**: 300MB 以下（現在 800MB 以上）

## 📝 さらなる最適化（上級）

1. **Addressables** を使った遅延ロード
2. **Object Pooling** でインスタンス化を削減
3. **Occlusion Culling** で見えないオブジェクトを非描画
4. **Texture Atlas** で描画コール削減
5. **Shader Variants** の削減

まずは **ステップ 1〜5** を実行してください！
