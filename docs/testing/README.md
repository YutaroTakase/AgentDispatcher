# 試験ドキュメント

このディレクトリはAgentDispatcherの実機受け入れ確認を扱う。

通常のCIで確認できる単体・結合・API・フロントエンド検証とは分離し、WSL2 Ubuntu、GitHub認証、ChatGPT認証済みCodex CLIなど実環境を必要とする確認をここへ記載する。

## 正本

- [導入試験手順](installation-test-procedure.md) — クリーンなWSL2 Ubuntuへの導入、認証、サービス起動、更新・削除の確認
- [E2Eテストケース](e2e-test-cases.md) — GitHub IssueからCodex実行、履歴保存、異常系までの総合試験

## 判定

MVPの実機受け入れ完了は、導入試験の必須項目とE2Eテストの優先度「必須」のケースがすべて成功した時点とする。

試験結果はGitHub IssueまたはPull Requestへ、実施日、対象コミット、環境情報、各ケースの結果、失敗時の証跡を記録する。
