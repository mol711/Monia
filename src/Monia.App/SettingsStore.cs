using System;
using System.IO;
using System.Text.Json;

namespace Monia.App;

internal static class SettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Monia");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDirectory, "settings.json");

    public static string? LoadImagePath()
    {
        try
        {
            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<Settings>(json)?.ImagePath;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void SaveImagePath(string imagePath)
    {
        Directory.CreateDirectory(SettingsDirectory);
        string json = JsonSerializer.Serialize(new Settings(imagePath));
        File.WriteAllText(SettingsPath, json);
    }

    private sealed record Settings(string ImagePath);
}