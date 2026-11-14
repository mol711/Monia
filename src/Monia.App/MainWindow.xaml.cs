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

    public MainWindow(){
        InitializeComponent();

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

    private async void StartRenderer_Click(object sender, RoutedEventArgs e)
    {
        if (rendererProcess is not null)
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

        var startInfo = new ProcessStartInfo
        {
            FileName = dialog.FileName,
            WorkingDirectory = Path.GetDirectoryName(dialog.FileName)!,
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
            StartRendererButton.IsEnabled = true;
            StopRendererButton.IsEnabled = false;
        }
    }

    private void StopRenderer_Click(object sender, RoutedEventArgs e)
    {
        if (rendererProcess is null || rendererProcess.HasExited)
        {
            return;
        }

        try
        {
            RendererStatus.Text = rendererProcess.CloseMainWindow()
                ? "3Dの終了を待っています"
                : "Unityのウィンドウを確認してください";
        }
        catch (InvalidOperationException)
        {
            // Unityが同時に閉じたときは、終了の待機側で画面を更新する。
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
}
