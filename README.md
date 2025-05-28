# Change Render Queue

Change Render Queue of the material slot. Includes the animations.

マテリアルスロットのRenderQueueを変更する。当該スロットを含むアニメーションもちゃんと変更されます。

（RenderQueueの変更のみなので、透過マテリアルにするとかどうとかは自分でやる必要がある）

## Install

### VCC用インストーラーunitypackageによる方法（おすすめ）

https://github.com/Narazaka/ChangeRenderQueue/releases/latest から `net.narazaka.vrchat.change-render-queue-installer.zip` をダウンロードして解凍し、対象のプロジェクトにインポートする。

### VCCによる方法

1. https://vpm.narazaka.net/ から「Add to VCC」ボタンを押してリポジトリをVCCにインストールします。
2. VCCでSettings→Packages→Installed Repositoriesの一覧中で「Narazaka VPM Listing」にチェックが付いていることを確認します。
3. アバタープロジェクトの「Manage Project」から「Change Render Queue」をインストールします。

## Usage

レンダーキューを変更したいRendererのGameObjectに「Add Component」ボタンから「ChangeRenderQueue」を付けて設定する。

## Changelog

- 1.0.3
  - アニメーションで変更されるマテリアルスロットが、ビルド前の元のマテリアルに相当するものにアサインされないことがあった問題を修正。
    - Custom Animationsをフォールバックで無効にしたとき、またはImposterの生成時に問題になります。
- 1.0.2
  - マテリアルスロットがアニメーション制御されていない場合に動作していなかった問題を修正
- 1.0.0
  - リリース

## License

[Zlib License](LICENSE.txt)
