# HUD Clock

VRChatアバター向けのローカル時刻HUDです。

パッケージ本体は `Packages/com.tentee.vrc-hud-clock/` にあります。利用方法と対応環境は、パッケージ内の [README](Packages/com.tentee.vrc-hud-clock/README.md) を参照してください。

リリース時には、同フォルダーの`package.json`からVPM用のzipとUnityPackageを生成します。

## リリース手順

1. `package.json`のversionを上げてmainにマージします。
2. GitHubでタグ`<version>`のリリースを公開すると、ワークフローがzip・UnityPackage・`package.json`を自動で添付します。
3. 手動でActionsの**Release**を`main`から実行しても作成できます（古いリリースへの後付けはバージョンタグを指定して実行）。`dry_run`を指定するとビルドと検証だけ行います。
4. シークレット`VPM_REPOS_TOKEN`があればVPMリスティングの再ビルドも自動で起動します。ない場合は`TenteEEEE/vpm-repos`のActionsで**Build Repo Listing**を手動実行してください。
