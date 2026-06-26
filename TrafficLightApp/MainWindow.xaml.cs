using System.Windows;
using System.Windows.Input;
namespace ClaudeTrafficLight;
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (s, e) => DragMove();
    }
}
