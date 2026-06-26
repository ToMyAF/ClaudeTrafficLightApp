### Task 6: WPF UI增强 - 气泡消息和闪烁动画

**Files:**
- Modify: `TrafficLightApp/MainWindow.xaml`
- Modify: `TrafficLightApp/MainWindow.xaml.cs`
- Create: `TrafficLightApp/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: WebSocket state events
- Produces: 气泡消息显示、闪烁动画、多实例管理

- [ ] **Step 1: 更新MainWindow.xaml添加气泡UI**

```xml
<Window x:Class="ClaudeTrafficLight.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Claude Traffic Light"
        Width="200"
        Height="80"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        x:Name="windowRoot">
    <Window.Resources>
        <Storyboard x:Key="FlashAnimation">
            <DoubleAnimation x:Name="FlashAnim"
                             Storyboard.TargetName="StatusLight"
                             Storyboard.TargetProperty="Opacity"
                             From="1" To="0.3" Duration="0:0:0.2"
                             AutoReverse="True"
                             RepeatBehavior="3x"/>
        </Storyboard>
    </Window.Resources>
    <Border Background="#2D2D30"
            CornerRadius="10"
            Opacity="{Binding WindowOpacity, ElementName=windowRoot}"
            BorderBrush="#404040"
            BorderThickness="1">
        <Grid Margin="12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <!-- 指示灯 -->
            <Ellipse x:Name="StatusLight"
                     Width="28"
                     Height="28"
                     Fill="#888888"
                     Stroke="#FFFFFF"
                     StrokeThickness="1"
                     VerticalAlignment="Center">
                <Ellipse.Effect>
                    <DropShadowEffect BlurRadius="8" ShadowDepth="0"/>
                </Ellipse.Effect>
            </Ellipse>
            <!-- 消息气泡 -->
            <StackPanel Grid.Column="1" Margin="10,0,0,0" VerticalAlignment="Center">
                <TextBlock x:Name="StatusText"
                           Text="等待连接..."
                           Foreground="#FFFFFF"
                           FontSize="12"
                           TextWrapping="Wrap"/>
            </StackPanel>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 2: 添加依赖属性和闪烁逻辑**

更新 `MainWindow.xaml.cs`：

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClaudeTrafficLight.Services;
namespace ClaudeTrafficLight;
public partial class MainWindow : Window
{
    public double WindowOpacity
    {
        get { return (double)GetValue(WindowOpacityProperty); }
        set { SetValue(WindowOpacityProperty, value); }
    }
    public static readonly DependencyProperty WindowOpacityProperty =
        DependencyProperty.Register("WindowOpacity", typeof(double), 
        typeof(MainWindow), new PropertyMetadata(0.8));
    private readonly WebSocketServer _wsServer;
    private readonly Dictionary<string, StateChangePayload> _instances = new();
    private string? _currentInstanceId;
    private int _carouselIndex;
    private readonly System.Timers.Timer _carouselTimer;
    private readonly Dictionary<string, Brush> _stateColors = new()
    {
        ["idle"] = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
        ["thinking"] = new SolidColorBrush(Color.FromRgb(255, 200, 0)),
        ["writing"] = new SolidColorBrush(Color.FromRgb(0, 200, 83)),
        ["needs_confirm"] = new SolidColorBrush(Color.FromRgb(255, 68, 68))
    };
    private readonly Dictionary<string, string> _stateTexts = new()
    {
        ["idle"] = "空闲",
        ["thinking"] = "思考中...",
        ["writing"] = "输出中...",
        ["needs_confirm"] = "需要确认！"
    };
    public MainWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (s, e) => DragMove();
        MouseEnter += (s, e) => WindowOpacity = 1.0;
        MouseLeave += (s, e) => WindowOpacity = 0.8;
        _wsServer = new WebSocketServer(19876);
        _wsServer.StateChanged += OnStateChanged;
        Loaded += async (s, e) => await _wsServer.StartAsync();
        Closing += async (s, e) => await _wsServer.StopAsync();
        _carouselTimer = new System.Timers.Timer(3000);
        _carouselTimer.Elapsed += (s, e) => CarouselNext();
        _carouselTimer.Start();
    }
    private void OnStateChanged(object? sender, StateChangePayload e)
    {
        Dispatcher.Invoke(() =>
        {
            _instances[e.vscodeWindowId] = e;
            // 如果有红灯，优先显示
            if (e.state == "needs_confirm")
            {
                _currentInstanceId = e.vscodeWindowId;
                UpdateUI(e);
                PlayFlashAnimation();
            }
            else if (_currentInstanceId == null || !HasAnyRedState())
            {
                _currentInstanceId = e.vscodeWindowId;
                UpdateUI(e);
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
            UpdateUI(instance);
        });
    }
    private void UpdateUI(StateChangePayload state)
    {
        if (_stateColors.TryGetValue(state.state, out var color))
        {
            StatusLight.Fill = color;
        }
        var text = _stateTexts.TryGetValue(state.state, out var t) ? t : state.state;
        StatusText.Text = text;
    }
    private void PlayFlashAnimation()
    {
        var storyboard = FindResource("FlashAnimation") as Storyboard;
        storyboard?.Begin();
    }
}
```

- [ ] **Step 3: 编译验证**

Run: `cd TrafficLightApp; dotnet build`
Expected: 编译成功

- [ ] **Step 4: 提交**

Run:
```
git add TrafficLightApp/MainWindow.xaml TrafficLightApp/MainWindow.xaml.cs
git commit -m "feat: add bubble message, flash animation and multi-instance carousel"
```

---

### Task 7: 右键菜单和配置

**Files:**
- Create: `TrafficLightApp/Services/SettingsService.cs`
- Modify: `TrafficLightApp/MainWindow.xaml`
- Modify: `TrafficLightApp/MainWindow.xaml.cs`

**Interfaces:**
- Produces: 设置持久化、右键菜单、透明度可配置

- [ ] **Step 1: 创建SettingsService.cs**

```csharp
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
```

- [ ] **Step 2: 添加右键菜单到MainWindow.xaml**

在Window根元素内添加：

```xml
<Window.ContextMenu>
    <ContextMenu>
        <MenuItem Header="透明度">
            <Slider x:Name="OpacitySlider" 
                    Minimum="0.3" Maximum="1.0" 
                    Value="0.8" 
                    Width="100"
                    ValueChanged="OpacitySlider_ValueChanged"/>
        </MenuItem>
        <Separator/>
        <MenuItem Header="退出" Click="ExitMenuItem_Click"/>
    </ContextMenu>
</Window.ContextMenu>
```

- [ ] **Step 3: 添加菜单事件处理到MainWindow.xaml.cs**

```csharp
private AppSettings _settings;
// 在构造函数中加载设置
_settings = SettingsService.Load();
WindowOpacity = _settings.Opacity;
OpacitySlider.Value = _settings.Opacity;
_carouselTimer.Interval = _settings.CarouselIntervalMs;
```

```csharp
private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    WindowOpacity = e.NewValue;
    _settings.Opacity = e.NewValue;
    SettingsService.Save(_settings);
}
private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
{
    Close();
}
```

- [ ] **Step 4: 编译验证**

Run: `cd TrafficLightApp; dotnet build`
Expected: 编译成功

- [ ] **Step 5: 提交**

Run:
```
git add TrafficLightApp/Services/SettingsService.cs TrafficLightApp/MainWindow.xaml TrafficLightApp/MainWindow.xaml.cs
git commit -m "feat: add settings service and right-click context menu"
```
