using System.IO;
using Microsoft.Win32;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Monia.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>

public partial class MainWindow : Window{
    private Process? rendererProcess;
    private string? rendererPath;
    private string? resumeRendererPath;
    private bool sessionLocked;
    private readonly SemaphoreSlim wallpaperCommandGate = new(1);
    private bool wallpaperRequested;
    private bool wallpaperApplied;
    private bool rendererStopRequested;

    public MainWindow(){
        InitializeComponent();
        SystemEvents.SessionSwitch += OnSessionSwitch;

        string? imagePath = SettingsStore.LoadImagePath();
        if (string.IsNullOrWhiteSpace(imagePath)){
            return;
        }

        try{
            WallpaperImage.Source = LoadImage(imagePath);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or FormatException or NotSupportedException or ArgumentException){
            // XAMLで指定した初期画像をそのまま表示する。
        }

    }
    private void ChooseImage_Click(object sender, RoutedEventArgs e){
        var dialog = new OpenFileDialog{
            Title = "表示する画像を選ぶ",
            Filter = "画像ファイル (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };

        if (dialog.ShowDialog(this) != true){
            return;
        }

        try{
            BitmapImage image = LoadImage(dialog.FileName);
            WallpaperImage.Source = image;         
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or NotSupportedException){
            MessageBox.Show(this, "画像を読み込めませんでした。", "Monia");
            return;
        }

        try{
            SettingsStore.SaveImagePath(dialog.FileName);
        }

        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException){
            MessageBox.Show(this, "画像は表示できましたが、設定を保存できませんでした。", "Monia");
        }
    }
    private static BitmapImage LoadImage(string path){
        using var stream = File.OpenRead(path);
        var image = new BitmapImage();

        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();

        return image;
    }

    private void StartRenderer_Click(object sender, RoutedEventArgs e)
    {
        if (sessionLocked || rendererProcess is not null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Unity試験ビルドの Monia.exe を選ぶ",
            Filter = "実行ファイル (*.exe)|*.exe",
            FileName = "Monia.exe"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _ = RunRendererAsync(dialog.FileName);
    }

    private async Task RunRendererAsync(string executablePath)
    {
        if (sessionLocked || rendererProcess is not null)
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            MessageBox.Show(this, $"3Dを起動できませんでした。\n{ex.Message}", "Monia");
            return;
        }

        if (process is null)
        {
            RendererStatus.Text = "3Dを起動できませんでした";
            return;
        }

        rendererProcess = process;
        rendererPath = executablePath;
        StartRendererButton.IsEnabled = false;
        StopRendererButton.IsEnabled = true;
        RendererStatus.Text = "3Dプロセスが実行中";

        try
        {
            await process.WaitForExitAsync();
            int exitCode = process.ExitCode;
            RendererStatus.Text = exitCode == 0
                ? "3Dは停止中"
                : $"3Dが終了しました（終了コード {exitCode}）";
        }
        finally
        {
            process.Dispose();
            rendererProcess = null;
            rendererPath = null;
            rendererStopRequested = false;
            StartRendererButton.IsEnabled = !sessionLocked;
            StopRendererButton.IsEnabled = false;
            TryResumeRenderer();
        }
    }

    private void StopRenderer_Click(object sender, RoutedEventArgs e)
    {
        resumeRendererPath = null;
        RequestRendererStop();
    }

    private bool RequestRendererStop()
    {
        if (rendererProcess is null || rendererStopRequested)
        {
            return false;
        }

        try
        {
            if (rendererProcess.HasExited)
            {
                return false;
            }

            rendererStopRequested = rendererProcess.CloseMainWindow();
            RendererStatus.Text = rendererStopRequested
                ? "3Dの終了を待っています"
                : "Unityのウィンドウを確認してください";
            return rendererStopRequested;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            RendererStatus.Text = "3Dに終了を要求できませんでした";
            Debug.WriteLine(ex.Message);
            return false;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (rendererProcess is not null && !rendererProcess.HasExited)
        {
            e.Cancel = true;
            MessageBox.Show(this, "Moniaを閉じる前に、3Dを終了してください。", "Monia");
        }
    }
    private async void Smile_Click(object sender, RoutedEventArgs e)
    {
        await SendExpressionAsync(1);
    }

    private async void ResetSmile_Click(object sender, RoutedEventArgs e)
    {
        await SendExpressionAsync(0);
    }

    private async Task SendExpressionAsync(byte command)
    {
        SmileButton.IsEnabled = false;
        ResetSmileButton.IsEnabled = false;
        ExpressionStatus.Text = "Unityに接続中…";

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var pipe = new NamedPipeClientStream(
                ".", "Monia.Shino.Expression.Test",
                PipeDirection.InOut, PipeOptions.Asynchronous);

            await pipe.ConnectAsync(timeout.Token);

            ExpressionStatus.Text = "Unityの応答を待っています…";
            await pipe.WriteAsync(new byte[] { command }, 0, 1, timeout.Token);

            var reply = new byte[1];
            int received = await pipe.ReadAsync(reply, 0, 1, timeout.Token);

            if (received == 0)
            {
                ExpressionStatus.Text = "応答前に接続が閉じました";
            }
            else if (reply[0] == 1)
            {
                ExpressionStatus.Text = command == 1 ? "笑顔にしました" : "笑顔を解除しました";
            }
            else
            {
                ExpressionStatus.Text = "Unityが成功を返しませんでした";
            }
        }
        catch (OperationCanceledException)
        {
            ExpressionStatus.Text = "Unityとの通信がタイムアウトしました";
        }
        catch (IOException)
        {
            ExpressionStatus.Text = "Unityとの通信に失敗しました";
        }
        catch (UnauthorizedAccessException)
        {
            ExpressionStatus.Text = "パイプへのアクセスが拒否されました";
        }
        finally
        {
            SmileButton.IsEnabled = true;
            ResetSmileButton.IsEnabled = true;
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e){
        if (e.Reason is not (SessionSwitchReason.SessionLock
            or SessionSwitchReason.SessionUnlock)){
            return;
        }

        WriteSessionLog(e.Reason.ToString());
        _ = Dispatcher.InvokeAsync(() => ApplySessionChange(e.Reason));
    }

    private static void WriteSessionLog(string message)
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Monia");

        string logPath = Path.Combine(directory, "session.log");

        try{
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                logPath,
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException){
            Debug.WriteLine($"セッションログを書き込めませんでした: {ex.Message}");
        }
    }

    private void ApplySessionChange(SessionSwitchReason reason)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (reason == SessionSwitchReason.SessionLock)
        {
            sessionLocked = true;
            _ = UpdateWallpaperAsync(locked: true);
            StartRendererButton.IsEnabled = false;

            if (rendererPath is string path && RequestRendererStop())
            {
                resumeRendererPath = path;
            }

            return;
        }

        sessionLocked = false;
        _ = UpdateWallpaperAsync(locked: false);
        StartRendererButton.IsEnabled = rendererProcess is null;
        TryResumeRenderer();
    }

    private void TryResumeRenderer()
    {
        if (sessionLocked || rendererProcess is not null
            || resumeRendererPath is not string path)
        {
            return;
        }

        resumeRendererPath = null;
        _ = RunRendererAsync(path);
    }

    protected override void OnClosed(EventArgs e){
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        resumeRendererPath = null;
        base.OnClosed(e);
    }
    private async void ApplyWallpaper_Click(object sender, RoutedEventArgs e)
    {
        await UpdateWallpaperAsync(locked: false, requested: true);
    }

    private async void CloseWallpaper_Click(object sender, RoutedEventArgs e)
    {
        await UpdateWallpaperAsync(locked: sessionLocked, requested: false);
    }
    private async Task UpdateWallpaperAsync(bool locked, bool? requested = null)
    {
        await wallpaperCommandGate.WaitAsync();

        try
        {
            if (!IsLoaded)
            {
                return;
            }

            if (requested is bool manualChoice)
            {
                wallpaperRequested = manualChoice;
                locked = sessionLocked;

                // 手動で押した場合はLivelyに操作を送る。
                // ロック中の手動適用は解除後まで待つ。
                if (manualChoice && locked)
                {
                    return;
                }

                if (await RunWallpaperCommandAsync(manualChoice))
                {
                    wallpaperApplied = manualChoice;
                }
                else if (manualChoice)
                {
                    wallpaperRequested = wallpaperApplied;
                }

                return;
            }

            bool shouldApply = wallpaperRequested && !locked;
            if (shouldApply != wallpaperApplied &&
                await RunWallpaperCommandAsync(shouldApply))
            {
                wallpaperApplied = shouldApply;
            }
        }
        finally
        {
            wallpaperCommandGate.Release();
        }
    }

    private async Task<bool> RunWallpaperCommandAsync(bool apply)
    {
        if (apply && rendererProcess is not null)
        {
            WallpaperStatus.Text = "先に通常ウィンドウの3Dを終了してください";
            return false;
        }

        string? livelyExe = Environment.GetEnvironmentVariable("MONIA_LIVELY_EXE");
        string? wallpaperFolder =
            Environment.GetEnvironmentVariable("MONIA_LIVELY_WALLPAPER_FOLDER");

        if (livelyExe is null || !File.Exists(livelyExe))
        {
            WallpaperStatus.Text = "Lively.exeの場所を確認してください";
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = livelyExe,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(apply ? "setwp" : "closewp");

        if (apply)
        {
            if (wallpaperFolder is null ||
                !File.Exists(Path.Combine(wallpaperFolder, "LivelyInfo.json")))
            {
                WallpaperStatus.Text = "壁紙の登録フォルダーを確認してください";
                return false;
            }

            startInfo.ArgumentList.Add("--file");
            startInfo.ArgumentList.Add(wallpaperFolder);
        }

        startInfo.ArgumentList.Add("--monitor");
        startInfo.ArgumentList.Add("1");

        ApplyWallpaperButton.IsEnabled = false;
        CloseWallpaperButton.IsEnabled = false;
        WallpaperStatus.Text = "Livelyに指示を送っています…";
        string operation = apply ? "setwp" : "closewp";
        WriteSessionLog($"Lively {operation}: 開始");

        try
        {
            using Process? command = Process.Start(startInfo);
            if (command is null)
            {
                WallpaperStatus.Text = "Livelyのコマンドを起動できませんでした";
                WriteSessionLog($"Lively {operation}: 起動失敗");
                return false;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await command.WaitForExitAsync(timeout.Token);

            int exitCode = command.ExitCode;
            WriteSessionLog($"Lively {operation}: 終了コード {exitCode}");
            WallpaperStatus.Text = exitCode == 0
                ? "デスクトップで結果を確認してください"
                : $"Livelyコマンドの終了コード: {exitCode}";
            return exitCode == 0;
        }
        catch (Win32Exception ex)
        {
            WallpaperStatus.Text = $"Livelyを起動できませんでした: {ex.Message}";
            WriteSessionLog($"Lively {operation}: 起動失敗 ({ex.NativeErrorCode})");
            return false;
        }
        catch (OperationCanceledException)
        {
            WallpaperStatus.Text = "Livelyの処理状況を確認してください";
            WriteSessionLog($"Lively {operation}: 待機タイムアウト");
            return false;
        }
        finally
        {
            ApplyWallpaperButton.IsEnabled = true;
            CloseWallpaperButton.IsEnabled = true;
        }
    }
}
