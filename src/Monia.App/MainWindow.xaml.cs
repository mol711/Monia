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
using System.Windows.Shapes;

namespace Monia.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window{
    public MainWindow(){
    InitializeComponent();

    string? imagePath = SettingsStore.LoadImagePath();
    if (string.IsNullOrWhiteSpace(imagePath))
    {
        return;
    }

    try
    {
        WallpaperImage.Source = LoadImage(imagePath);
    }
    catch (Exception ex) when (
        ex is IOException or UnauthorizedAccessException
            or FormatException or NotSupportedException or ArgumentException)
    {
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
    private static BitmapImage LoadImage(string path)
    {
    using var stream = File.OpenRead(path);
    var image = new BitmapImage();

    image.BeginInit();
    image.CacheOption = BitmapCacheOption.OnLoad;
    image.StreamSource = stream;
    image.EndInit();

    return image;
    }
}