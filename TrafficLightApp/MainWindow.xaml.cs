using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeTrafficLight.Services;
namespace ClaudeTrafficLight;
public partial class MainWindow : Window
{
    private readonly WebSocketServer _wsServer;
    private readonly Dictionary<string, Brush> _stateColors = new()
    {
        ["idle"] = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
        ["thinking"] = new SolidColorBrush(Color.FromRgb(255, 200, 0)),
        ["writing"] = new SolidColorBrush(Color.FromRgb(0, 200, 83)),
        ["needs_confirm"] = new SolidColorBrush(Color.FromRgb(255, 68, 68))
    };
    public MainWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (s, e) => DragMove();
        Loaded += async (s, e) =>
        {
            _wsServer = new WebSocketServer(19876);
            _wsServer.StateChanged += OnStateChanged;
            await _wsServer.StartAsync();
        };
        Closing += async (s, e) =>
        {
            await _wsServer.StopAsync();
        };
    }
    private void OnStateChanged(object? sender, StateChangePayload e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_stateColors.TryGetValue(e.state, out var color))
            {
                StatusLight.Fill = color;
            }
        });
    }
}
