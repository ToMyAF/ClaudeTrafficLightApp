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
    private readonly AppSettings _settings;
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
        _settings = SettingsService.Load();
        WindowOpacity = _settings.Opacity;
        OpacitySlider.Value = _settings.Opacity;
        MouseLeftButtonDown += (s, e) => DragMove();
        MouseEnter += (s, e) => WindowOpacity = 1.0;
        MouseLeave += (s, e) => WindowOpacity = _settings.Opacity;
        _wsServer = new WebSocketServer(_settings.WebSocketPort);
        _wsServer.StateChanged += OnStateChanged;
        Loaded += async (s, e) => await _wsServer.StartAsync();
        Closing += async (s, e) => await _wsServer.StopAsync();
        _carouselTimer = new System.Timers.Timer(_settings.CarouselIntervalMs);
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
}
