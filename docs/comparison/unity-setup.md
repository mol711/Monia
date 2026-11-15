# Unityプロジェクトの保存先とGit管理

## 保存先

プロジェクトの保存先は、Moniaリポジトリ内の `src/comparison/unity`。
既存のMoniaリポジトリで管理するため、Unityフォルダー内で別の `git init` は実行しない。
Unity Editor本体のインストール先とは別である。

Unity HubのNew projectで、次の値を入力する。

| 項目 | 入力値 |
|---|---|
| Project name | `unity` |
| Location | Moniaリポジトリ内の `src/comparison` の絶対パス |
| Unity Version Control | 選択項目がある場合は無効（既存のGitで管理する） |

作成前に、最終的なパスが `src\comparison\unity` になることを確認する。
Editorとテンプレートは比較条件に合わせて選択する。本手順ではプロジェクト作成・起動確認はまだ実施していない。

## Gitに含めるもの

- `Assets/` 内のコード、シーン、公開可能な素材と、それぞれの `.meta`
- `Packages/` のパッケージ定義とロックファイル、管理対象の埋め込みパッケージ
- `ProjectSettings/` の設定とEditorバージョン情報

ルートの `.gitignore` で、`Library/`、`Temp/`、`Logs/`、`UserSettings/`、ビルド出力などを除外している。
プロジェクト作成後は、アセットのメタファイルを保持し、シーン等をテキスト形式で保存する設定（Visible Meta Files / Force Text）を確認する。

ruruneなどローカル限定の素材を取り込む場合は `Assets/PrivateAssets/` にまとめる。
このフォルダーとフォルダー自体の `.meta` はGit除外済み。素材を含むビルドは
`%LOCALAPPDATA%\MoniaDev\private-builds` に保存する。
公開するシーンやPrefabに、非公開素材を埋め込んだり、そのデータをコピーしたりしない。
Git除外はエクスポートやアーカイブの対象を制御しないため、配布する場合は含まれるファイルを別途確認する。

## 初回保存後の確認

Moniaのルートで `git status --short` を確認し、キャッシュや非公開素材が含まれていないことを確認してから、
`Assets`、`Packages`、`ProjectSettings` をコミットする。
現時点では保存場所と除外設定のみを用意している。

## 公式資料

- [Unity Hubでのプロジェクト作成](https://docs.unity.com/en-us/hub/project-create)
- [Unityプロジェクトのフォルダー構成](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/project-configuration/default-directories)
- [アセットのメタデータ](https://docs.unity.com/en-us/engine/6000.6/manual/assets-and-media/import-assets/asset-metadata)
