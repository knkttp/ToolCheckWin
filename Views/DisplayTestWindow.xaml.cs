using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ToolCheckWin.Views;

public partial class DisplayTestWindow : Window
{
    private int _colorIndex = 0;
    private readonly Brush[] _testBrushes;
    private readonly string[] _colorNames =
    {
        "1/7: ĐỎ (Red)",
        "2/7: XANH LÁ (Lime)",
        "3/7: XANH DƯƠNG (Blue)",
        "4/7: TRẮNG (White)",
        "5/7: ĐEN (Black)",
        "6/7: XÁM 50% (Gray)",
        "7/7: DẢI CHUYỂN SẮC (LinearGradient)"
    };

    /// <summary>
    /// 0 = Hoàn hảo (20đ), 1 = Lỗi nhẹ (12đ), 2 = Lỗi nặng (0đ)
    /// </summary>
    public int SelectedDisplayStatus { get; private set; } = 0;

    public DisplayTestWindow()
    {
        InitializeComponent();

        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };
        gradient.GradientStops.Add(new GradientStop(Colors.Red, 0.0));
        gradient.GradientStops.Add(new GradientStop(Colors.Yellow, 0.2));
        gradient.GradientStops.Add(new GradientStop(Colors.Lime, 0.4));
        gradient.GradientStops.Add(new GradientStop(Colors.Cyan, 0.6));
        gradient.GradientStops.Add(new GradientStop(Colors.Blue, 0.8));
        gradient.GradientStops.Add(new GradientStop(Colors.Magenta, 1.0));

        _testBrushes = new Brush[]
        {
            Brushes.Red,
            Brushes.Lime,
            Brushes.Blue,
            Brushes.White,
            Brushes.Black,
            Brushes.Gray,
            gradient
        };

        ApplyCurrentBrush();
    }

    private void ApplyCurrentBrush()
    {
        RootGrid.Background = _testBrushes[_colorIndex];
        TxtColorStep.Text = $"Màu {_colorNames[_colorIndex]} — Click chuột trái hoặc nhấn [SPACE] để đổi màu | [ESC] Kết thúc";
    }

    private void AdvanceColorOrFinish()
    {
        if (ResultModal.Visibility == Visibility.Visible)
            return;

        _colorIndex++;
        if (_colorIndex >= _testBrushes.Length)
        {
            ShowResultPrompt();
        }
        else
        {
            ApplyCurrentBrush();
        }
    }

    private void ShowResultPrompt()
    {
        HintBanner.Visibility = Visibility.Collapsed;
        ResultModal.Visibility = Visibility.Visible;
        Cursor = Cursors.Arrow;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        AdvanceColorOrFinish();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            AdvanceColorOrFinish();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ShowResultPrompt();
            e.Handled = true;
        }
    }

    private void BtnStatus0_Click(object sender, RoutedEventArgs e)
    {
        SelectedDisplayStatus = 0;
        DialogResult = true;
        Close();
    }

    private void BtnStatus1_Click(object sender, RoutedEventArgs e)
    {
        SelectedDisplayStatus = 1;
        DialogResult = true;
        Close();
    }

    private void BtnStatus2_Click(object sender, RoutedEventArgs e)
    {
        SelectedDisplayStatus = 2;
        DialogResult = true;
        Close();
    }
}
