using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace ClaudeTrafficLight.Services;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss.fff}] {ClientId} | {State,-15} | {Message}";
    }
}
public class AppSettings
{
    public double Opacity { get; set; } = 0.8;
    public int RedFlashCount { get; set; } = 3;
    public int CarouselIntervalMs { get; set; } = 3000;
    public bool EdgeSnapEnabled { get; set; } = true;
    public int SnapThreshold { get; set; } = 60;          // 吸边阈值 (像素)
    public int WebSocketPort { get; set; } = 19876;
    public int BubbleTextLength { get; set; } = 50;
    public bool ShowBubbleAlways { get; set; } = true;
    public string BackgroundColor { get; set; } = "#555555";
    public bool EnableMessageLogging { get; set; } = false;
    public int MaxLogEntries { get; set; } = 100;
    public int FilePollingIntervalMs { get; set; } = 500;

    // LED 显示相关配置
    public double ScrollSpeed { get; set; } = 0.8;        // LED 滚动速度 (像素/次)
    public int LightWidth { get; set; } = 64;             // 信号灯宽度（64px，与XAML一致）
    public int LedWidth { get; set; } = 36;               // LED 文字区域宽度
    public int TotalWindowWidth { get; set; } = 124;      // 窗口总宽度（64+36+24间距）
    public string LedBackground { get; set; } = "#1A000000"; // LED 区域背景色
    public bool StopScrollAfterLoop { get; set; } = true; // 滚动一轮后停止

    // 自定义文案配置
    public string YellowLabelText { get; set; } = "思考中";
    public string YellowMessageText { get; set; } = "AI正在思考您的问题...";
    public string RedLabelText { get; set; } = "需要确认";
    public string RedMessageText { get; set; } = "请确认下一步操作...";
    public string GreenLabelText { get; set; } = "输出中";
    public string GreenMessageText { get; set; } = "正在生成回答...";

    // GIF动画配置
    public string YellowGifPath { get; set; } = "";
    public string RedGifPath { get; set; } = "";
    public string GreenGifPath { get; set; } = "";
    public bool EnableGifAnimation { get; set; } = true;
    public double GifLightOpacity { get; set; } = 1.0; // 有GIF时灯的透明度 (0-1)
    public double LightRingSize { get; set; } = 40.0; // 灯环大小（像素）
    public double GifSize { get; set; } = 30.0; // GIF显示大小（像素）

    // 开机自启配置
    public bool AutoStart { get; set; } = false;
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Error loading settings: {ex.Message}");
        }
        return new AppSettings();
    }
    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Error saving settings: {ex.Message}");
        }
    }
}
