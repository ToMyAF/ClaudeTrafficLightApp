using System.IO;
using System.Text.Json;
namespace ClaudeTrafficLight.Services;
public class AppSettings
{
    public double Opacity { get; set; } = 0.8;
    public int RedFlashCount { get; set; } = 3;
    public int CarouselIntervalMs { get; set; } = 3000;
    public bool EdgeSnapEnabled { get; set; } = true;
    public int WebSocketPort { get; set; } = 19876;
}
public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeTrafficLight", "settings.json"
    );
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }
    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
