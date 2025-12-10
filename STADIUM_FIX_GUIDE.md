# Grand Stadium V2.0 ピンク問題の解決手順

## 問題

Grand Stadium V2.0 は Built-in Render Pipeline 用のアセットのため、URP プロジェクトではマテリアルがピンク（マゼンタ）になります。

## 解決手順

### ステップ 1: マテリアルを一括変換

1. Unity のメニューバーから `Edit` → `Render Pipeline` → `Universal Render Pipeline` → `Upgrade Project Materials to UniversalRP Materials` を選択
2. 確認ダイアログが表示されたら `Proceed` をクリック
3. 変換が完了するまで待機

### ステップ 2: 個別マテリアルの修正（必要に応じて）

変換後もピンクのままのオブジェクトがある場合：

1. ピンクのオブジェクトを選択
2. Inspector で Material を確認
3. Shader が `Hidden/InternalErrorShader` や赤文字になっている場合：
   - Shader ドロップダウンをクリック
   - `Universal Render Pipeline` → `Lit` を選択
4. テクスチャが外れている場合は、元のテクスチャを再割り当て

### ステップ 3: ライティングの調整

URP では照明の挙動が異なるため、調整が必要な場合があります：

1. スタジアムの Directional Light を選択
2. Intensity（強度）を調整（推奨: 0.5 - 1.5）
3. 必要に応じて Post-processing（ポストプロセス）を追加

### ステップ 4: シャドウ設定の最適化

スタジアムには多数のライトがあるため、シャドウ設定を調整：

1. `Edit` → `Project Settings` → `Quality` を開く
2. 使用中の Quality レベルを選択
3. `Shadows` セクションで：

   - `Shadow Resolution`: `High Resolution` または `Very High Resolution`
   - `Shadow Distance`: 50-100（必要に応じて調整）

4. URP アセットの設定（推奨）：
   - Project ウィンドウで URP Asset（通常は `Settings` フォルダ内）を選択
   - Inspector で以下を設定：
     - `Shadow Atlas Resolution`: `4096` に増やす
     - `Main Light` → `Cast Shadows`: `On`
     - `Additional Lights` → `Cast Shadows`: 必要な数のみ `On`

### トラブルシューティング

#### 問題: 一部のオブジェクトだけピンク

**原因:** 特定のマテリアルが変換されていない
**解決:** 該当オブジェクトのマテリアルを手動で `URP/Lit` に変更

#### 問題: テクスチャが消えた

**原因:** マテリアル変換時にテクスチャのリンクが外れた
**解決:**

1. 元のマテリアルを確認（Project ウィンドウでマテリアルを選択）
2. Base Map スロットに元のテクスチャをドラッグ&ドロップ

#### 問題: 暗すぎる/明るすぎる

**原因:** URP と Built-in でライティングモデルが異なる
**解決:**

1. Directional Light の強度を調整
2. Environment Lighting（Window → Rendering → Lighting → Environment）を調整
3. マテリアルの Emission を調整

## 注意事項

- 変換は元に戻せないため、事前にプロジェクトのバックアップを推奨
- 一部のエフェクトやカスタムシェーダーは手動での調整が必要な場合があります
