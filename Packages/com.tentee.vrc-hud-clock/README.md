# HUD Clock

VRChatアバターの一人称ビューに、装着者のローカル時刻を表示するHUDです。

## 対応環境

- Unity 2022.3
- VRChat Avatars SDK 3.10.4以降
- Modular Avatar 1.18.7以降
- NDMF 1.14.8以降
- PC版VRChat（Desktop / PCVR）

Quest単体のAndroidアバターではカスタムシェーダーを使用できないため利用できません。

## 導入

1. VCCまたはVPM CLIに次のリポジトリを追加します。  
   `https://tenteeeee.github.io/vpm-repos/index.json`
2. `HUD Clock`をプロジェクトへ追加します。
3. アバターのルートを選択し、`GameObject > HUD Clock`を実行します。
4. 必要に応じて、追加されたコンポーネントの表示設定を調整してアップロードします。

ビルド時にNDMFが表示オブジェクト、メニュー、アニメーションを生成します。生成物を手作業で編集する必要はありません。

## 主な機能

- 時刻表示（秒、コロン点滅、色、発光、セグメント表示の調整）
- 左右位置、位置・回転の微調整、透過度のExpression Menu操作
- RotationAlert対応ワールドでのイベントタイマー行
- ローカルユーザーだけに表示。ミラーとハンドヘルドカメラでは非表示

時刻はOSの時計を直接読むのではなく、VRChatが提供する時刻globalを使用します。表示されるタイムゾーンはVRChatの設定に従います。OBSなどの画面キャプチャには通常の一人称ビューとして表示されます。

## 注意

- 対象アバターにはVRChatのヒューマノイドリグが必要です。
- Avatar Optimizerを使用する場合、必要に応じて`HUDClock`を最適化除外対象にしてください。
- 旧来のPrefab版からは自動移行されません。旧オブジェクトを削除してから、このパッケージのメニューで追加してください。

## ライセンス

MIT License
