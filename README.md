# Monia

Phase 0のWindows試作アプリです。静止画の選択と復元、別プロセスで動くUnity試験版の起動・終了と表情操作、Lively経由の壁紙切り替えを試せます。

## 本体の起動

Windowsで.NET 10 SDKを用意し、リポジトリのルートで実行します。

```powershell
dotnet run --project .\src\Monia.App\Monia.App.csproj
```

## 3Dと壁紙の試験

「3Dを起動」では、表情通信に対応したローカルのUnity試験ビルドを選びます。Livelyで壁紙を操作する場合は、対応ビルドをLivelyに登録し、起動前のPowerShellで `MONIA_LIVELY_EXE` にLivelyの実行ファイル、`MONIA_LIVELY_WALLPAPER_FOLDER` に `LivelyInfo.json` を含む登録フォルダーを指定します。Phase 0の壁紙操作はLivelyのディスプレイ1が対象です。

Unityの比較プロジェクトを作る場合は、[Unityセットアップの記録](docs/comparison/unity-setup.md)を参照してください。
