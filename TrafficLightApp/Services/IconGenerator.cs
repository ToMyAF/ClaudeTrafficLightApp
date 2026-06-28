using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ClaudeTrafficLight.Services;

/// <summary>
/// 动态生成红绿灯托盘图标
/// </summary>
public static class IconGenerator
{
    /// <summary>
    /// 创建红绿灯图标（默认状态）
    /// </summary>
    public static Icon CreateTrafficLightIcon()
    {
        const int size = 16; // 托盘图标标准大小
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // 绘制圆形背景（深灰色）
        var bgRect = new Rectangle(1, 1, size - 2, size - 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(255, 45, 45, 48));
        g.FillEllipse(bgBrush, bgRect);

        // 绘制边框
        using var pen = new Pen(Color.FromArgb(255, 80, 80, 85), 1);
        g.DrawEllipse(pen, bgRect);

        // 绘制三个小圆（红绿灯）
        int lightSize = 3;
        int centerX = size / 2;

        // 红灯 (顶部)
        DrawLight(g, centerX, 5, lightSize, Color.FromArgb(255, 255, 69, 58));

        // 黄灯 (中间)
        DrawLight(g, centerX, 9, lightSize, Color.FromArgb(255, 255, 204, 0));

        // 绿灯 (底部)
        DrawLight(g, centerX, 13, lightSize, Color.FromArgb(255, 50, 215, 75));

        // 转换为 Icon
        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    /// <summary>
    /// 创建带状态的红绿灯图标
    /// </summary>
    public static Icon CreateTrafficLightIcon(string state)
    {
        const int size = 16;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // 绘制圆形背景
        var bgRect = new Rectangle(1, 1, size - 2, size - 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(255, 45, 45, 48));
        g.FillEllipse(bgBrush, bgRect);

        using var pen = new Pen(Color.FromArgb(255, 80, 80, 85), 1);
        g.DrawEllipse(pen, bgRect);

        int lightSize = 3;
        int centerX = size / 2;

        // 根据状态点亮对应灯
        bool isRed = state == "needs_confirm";
        bool isYellow = state == "thinking";
        bool isGreen = state == "writing";

        // 红灯 (顶部)
        DrawLight(g, centerX, 5, lightSize,
            isRed ? Color.FromArgb(255, 255, 69, 58) : Color.FromArgb(100, 80, 20, 20));

        // 黄灯 (中间)
        DrawLight(g, centerX, 9, lightSize,
            isYellow ? Color.FromArgb(255, 255, 204, 0) : Color.FromArgb(100, 80, 70, 20));

        // 绿灯 (底部)
        DrawLight(g, centerX, 13, lightSize,
            isGreen ? Color.FromArgb(255, 50, 215, 75) : Color.FromArgb(100, 20, 70, 20));

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private static void DrawLight(Graphics g, int x, int y, int size, Color color)
    {
        var rect = new Rectangle(x - size / 2, y - size / 2, size, size);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, rect);

        // 添加高光效果
        using var highlightBrush = new SolidBrush(Color.FromArgb(120, 255, 255, 255));
        var highlightRect = new Rectangle(x - size / 2 + 1, y - size / 2 + 1, size / 2, size / 2);
        g.FillEllipse(highlightBrush, highlightRect);
    }
}
