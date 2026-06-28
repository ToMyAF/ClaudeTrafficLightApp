using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace ClaudeTrafficLight;

public partial class App : System.Windows.Application
{
    private NotifyIcon _notifyIcon;
    private MainWindow _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 先创建并显示主窗口
            _mainWindow = new MainWindow();
            _mainWindow.Show();

            // 创建系统托盘图标 - 先用简单的系统图标测试
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "Claude Traffic Light";
            _notifyIcon.Icon = SystemIcons.Information;  // 使用系统内置图标
            _notifyIcon.Visible = true;

            // 双击托盘图标 → 显示/隐藏
            _notifyIcon.DoubleClick += (s, args) =>
            {
                _mainWindow?.ToggleVisibility();
            };

            // 托盘右键菜单
            var contextMenu = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem("显示/隐藏");
            showItem.Click += (s, args) =>
            {
                _mainWindow?.ToggleVisibility();
            };
            contextMenu.Items.Add(showItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, args) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Current.Shutdown();
            };
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // 启动时显示气球通知
            _notifyIcon.ShowBalloonTip(2000, "Claude Traffic Light", "红绿灯已启动，正在监控Claude Code状态...", ToolTipIcon.Info);

            // 修改主窗口关闭行为
            _mainWindow.Closing += (s, args) =>
            {
                if (_mainWindow.IsExitRequested)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                else
                {
                    args.Cancel = true;
                    _mainWindow.Hide();
                    _notifyIcon.ShowBalloonTip(1000, "Claude Traffic Light", "已最小化到系统托盘", ToolTipIcon.Info);
                }
            };
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"启动错误: {ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
