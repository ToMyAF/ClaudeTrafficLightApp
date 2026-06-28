using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ClaudeTrafficLight.Services;

namespace ClaudeTrafficLight
{
    // 状态文件 JSON 模型
    public class TrafficLightStateFile
    {
        public string state { get; set; } = ""; // "red" | "yellow" | "green" | "idle"
        public string content { get; set; } = ""; // Claude Code 输出内容
        public long timestamp { get; set; } = 0;
        public string label { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        public bool IsExitRequested { get; private set; } = false;

        public double WindowOpacity
        {
            get { return (double)GetValue(WindowOpacityProperty); }
            set { SetValue(WindowOpacityProperty, value); }
        }
        public static readonly DependencyProperty WindowOpacityProperty =
            DependencyProperty.Register("WindowOpacity", typeof(double),
            typeof(MainWindow), new PropertyMetadata(0.8));

        private readonly WebSocketServer _wsServer;
        private readonly AppSettings _settings;
        private readonly Dictionary<string, StateChangePayload> _instances = new();
        private string? _currentInstanceId;
        private int _carouselIndex;
        private readonly System.Timers.Timer _carouselTimer;
        private readonly System.Timers.Timer _filePollingTimer;
        private readonly System.Timers.Timer _scrollTimer; // LED 文字自动滚动定时器
        private string _currentState = "idle";
        private string _lastFileState = "";
        private readonly List<LogEntry> _messageLogs = new();

        // 颜色到状态的映射
        private readonly Dictionary<string, (string state, string message, Brush labelBrush)> _colorStateMap = new()
        {
            { "red", ("needs_confirm", "需要用户确认", new SolidColorBrush(Color.FromRgb(255, 68, 68))) },
            { "yellow", ("thinking", "思考处理中", new SolidColorBrush(Color.FromRgb(255, 224, 102))) },
            { "green", ("writing", "输出结果中", new SolidColorBrush(Color.FromRgb(102, 255, 153))) },
        };

        // 状态文件路径
        private readonly string _stateFile = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            "cc_traffic_light_state"
        );

        // 动画资源引用
        private Storyboard? _breatheAnim;
        private Storyboard? _bubbleEnterAnim;
        private Storyboard? _bubbleExitAnim;

        // 颜色和画笔
        private readonly Color _yellowColor = Color.FromRgb(255, 215, 0);
        private readonly Color _greenColor = Color.FromRgb(0, 255, 127);
        private readonly Color _redColor = Color.FromRgb(255, 68, 68);
        private readonly Brush _offBrush = new SolidColorBrush(Color.FromRgb(78, 78, 78));
        private readonly Brush _grayStroke = new SolidColorBrush(Color.FromRgb(90, 90, 90));

        // 状态标签颜色
        private readonly Brush _yellowLabelBrush = new SolidColorBrush(Color.FromRgb(255, 224, 102));
        private readonly Brush _greenLabelBrush = new SolidColorBrush(Color.FromRgb(102, 255, 153));
        private readonly Brush _redLabelBrush = new SolidColorBrush(Color.FromRgb(255, 102, 102));

        public MainWindow()
        {
            InitializeComponent();
            _settings = SettingsService.Load();
            WindowOpacity = _settings.Opacity;

            MouseLeftButtonDown += (s, e) => DragMove();
            MouseLeftButtonUp += (s, e) => ApplyEdgeSnap();
            MouseEnter += (s, e) =>
            {
                WindowOpacity = 1.0;
                LedTextContainer.Opacity = 1.0;
                LedTextBlock.Opacity = 1.0;
            };
            MouseLeave += (s, e) =>
            {
                WindowOpacity = _settings.Opacity;
                LedTextContainer.Opacity = _settings.Opacity;
                LedTextBlock.Opacity = _settings.Opacity;
            };

            _wsServer = new WebSocketServer(_settings.WebSocketPort);
            _wsServer.StateChanged += OnStateChanged;
            _wsServer.ServerStarted += OnServerStarted;
            _wsServer.ServerError += OnServerError;
            _wsServer.ClientConnected += OnClientConnected;
            _wsServer.ClientDisconnected += OnClientDisconnected;

            Loaded += MainWindow_Loaded;
            Closing += async (s, e) =>
            {
                _filePollingTimer.Stop();
                _scrollTimer.Stop();
                await _wsServer.StopAsync();
            };

            _carouselTimer = new System.Timers.Timer(_settings.CarouselIntervalMs);
            _carouselTimer.Elapsed += (s, e) => CarouselNext();
            _carouselTimer.Start();

            // 初始化文件轮询定时器（使用配置的间隔）
            _filePollingTimer = new System.Timers.Timer(_settings.FilePollingIntervalMs);
            _filePollingTimer.Elapsed += (s, e) => CheckStateFile();

            // 初始化 LED 文字滚动定时器（每50ms滚动一点）
            _scrollTimer = new System.Timers.Timer(50);
            _scrollTimer.Elapsed += (s, e) => ScrollLedText();

            // 初始化端口文本框
            PortTextBox.Text = _settings.WebSocketPort.ToString();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载动画资源
            _breatheAnim = FindResource("BreatheAnimation") as Storyboard;
            _bubbleEnterAnim = FindResource("BubbleEnterAnimation") as Storyboard;
            _bubbleExitAnim = FindResource("BubbleExitAnimation") as Storyboard;

            // 初始窗口宽度设置为只有信号灯宽度（LED 隐藏时）
            Width = _settings.LightWidth;

            // 应用保存的背景色
            try
            {
                if (!string.IsNullOrEmpty(_settings.BackgroundColor))
                {
                    var color = (Color)ColorConverter.ConvertFromString(_settings.BackgroundColor);
                    LightContainer.Background = new SolidColorBrush(color);
                    BgColorTextBox.Text = _settings.BackgroundColor;
                }
            }
            catch { }

            // 应用宽度配置
            Width = _settings.TotalWindowWidth;
            LightContainer.Width = _settings.LightWidth;
            LedTextContainer.Width = _settings.LedWidth;
            LedCanvas.Width = _settings.LedWidth;

            // 应用LED背景色
            try
            {
                if (!string.IsNullOrEmpty(_settings.LedBackground))
                {
                    var ledBgColor = (Color)ColorConverter.ConvertFromString(_settings.LedBackground);
                    LedTextContainer.Background = new SolidColorBrush(ledBgColor);
                }
            }
            catch { }

            // 启动WebSocket服务器（兼容旧模式）
            await _wsServer.StartAsync();

            // 启动文件轮询
            _filePollingTimer.Start();

            // 初始检查一次
            CheckStateFile();
        }

        // 检查状态文件（支持 JSON 和简单文本两种格式）
        private void CheckStateFile()
        {
            try
            {
                if (!File.Exists(_stateFile))
                {
                    return;
                }

                var rawContent = File.ReadAllText(_stateFile).Trim();

                // 尝试解析为 JSON 格式
                if (rawContent.StartsWith("{"))
                {
                    var stateData = JsonSerializer.Deserialize<TrafficLightStateFile>(rawContent);
                    if (stateData != null && !string.IsNullOrEmpty(stateData.state))
                    {
                        var stateKey = stateData.state.ToLower();
                        var stateKeyWithContent = $"{stateKey}:{stateData.content}";

                        if (stateKeyWithContent != _lastFileState && _colorStateMap.ContainsKey(stateKey))
                        {
                            _lastFileState = stateKeyWithContent;
                            var (state, defaultMessage, labelBrush) = _colorStateMap[stateKey];
                            var displayMessage = !string.IsNullOrEmpty(stateData.content)
                                ? stateData.content
                                : defaultMessage;

                            Dispatcher.Invoke(() =>
                            {
                                SetLightOn(state, displayMessage);
                            });
                        }
                    }
                }
                // 兼容旧的简单文本格式
                else
                {
                    var content = rawContent.ToLower();
                    if (!string.IsNullOrEmpty(content) && content != _lastFileState && _colorStateMap.ContainsKey(content))
                    {
                        _lastFileState = content;
                        var (state, message, labelBrush) = _colorStateMap[content];

                        Dispatcher.Invoke(() =>
                        {
                            SetLightOn(state, message);
                        });
                    }
                }
            }
            catch
            {
                // 静默失败（文件可能被占用等）
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (OpacitySlider != null)
            {
                OpacitySlider.Value = _settings.Opacity;
            }
            if (BubbleLengthSlider != null)
            {
                BubbleLengthSlider.Value = _settings.BubbleTextLength;
            }
            if (ShowBubbleCheckBox != null)
            {
                ShowBubbleCheckBox.IsChecked = _settings.ShowBubbleAlways;
            }
            if (EnableLoggingCheckBox != null)
            {
                EnableLoggingCheckBox.IsChecked = _settings.EnableMessageLogging;
            }
            if (MaxLogEntriesTextBox != null)
            {
                MaxLogEntriesTextBox.Text = _settings.MaxLogEntries.ToString();
            }
            PortTextBox.Text = _settings.WebSocketPort.ToString();
            PollingIntervalTextBox.Text = _settings.FilePollingIntervalMs.ToString();

            // LED 显示设置
            if (SnapThresholdSlider != null)
            {
                SnapThresholdSlider.Value = _settings.SnapThreshold;
            }
            if (StopScrollCheckBox != null)
            {
                StopScrollCheckBox.IsChecked = _settings.StopScrollAfterLoop;
            }
            if (ScrollSpeedSlider != null)
            {
                ScrollSpeedSlider.Value = _settings.ScrollSpeed;
            }
            if (WindowWidthTextBox != null)
            {
                WindowWidthTextBox.Text = _settings.TotalWindowWidth.ToString();
            }
            if (LightWidthTextBox != null)
            {
                LightWidthTextBox.Text = _settings.LightWidth.ToString();
            }
            if (LedWidthTextBox != null)
            {
                LedWidthTextBox.Text = _settings.LedWidth.ToString();
            }

            // 文案设置
            if (YellowLabelTextBox != null)
            {
                YellowLabelTextBox.Text = _settings.YellowLabelText;
            }
            if (YellowMessageTextBox != null)
            {
                YellowMessageTextBox.Text = _settings.YellowMessageText;
            }
            if (RedLabelTextBox != null)
            {
                RedLabelTextBox.Text = _settings.RedLabelText;
            }
            if (RedMessageTextBox != null)
            {
                RedMessageTextBox.Text = _settings.RedMessageText;
            }
            if (GreenLabelTextBox != null)
            {
                GreenLabelTextBox.Text = _settings.GreenLabelText;
            }
            if (GreenMessageTextBox != null)
            {
                GreenMessageTextBox.Text = _settings.GreenMessageText;
            }

            // GIF动画设置
            if (EnableGifCheckBox != null)
            {
                EnableGifCheckBox.IsChecked = _settings.EnableGifAnimation;
            }

            // 开机启动状态
            var autoStartMenuItem = ContextMenu?.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header?.ToString() == "开机启动");
            if (autoStartMenuItem != null)
            {
                autoStartMenuItem.IsChecked = _settings.AutoStart;
            }
            if (YellowGifPathTextBox != null)
            {
                YellowGifPathTextBox.Text = _settings.YellowGifPath;
            }
            if (RedGifPathTextBox != null)
            {
                RedGifPathTextBox.Text = _settings.RedGifPath;
            }
            if (GreenGifPathTextBox != null)
            {
                GreenGifPathTextBox.Text = _settings.GreenGifPath;
            }
        }

        private void OnServerStarted(object? sender, int port)
        {
            Dispatcher.Invoke(() =>
            {
                SetAllLightsOff();
                ServerStatusIcon.Text = "✅";
            });
        }

        private void OnServerError(object? sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                SetLightOn("error");
                ServerStatusIcon.Text = "❌";
            });
        }

        private void OnClientConnected(object? sender, int count)
        {
            Dispatcher.Invoke(() =>
            {
                ServerStatusIcon.Text = count > 0 ? "🔌" : "✅";

                // 客户端连接成功：绿灯常量 + 气泡提示
                SetLightState(GreenLight, GreenGlow, true, _greenColor);
                StartBreathingAnimation(GreenLight);
                _currentState = "writing";

                // 显示 LED 提示
                var message = count > 1
                    ? $"已连接 {count} 个客户端\n等待状态消息..."
                    : "VSCode 扩展已连接\n等待 Claude Code 状态...";

                ShowLedText("连接成功", message, _greenLabelBrush);
            });
        }

        private void OnClientDisconnected(object? sender, int count)
        {
            Dispatcher.Invoke(() =>
            {
                ServerStatusIcon.Text = count > 0 ? "🔌" : "✅";
                if (count == 0)
                {
                    _currentState = "idle";
                    SetAllLightsOff();
                }
            });
        }

        private void SetAllLightsOff()
        {
            // 关闭所有GIF，显示静态灯
            YellowGif.Visibility = Visibility.Collapsed;
            GreenGif.Visibility = Visibility.Collapsed;
            RedGif.Visibility = Visibility.Collapsed;
            YellowLight.Visibility = Visibility.Visible;
            GreenLight.Visibility = Visibility.Visible;
            RedLight.Visibility = Visibility.Visible;

            SetLightState(YellowLight, YellowGlow, false, _yellowColor);
            SetLightState(GreenLight, GreenGlow, false, _greenColor);
            SetLightState(RedLight, RedGlow, false, _redColor);
            HideLedText();
        }

        private void SetLightOn(string state, string? message = null)
        {
            SetAllLightsOff();

            Ellipse targetLight;
            DropShadowEffect targetGlow;
            Color glowColor;
            Brush labelBrush;
            string labelText;
            string defaultMessage;

            switch (state)
            {
                case "idle":
                    _currentState = state;
                    if (!string.IsNullOrEmpty(message))
                    {
                        ShowLedText("空闲", message, _yellowLabelBrush);
                    }
                    else
                    {
                        HideLedText();
                    }
                    return;
                case "thinking":
                    targetLight = YellowLight;
                    targetGlow = YellowGlow;
                    glowColor = _yellowColor;
                    labelBrush = _yellowLabelBrush;
                    labelText = _settings.YellowLabelText;
                    defaultMessage = _settings.YellowMessageText;
                    break;
                case "writing":
                    targetLight = GreenLight;
                    targetGlow = GreenGlow;
                    glowColor = _greenColor;
                    labelBrush = _greenLabelBrush;
                    labelText = _settings.GreenLabelText;
                    defaultMessage = _settings.GreenMessageText;
                    break;
                case "needs_confirm":
                    targetLight = RedLight;
                    targetGlow = RedGlow;
                    glowColor = _redColor;
                    labelBrush = _redLabelBrush;
                    labelText = _settings.RedLabelText;
                    defaultMessage = _settings.RedMessageText;
                    break;
                case "error":
                    targetLight = RedLight;
                    targetGlow = RedGlow;
                    glowColor = _redColor;
                    labelBrush = _redLabelBrush;
                    labelText = "错误";
                    defaultMessage = "发生错误";
                    break;
                default:
                    _currentState = "idle";
                    return;
            }

            _currentState = state;

            // 激活灯光
            SetLightState(targetLight, targetGlow, true, glowColor);

            // 如果启用了GIF动画，显示对应的GIF
            if (_settings.EnableGifAnimation)
            {
                if (state == "thinking" && !string.IsNullOrEmpty(_settings.YellowGifPath) && File.Exists(_settings.YellowGifPath))
                {
                    YellowGif.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(_settings.YellowGifPath));
                    YellowGif.Visibility = Visibility.Visible;
                    YellowLight.Visibility = Visibility.Collapsed; // 隐藏静态灯
                }
                else if (state == "writing" && !string.IsNullOrEmpty(_settings.GreenGifPath) && File.Exists(_settings.GreenGifPath))
                {
                    GreenGif.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(_settings.GreenGifPath));
                    GreenGif.Visibility = Visibility.Visible;
                    GreenLight.Visibility = Visibility.Collapsed; // 隐藏静态灯
                }
                else if (state == "needs_confirm" && !string.IsNullOrEmpty(_settings.RedGifPath) && File.Exists(_settings.RedGifPath))
                {
                    RedGif.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(_settings.RedGifPath));
                    RedGif.Visibility = Visibility.Visible;
                    RedLight.Visibility = Visibility.Collapsed; // 隐藏静态灯
                }
            }

            // 开始呼吸动画（仅对静态灯）
            if (state == "thinking" && YellowGif.Visibility == Visibility.Collapsed)
            {
                StartBreathingAnimation(targetLight);
            }
            else if (state == "writing" && GreenGif.Visibility == Visibility.Collapsed)
            {
                StartBreathingAnimation(targetLight);
            }
            else if (state == "needs_confirm" && RedGif.Visibility == Visibility.Collapsed)
            {
                StartBreathingAnimation(targetLight);
            }

            // 确定显示的消息：优先使用传入的消息，否则使用自定义默认消息
            string displayMessage = !string.IsNullOrEmpty(message) ? message : defaultMessage;

            // 显示 LED 文字
            if (!string.IsNullOrEmpty(displayMessage))
            {
                if (displayMessage.Length > _settings.BubbleTextLength)
                {
                    displayMessage = displayMessage.Substring(0, _settings.BubbleTextLength) + "...";
                }
                ShowLedText(labelText, displayMessage, labelBrush);
            }
            else if (_settings.ShowBubbleAlways)
            {
                ShowLedText(labelText, "", labelBrush);
            }
            else
            {
                HideLedText();
            }
        }

        private void SetLightState(Ellipse light, DropShadowEffect glow, bool isOn, Color glowColor)
        {
            if (isOn)
            {
                // 使用XAML中定义的渐变画笔
                RadialGradientBrush? brush = null;
                if (glowColor == _yellowColor)
                {
                    brush = FindResource("YellowOnBrush") as RadialGradientBrush;
                }
                else if (glowColor == _greenColor)
                {
                    brush = FindResource("GreenOnBrush") as RadialGradientBrush;
                }
                else if (glowColor == _redColor)
                {
                    brush = FindResource("RedOnBrush") as RadialGradientBrush;
                }

                light.Fill = brush != null ? brush : new SolidColorBrush(glowColor);
                light.Stroke = new SolidColorBrush(glowColor);
                light.Opacity = 1.0;

                // 发光效果
                glow.BlurRadius = 18;
                glow.Opacity = 0.9;
            }
            else
            {
                light.Fill = _offBrush;
                light.Stroke = _grayStroke;
                light.Opacity = 0.4;
                glow.BlurRadius = 0;
                glow.Opacity = 0;
            }
        }

        private void StartBreathingAnimation(Ellipse light)
        {
            if (_breatheAnim == null) return;

            var anim = _breatheAnim.Clone();
            Storyboard.SetTarget(anim, light);
            anim.Begin();
        }

        private double _scrollOffset = 0; // 纵向滚动偏移量
        private bool _hasCompletedFirstLoop = false; // 是否已完成第一轮滚动

        private void ShowLedText(string label, string message, Brush textColor)
        {
            // 先保存当前位置用于吸边检测
            var workingArea = SystemParameters.WorkArea;
            var lightWidth = _settings.LightWidth;

            // 检测是否吸附在左边缘或右边缘
            var isSnappedToLeft = Math.Abs(Left - workingArea.Left) < 5;
            var isSnappedToRight = Math.Abs(Left + lightWidth - (workingArea.Left + workingArea.Width)) < 5;

            // 设置 LED 文字颜色和发光效果（与信号灯共享透明度）
            var color = ((SolidColorBrush)textColor).Color;
            LedTextBlock.Foreground = textColor;
            LedTextBlock.Opacity = WindowOpacity; // 与信号灯共享透明度设置
            LedTextGlow.Color = color;

            // 设置 LED 容器背景色（跟随信号灯背景色设置，半透明效果）
            try
            {
                if (!string.IsNullOrEmpty(_settings.BackgroundColor))
                {
                    var bgColor = (Color)ColorConverter.ConvertFromString(_settings.BackgroundColor);
                    bgColor.A = 40; // 设置 15% 透明度
                    LedTextContainer.Background = new SolidColorBrush(bgColor);
                }
            }
            catch { }

            // 将文字转换为竖排（每个字符换行）
            var fullText = string.IsNullOrEmpty(message)
                ? label
                : $"{label} {message}";

            // 每个字符占一行，实现竖排效果
            var verticalText = string.Join("\n", fullText.ToCharArray());
            LedTextBlock.Text = verticalText;

            // 显示 LED 容器
            LedTextContainer.Visibility = Visibility.Visible;
            LedTextContainer.Opacity = WindowOpacity; // 共享透明度

            // 调整窗口大小以包含 LED
            var totalWidth = _settings.TotalWindowWidth;
            if (totalWidth < lightWidth + _settings.LedWidth + 10)
            {
                totalWidth = lightWidth + _settings.LedWidth + 10;
            }
            Width = totalWidth;

            // 如果吸附在右边缘，需要调整窗口位置，保持信号灯位置不变
            if (isSnappedToRight)
            {
                // 信号灯保持在原位置，窗口向右扩展（实际上是向左移动窗口起点）
                Left = (workingArea.Left + workingArea.Width) - totalWidth;
            }

            // 重置滚动状态
            _scrollOffset = 184; // 从区域底部开始
            _hasCompletedFirstLoop = false;
            Canvas.SetTop(LedTextBlock, _scrollOffset);

            // 启动纵向滚动定时器
            _scrollTimer.Stop();
            _scrollTimer.Start();
        }

        // LED 文字纵向滚动（从下往上）
        private void ScrollLedText()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 如果设置了滚动一轮后停止，并且已完成第一轮，则停止滚动并隐藏 LED
                    if (_settings.StopScrollAfterLoop && _hasCompletedFirstLoop)
                    {
                        _scrollTimer.Stop();
                        HideLedText(); // 隐藏 LED 区域
                        return;
                    }

                    // 使用配置的滚动速度
                    _scrollOffset -= _settings.ScrollSpeed;

                    // 获取文字高度
                    var textHeight = LedTextBlock.ActualHeight;

                    // 当文字完全滚出顶部后
                    if (_scrollOffset < -textHeight - 20)
                    {
                        if (_settings.StopScrollAfterLoop)
                        {
                            // 标记已完成第一轮滚动，下次触发时停止
                            _hasCompletedFirstLoop = true;
                            _scrollOffset = 184;
                        }
                        else
                        {
                            // 循环滚动模式：重置到底部
                            _scrollOffset = 184;
                        }
                    }

                    Canvas.SetTop(LedTextBlock, _scrollOffset);
                });
            }
            catch
            {
                // 静默失败
            }
        }

        private void HideLedText()
        {
            // 记录隐藏前的位置和宽度
            var currentWidth = Width;
            var lightWidth = _settings.LightWidth;

            // 检测是否吸附在右边缘
            var workingArea = SystemParameters.WorkArea;
            var isSnappedToRight = Math.Abs(Left + currentWidth - (workingArea.Left + workingArea.Width)) < 5;

            // 隐藏 LED 容器
            LedTextContainer.Visibility = Visibility.Collapsed;
            LedTextBlock.Text = "";

            // 收缩窗口宽度到只有信号灯的宽度
            Width = lightWidth;

            // 如果之前吸附在右边缘，调整位置保持信号灯位置不变
            if (isSnappedToRight)
            {
                Left = (workingArea.Left + workingArea.Width) - lightWidth;
            }
        }

        private void ApplyEdgeSnap()
        {
            if (!_settings.EdgeSnapEnabled) return;

            var snapThreshold = _settings.SnapThreshold; // 使用配置的吸边阈值

            // 使用工作区域（排除任务栏）而不是全屏尺寸
            var workingArea = SystemParameters.WorkArea;
            var screenWidth = workingArea.Width;
            var screenHeight = workingArea.Height;
            var screenLeft = workingArea.Left;
            var screenTop = workingArea.Top;

            // 计算窗口相对于工作区域的位置
            var relativeLeft = Left - screenLeft;
            var relativeTop = Top - screenTop;

            // 左边吸附（信号灯位置）
            if (relativeLeft < snapThreshold) Left = screenLeft;

            // 顶边吸附
            if (relativeTop < snapThreshold) Top = screenTop;

            // 右边吸附 - 使用信号灯宽度判断，保持信号灯在边缘
            var snapWidth = _settings.LightWidth;
            if (relativeLeft + snapWidth > screenWidth - snapThreshold)
                Left = screenLeft + screenWidth - snapWidth;

            // 底边吸附
            if (relativeTop + Height > screenHeight - snapThreshold)
                Top = screenTop + screenHeight - Height;
        }

        private void OnStateChanged(object? sender, StateChangePayload e)
        {
            // 记录日志（如果启用）
            if (_settings.EnableMessageLogging)
            {
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    ClientId = e.vscodeWindowId,
                    State = e.state,
                    Message = e.message,
                    RawJson = System.Text.Json.JsonSerializer.Serialize(e)
                };

                _messageLogs.Insert(0, logEntry);

                // 限制日志条数
                if (_messageLogs.Count > _settings.MaxLogEntries)
                {
                    _messageLogs.RemoveRange(_settings.MaxLogEntries, _messageLogs.Count - _settings.MaxLogEntries);
                }
            }

            Dispatcher.Invoke(() =>
            {
                _instances[e.vscodeWindowId] = e;

                // 如果有红灯，优先显示
                if (e.state == "needs_confirm")
                {
                    _currentInstanceId = e.vscodeWindowId;
                    SetLightOn(e.state, e.message);
                }
                else if (_currentInstanceId == null || !HasAnyRedState())
                {
                    _currentInstanceId = e.vscodeWindowId;
                    SetLightOn(e.state, e.message);
                }
            });
        }

        private bool HasAnyRedState() => _instances.Values.Any(i => i.state == "needs_confirm");

        private void CarouselNext()
        {
            if (HasAnyRedState()) return; // 红灯时不轮播
            if (_instances.Count == 0) return;

            Dispatcher.Invoke(() =>
            {
                _carouselIndex = (_carouselIndex + 1) % _instances.Count;
                var instance = _instances.Values.ElementAt(_carouselIndex);
                SetLightOn(instance.state, instance.message);
            });
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settings == null) return;
            WindowOpacity = e.NewValue;
            _settings.Opacity = e.NewValue;
            SettingsService.Save(_settings);
        }

        private void BubbleLengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settings == null) return;
            _settings.BubbleTextLength = (int)e.NewValue;
            SettingsService.Save(_settings);
        }

        private void ShowBubbleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings == null || ShowBubbleCheckBox == null) return;
            _settings.ShowBubbleAlways = ShowBubbleCheckBox.IsChecked ?? false;
            SettingsService.Save(_settings);
        }

        private async void SavePort_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PortTextBox.Text, out int newPort) && newPort > 0 && newPort < 65536)
            {
                if (newPort != _settings.WebSocketPort)
                {
                    _settings.WebSocketPort = newPort;
                    SettingsService.Save(_settings);

                    // 重启服务器
                    await _wsServer.StopAsync();

                    _instances.Clear();
                    _currentInstanceId = null;
                    SetAllLightsOff();

                    var newServer = new WebSocketServer(newPort);
                    newServer.StateChanged += OnStateChanged;
                    newServer.ServerStarted += OnServerStarted;
                    newServer.ServerError += OnServerError;
                    newServer.ClientConnected += OnClientConnected;
                    newServer.ClientDisconnected += OnClientDisconnected;

                    await newServer.StartAsync();
                }

                // 关闭右键菜单
                if (ContextMenu != null)
                {
                    ContextMenu.IsOpen = false;
                }
            }
            else
            {
                MessageBox.Show("请输入有效的端口号 (1-65535)", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PortTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 允许回车触发保存
            if (e.Key == Key.Enter)
            {
                SavePort_Click(sender, e);
                e.Handled = true;
            }
        }

        private void SavePollingInterval_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PollingIntervalTextBox.Text, out int newInterval))
            {
                if (newInterval < 100 || newInterval > 5000)
                {
                    MessageBox.Show("请输入有效的轮询间隔 (100-5000毫秒)", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (newInterval != _settings.FilePollingIntervalMs)
                {
                    _settings.FilePollingIntervalMs = newInterval;
                    SettingsService.Save(_settings);

                    // 重启定时器
                    _filePollingTimer.Stop();
                    _filePollingTimer.Interval = newInterval;
                    _filePollingTimer.Start();
                }

                // 关闭右键菜单
                if (ContextMenu != null)
                {
                    ContextMenu.IsOpen = false;
                }

                MessageBox.Show($"轮询间隔已设置为 {newInterval} 毫秒", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("请输入有效的数字！", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PollingIntervalTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 允许回车触发保存
            if (e.Key == Key.Enter)
            {
                SavePollingInterval_Click(sender, e);
                e.Handled = true;
            }
        }

        // ==================== LED 显示设置 ====================
        private void StopScrollCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings == null || StopScrollCheckBox == null) return;
            _settings.StopScrollAfterLoop = StopScrollCheckBox.IsChecked ?? true;
            SettingsService.Save(_settings);
        }

        private void ScrollSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settings == null) return;
            _settings.ScrollSpeed = e.NewValue;
            SettingsService.Save(_settings);
        }

        private void SnapThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settings == null) return;
            _settings.SnapThreshold = (int)e.NewValue;
            SettingsService.Save(_settings);
        }

        private void ApplySizeSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(WindowWidthTextBox.Text, out int windowWidth))
            {
                MessageBox.Show("请输入有效的窗口宽度！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!int.TryParse(LightWidthTextBox.Text, out int lightWidth))
            {
                MessageBox.Show("请输入有效的信号灯宽度！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!int.TryParse(LedWidthTextBox.Text, out int ledWidth))
            {
                MessageBox.Show("请输入有效的LED区域宽度！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 保存设置
            _settings.TotalWindowWidth = windowWidth;
            _settings.LightWidth = lightWidth;
            _settings.LedWidth = ledWidth;
            SettingsService.Save(_settings);

            // 立即应用
            Width = windowWidth;
            LightContainer.Width = lightWidth;
            LedTextContainer.Width = ledWidth;
            LedCanvas.Width = ledWidth;

            // 关闭右键菜单
            if (ContextMenu != null)
            {
                ContextMenu.IsOpen = false;
            }

            MessageBox.Show($"尺寸设置已应用！\n窗口宽度: {windowWidth}px\n信号灯宽度: {lightWidth}px\nLED区域宽度: {ledWidth}px",
                "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void WindowWidthTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplySizeSettings_Click(sender, e);
        }

        private void LightWidthTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplySizeSettings_Click(sender, e);
        }

        private void LedWidthTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplySizeSettings_Click(sender, e);
        }

        private void SaveTextSettings_Click(object sender, RoutedEventArgs e)
        {
            // 保存文案设置
            _settings.YellowLabelText = YellowLabelTextBox.Text.Trim();
            _settings.YellowMessageText = YellowMessageTextBox.Text.Trim();
            _settings.RedLabelText = RedLabelTextBox.Text.Trim();
            _settings.RedMessageText = RedMessageTextBox.Text.Trim();
            _settings.GreenLabelText = GreenLabelTextBox.Text.Trim();
            _settings.GreenMessageText = GreenMessageTextBox.Text.Trim();

            SettingsService.Save(_settings);

            // 关闭右键菜单
            if (ContextMenu != null)
            {
                ContextMenu.IsOpen = false;
            }

            MessageBox.Show($"文案设置已保存！\n\n" +
                $"🟡 黄灯: {_settings.YellowLabelText}\n" +
                $"🔴 红灯: {_settings.RedLabelText}\n" +
                $"🟢 绿灯: {_settings.GreenLabelText}",
                "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetTextSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要恢复默认文案吗？", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 恢复默认值
                _settings.YellowLabelText = "思考中";
                _settings.YellowMessageText = "AI正在思考您的问题...";
                _settings.RedLabelText = "需要确认";
                _settings.RedMessageText = "请确认下一步操作...";
                _settings.GreenLabelText = "输出中";
                _settings.GreenMessageText = "正在生成回答...";

                SettingsService.Save(_settings);

                // 更新UI
                YellowLabelTextBox.Text = _settings.YellowLabelText;
                YellowMessageTextBox.Text = _settings.YellowMessageText;
                RedLabelTextBox.Text = _settings.RedLabelText;
                RedMessageTextBox.Text = _settings.RedMessageText;
                GreenLabelTextBox.Text = _settings.GreenLabelText;
                GreenMessageTextBox.Text = _settings.GreenMessageText;

                MessageBox.Show("已恢复默认文案！", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EnableGifCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings == null || EnableGifCheckBox == null) return;
            _settings.EnableGifAnimation = EnableGifCheckBox.IsChecked ?? true;
            SettingsService.Save(_settings);
        }

        private void BrowseYellowGif_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "GIF文件 (*.gif)|*.gif|所有文件 (*.*)|*.*",
                Title = "选择黄灯GIF动画"
            };

            if (dialog.ShowDialog() == true)
            {
                YellowGifPathTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseRedGif_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "GIF文件 (*.gif)|*.gif|所有文件 (*.*)|*.*",
                Title = "选择红灯GIF动画"
            };

            if (dialog.ShowDialog() == true)
            {
                RedGifPathTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseGreenGif_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "GIF文件 (*.gif)|*.gif|所有文件 (*.*)|*.*",
                Title = "选择绿灯GIF动画"
            };

            if (dialog.ShowDialog() == true)
            {
                GreenGifPathTextBox.Text = dialog.FileName;
            }
        }

        private void SaveGifSettings_Click(object sender, RoutedEventArgs e)
        {
            _settings.YellowGifPath = YellowGifPathTextBox.Text.Trim();
            _settings.RedGifPath = RedGifPathTextBox.Text.Trim();
            _settings.GreenGifPath = GreenGifPathTextBox.Text.Trim();
            _settings.EnableGifAnimation = EnableGifCheckBox.IsChecked ?? true;

            SettingsService.Save(_settings);

            // 关闭右键菜单
            if (ContextMenu != null)
            {
                ContextMenu.IsOpen = false;
            }

            MessageBox.Show($"GIF动画设置已保存！\n\n" +
                $"启用状态: {(_settings.EnableGifAnimation ? "✅ 已启用" : "❌ 已禁用")}\n" +
                $"🟡 黄灯: {(string.IsNullOrEmpty(_settings.YellowGifPath) ? "未设置" : "已设置")}\n" +
                $"🔴 红灯: {(string.IsNullOrEmpty(_settings.RedGifPath) ? "未设置" : "已设置")}\n" +
                $"🟢 绿灯: {(string.IsNullOrEmpty(_settings.GreenGifPath) ? "未设置" : "已设置")}",
                "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AutoStartMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                _settings.AutoStart = menuItem.IsChecked;
                SettingsService.Save(_settings);

                if (menuItem.IsChecked)
                {
                    SetAutoStart(true);
                    MessageBox.Show("已启用开机自启！", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    SetAutoStart(false);
                    MessageBox.Show("已禁用开机自启！", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                var shortcutPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs",
                    "Startup",
                    "ClaudeTrafficLight.lnk");

                if (enable)
                {
                    // 创建快捷方式
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (string.IsNullOrEmpty(exePath)) return;

                    // 使用ShellLink创建快捷方式
                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        var shell = Activator.CreateInstance(shellType);
                        var shortcut = shellType.InvokeMember("CreateShortcut",
                            System.Reflection.BindingFlags.InvokeMethod, null, shell,
                            new object[] { shortcutPath });

                        if (shortcut != null)
                        {
                            var shortcutType = shortcut.GetType();
                            shortcutType.InvokeMember("TargetPath",
                                System.Reflection.BindingFlags.SetProperty, null,
                                shortcut, new object[] { exePath });
                            shortcutType.InvokeMember("WorkingDirectory",
                                System.Reflection.BindingFlags.SetProperty, null,
                                shortcut, new object[] { System.IO.Path.GetDirectoryName(exePath) });
                            shortcutType.InvokeMember("Description",
                                System.Reflection.BindingFlags.SetProperty, null,
                                shortcut, new object[] { "Claude Code 交通信号灯" });
                            shortcutType.InvokeMember("Save",
                                System.Reflection.BindingFlags.InvokeMethod, null,
                                shortcut, null);
                        }
                    }
                }
                else
                {
                    // 删除快捷方式
                    if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置开机自启失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            var settingsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClaudeTrafficLight"
            );

            try
            {
                if (!Directory.Exists(settingsPath))
                {
                    Directory.CreateDirectory(settingsPath);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = settingsPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件夹: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ToggleVisibility()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
                Activate();
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            IsExitRequested = true;
            Close();
        }

        private void InstallClaudeHooks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".claude",
                    "settings.json"
                );

                if (!File.Exists(settingsPath))
                {
                    MessageBox.Show("未找到 Claude Code 配置文件！\n路径: " + settingsPath,
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 读取现有配置
                var json = File.ReadAllText(settingsPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 构建新的配置
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

                writer.WriteStartObject();

                // 复制所有现有属性
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.NameEquals("hooks")) continue; // 跳过旧的hooks配置
                    prop.WriteTo(writer);
                }

                // 写入hooks配置
                writer.WritePropertyName("hooks");
                writer.WriteStartObject();

                // UserPromptSubmit -> yellow: 用户发送消息，Claude开始思考
                writer.WritePropertyName("UserPromptSubmit");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("hooks");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "command");
                writer.WriteString("command", "bash -c \"echo '{\\\"state\\\":\\\"yellow\\\",\\\"content\\\":\\\"AI正在思考...\\\",\\\"timestamp\\\":'$(date +%s%3N)',\\\"label\\\":\\\"思考中\\\"}' > $HOME/.claude/cc_traffic_light_state\"");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndArray();

                // Stop -> green: Claude完成输出
                writer.WritePropertyName("Stop");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("hooks");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "command");
                writer.WriteString("command", "bash -c \"echo '{\\\"state\\\":\\\"green\\\",\\\"content\\\":\\\"AI输出完成\\\",\\\"timestamp\\\":'$(date +%s%3N)',\\\"label\\\":\\\"输出中\\\"}' > $HOME/.claude/cc_traffic_light_state\"");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndArray();

                // Elicitation -> red: Claude请求用户输入/确认
                writer.WritePropertyName("Elicitation");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("hooks");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "command");
                writer.WriteString("command", "bash -c \"echo '{\\\"state\\\":\\\"red\\\",\\\"content\\\":\\\"需要您的输入...\\\",\\\"timestamp\\\":'$(date +%s%3N)',\\\"label\\\":\\\"需要确认\\\"}' > $HOME/.claude/cc_traffic_light_state\"");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndArray();

                // ToolStart -> yellow: 开始调用工具
                writer.WritePropertyName("ToolStart");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("hooks");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "command");
                writer.WriteString("command", "bash -c \"echo '{\\\"state\\\":\\\"yellow\\\",\\\"content\\\":\\\"调用工具中...\\\",\\\"timestamp\\\":'$(date +%s%3N)',\\\"label\\\":\\\"工具调用\\\"}' > $HOME/.claude/cc_traffic_light_state\"");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndArray();

                // ToolEnd -> green: 工具调用结束
                writer.WritePropertyName("ToolEnd");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("hooks");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "command");
                writer.WriteString("command", "bash -c \"echo '{\\\"state\\\":\\\"green\\\",\\\"content\\\":\\\"工具调用完成\\\",\\\"timestamp\\\":'$(date +%s%3N)',\\\"label\\\":\\\"完成\\\"}' > $HOME/.claude/cc_traffic_light_state\"");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndArray();

                // ToolConfirm -> red: 需要用户确认工具调用
                writer.WritePropertyName("ToolConfirm");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("hooks");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "command");
                writer.WriteString("command", "bash -c \"echo '{\\\"state\\\":\\\"red\\\",\\\"content\\\":\\\"需要确认工具调用...\\\",\\\"timestamp\\\":'$(date +%s%3N)',\\\"label\\\":\\\"工具确认\\\"}' > $HOME/.claude/cc_traffic_light_state\"");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndArray();

                // PlanStart -> yellow: 进入计划模式
                writer.WritePropertyName("PlanStart");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("hooks");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "command");
                writer.WriteString("command", "bash -c \"echo '{\\\"state\\\":\\\"yellow\\\",\\\"content\\\":\\\"正在制定计划...\\\",\\\"timestamp\\\":'$(date +%s%3N)',\\\"label\\\":\\\"计划模式\\\"}' > $HOME/.claude/cc_traffic_light_state\"");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndArray();

                // PlanEnd -> green: 退出计划模式
                writer.WritePropertyName("PlanEnd");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("hooks");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "command");
                writer.WriteString("command", "bash -c \"echo '{\\\"state\\\":\\\"green\\\",\\\"content\\\":\\\"计划完成\\\",\\\"timestamp\\\":'$(date +%s%3N)',\\\"label\\\":\\\"完成\\\"}' > $HOME/.claude/cc_traffic_light_state\"");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndArray();

                writer.WriteEndObject(); // end of hooks
                writer.WriteEndObject(); // end of root

                writer.Flush();

                // 写回文件
                File.WriteAllText(settingsPath, Encoding.UTF8.GetString(stream.ToArray()));

                MessageBox.Show(
                    "✅ Claude Code Hooks 配置已成功安装！\n\n" +
                    "📝 配置的Hook事件:\n" +
                    "  • 用户提问 → 黄灯（AI思考中）\n" +
                    "  • 请求输入 → 红灯（需要用户确认）\n" +
                    "  • 完成回答 → 绿灯（输出完成）\n" +
                    "  • 工具调用 → 黄灯（工具调用中）\n" +
                    "  • 工具确认 → 红灯（需要确认工具调用）\n" +
                    "  • 计划模式 → 黄灯（制定计划中）\n\n" +
                    "⚠️ 请重启 VSCode 或 Claude Code 会话后生效！",
                    "安装成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"安装失败: {ex.Message}\n\n{ex.StackTrace}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UninstallClaudeHooks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".claude",
                    "settings.json"
                );

                if (!File.Exists(settingsPath))
                {
                    MessageBox.Show("未找到 Claude Code 配置文件！\n路径: " + settingsPath,
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 读取现有配置
                var json = File.ReadAllText(settingsPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 检查是否有hooks配置
                if (!root.TryGetProperty("hooks", out var hooksElement))
                {
                    MessageBox.Show("未检测到 Traffic Light Hooks 配置，无需卸载。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 构建新的配置
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

                writer.WriteStartObject();

                // 复制所有现有属性
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.NameEquals("hooks"))
                    {
                        // 只移除 Traffic Light 相关的三个hooks，保留其他hooks
                        writer.WritePropertyName("hooks");
                        writer.WriteStartObject();

                        foreach (var hookProp in hooksElement.EnumerateObject())
                        {
                            // 跳过所有 Traffic Light 添加的hook事件
                            if (hookProp.NameEquals("UserPromptSubmit") ||
                                hookProp.NameEquals("Stop") ||
                                hookProp.NameEquals("Elicitation") ||
                                hookProp.NameEquals("ToolStart") ||
                                hookProp.NameEquals("ToolEnd") ||
                                hookProp.NameEquals("ToolConfirm") ||
                                hookProp.NameEquals("PlanStart") ||
                                hookProp.NameEquals("PlanEnd"))
                            {
                                continue;
                            }
                            // 保留其他hooks
                            hookProp.WriteTo(writer);
                        }

                        writer.WriteEndObject(); // end of hooks
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }

                writer.WriteEndObject(); // end of root
                writer.Flush();

                // 写回文件
                File.WriteAllText(settingsPath, Encoding.UTF8.GetString(stream.ToArray()));

                MessageBox.Show(
                    "✅ Claude Code Hooks 配置已成功卸载！\n\n" +
                    "📝 已移除的Hook事件:\n" +
                    "  • UserPromptSubmit (用户提问 → 红灯)\n" +
                    "  • Stop (完成回答 → 绿灯)\n" +
                    "  • Elicitation (等待输入 → 黄灯)\n\n" +
                    "⚠️ 请重启 VSCode 或 Claude Code 会话后生效！",
                    "卸载成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"卸载失败: {ex.Message}\n\n{ex.StackTrace}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyBackgroundColor(string colorHex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                LightContainer.Background = new SolidColorBrush(color);
                BgColorTextBox.Text = colorHex;

                // 同步更新 LED 区域背景色（半透明效果）
                var ledBgColor = color;
                ledBgColor.A = 40; // 15% 透明度
                LedTextContainer.Background = new SolidColorBrush(ledBgColor);

                // 保存到设置
                _settings.BackgroundColor = colorHex;
                SettingsService.Save(_settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"颜色格式错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BgColorPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorHex)
            {
                ApplyBackgroundColor(colorHex);
            }
        }

        private void ApplyBgColor_Click(object sender, RoutedEventArgs e)
        {
            ApplyBackgroundColor(BgColorTextBox.Text);
        }

        private void BgColorTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyBgColor_Click(sender, e);
                e.Handled = true;
            }
        }

        private void ShowConnectionStatus_Click(object sender, RoutedEventArgs e)
        {
            var clientCount = _wsServer.ClientCount;
            var port = _wsServer.Port;
            var isRunning = _wsServer.IsRunning;

            var message = new StringBuilder();
            message.AppendLine("🔌 WebSocket 连接状态");
            message.AppendLine("═".PadRight(35, '═'));
            message.AppendLine();
            message.AppendLine($"服务状态: {(isRunning ? "✅ 运行中" : "❌ 已停止")}");
            message.AppendLine($"监听端口: {port}");
            message.AppendLine($"客户端数: {clientCount}");
            message.AppendLine();

            if (clientCount > 0)
            {
                message.AppendLine("📱 已连接的客户端:");
                message.AppendLine("─".PadRight(35, '─'));

                foreach (var client in _wsServer.Clients)
                {
                    var stateEmoji = client.CurrentState switch
                    {
                        "thinking" => "🟡",
                        "writing" => "🟢",
                        "needs_confirm" => "🔴",
                        _ => "⚪"
                    };

                    var connectedDuration = DateTime.Now - client.ConnectedAt;
                    var durationStr = connectedDuration.TotalMinutes < 1
                        ? $"{connectedDuration.Seconds}秒"
                        : $"{(int)connectedDuration.TotalMinutes}分钟";

                    var lastActivity = client.LastActivity.HasValue
                        ? $"{(int)(DateTime.Now - client.LastActivity.Value).TotalSeconds}秒前"
                        : "无";

                    message.AppendLine();
                    message.AppendLine($"  {stateEmoji} 客户端 #{client.ClientId}");
                    message.AppendLine($"     ├─ 连接时长: {durationStr}");
                    message.AppendLine($"     ├─ 端点: {client.EndPoint}");
                    message.AppendLine($"     ├─ 当前状态: {client.CurrentState}");
                    message.AppendLine($"     └─ 最后活动: {lastActivity}");
                }
            }
            else
            {
                message.AppendLine("💤 暂无客户端连接");
                message.AppendLine();
                message.AppendLine("💡 提示:");
                message.AppendLine("  • 确保VSCode扩展已安装并激活");
                message.AppendLine("  • 如果未连接，尝试重启VSCode窗口");
                message.AppendLine("  • 或使用\"重启WebSocket服务\"菜单项");
            }

            MessageBox.Show(message.ToString(), "WebSocket 连接状态",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void RestartWebSocket_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _wsServer.StopAsync();
                var newServer = new WebSocketServer(_settings.WebSocketPort);
                newServer.StateChanged += OnStateChanged;
                newServer.ServerStarted += OnServerStarted;
                newServer.ServerError += OnServerError;
                newServer.ClientConnected += OnClientConnected;
                newServer.ClientDisconnected += OnClientDisconnected;
                await newServer.StartAsync();

                // 替换私有字段
                var wsServerField = GetType().GetField("_wsServer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                wsServerField?.SetValue(this, newServer);

                MessageBox.Show("WebSocket 服务已重启！", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重启失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableLoggingCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings == null || EnableLoggingCheckBox == null) return;
            _settings.EnableMessageLogging = EnableLoggingCheckBox.IsChecked ?? false;
            SettingsService.Save(_settings);
        }

        private void ApplyMaxLogEntries_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(MaxLogEntriesTextBox.Text, out int max))
            {
                _settings.MaxLogEntries = max;
                SettingsService.Save(_settings);

                // 裁剪现有日志
                if (_messageLogs.Count > max)
                {
                    _messageLogs.RemoveRange(max, _messageLogs.Count - max);
                }
            }
        }

        private void ViewLogs_Click(object sender, RoutedEventArgs e)
        {
            if (_messageLogs.Count == 0)
            {
                MessageBox.Show("暂无日志记录。\n请先在\"日志设置\"中启用消息日志记录。",
                    "WebSocket 消息日志", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("📝 WebSocket 消息日志");
            sb.AppendLine($"共 {_messageLogs.Count} 条记录");
            sb.AppendLine("═".PadRight(60, '═'));
            sb.AppendLine();

            foreach (var log in _messageLogs.Take(50)) // 最多显示50条
            {
                var stateEmoji = log.State switch
                {
                    "thinking" => "🟡",
                    "writing" => "🟢",
                    "needs_confirm" => "🔴",
                    _ => "⚪"
                };
                sb.AppendLine($"{stateEmoji} [{log.Timestamp:HH:mm:ss.fff}]");
                sb.AppendLine($"   客户端: {log.ClientId}");
                sb.AppendLine($"   状态: {log.State}");
                sb.AppendLine($"   消息: {log.Message}");
                sb.AppendLine();
            }

            if (_messageLogs.Count > 50)
            {
                sb.AppendLine($"  ... 还有 {_messageLogs.Count - 50} 条记录 (使用\"导出日志文件\"查看全部)");
            }

            var window = new Window
            {
                Title = "WebSocket 消息日志",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48))
            };

            var textBox = new TextBox
            {
                Text = sb.ToString(),
                IsReadOnly = true,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap
            };

            window.Content = textBox;
            window.ShowDialog();
        }

        private void ExportLogs_Click(object sender, RoutedEventArgs e)
        {
            if (_messageLogs.Count == 0)
            {
                MessageBox.Show("暂无日志记录可导出。", "导出日志",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("时间,客户端ID,状态,消息,原始JSON");
                foreach (var log in _messageLogs)
                {
                    sb.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                                 $"{log.ClientId},{log.State},\"{log.Message.Replace("\"", "\"\"")}\",\"{log.RawJson.Replace("\"", "\"\"")}\"");
                }

                var filePath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"TrafficLight_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                MessageBox.Show($"日志已导出到:\n{filePath}", "导出成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show($"确定要清空所有 {_messageLogs.Count} 条日志记录吗？",
                "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _messageLogs.Clear();
                MessageBox.Show("日志已清空。", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
