using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using ToolCheckWin.Core;

namespace ToolCheckWin.Views;

public class KeyCapItem : INotifyPropertyChanged
{
    private Brush _backgroundBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)); // #1E293B
    private Brush _foregroundBrush = Brushes.White;
    private bool _isTested;

    public uint VirtualKeyCode { get; set; }
    public string Label { get; set; } = "";

    public bool IsTested
    {
        get => _isTested;
        set { _isTested = value; OnPropertyChanged(); }
    }

    public Brush BackgroundBrush
    {
        get => _backgroundBrush;
        set { _backgroundBrush = value; OnPropertyChanged(); }
    }

    public Brush ForegroundBrush
    {
        get => _foregroundBrush;
        set { _foregroundBrush = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class KeyboardTestControl : UserControl
{
    private static readonly SolidColorBrush DefaultKeyBrush = new(Color.FromRgb(30, 41, 59));
    private static readonly SolidColorBrush PressedYellowBrush = new(Color.FromRgb(250, 204, 21)); // Yellow
    private static readonly SolidColorBrush TestedGreenBrush = new((Color)ColorConverter.ConvertFromString("#22C55E"));

    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;
    private WaveInEvent? _waveIn;
    private bool _isMicListening;

    public ObservableCollection<KeyCapItem> Keys { get; } = new();

    public bool KeyboardPassed { get; private set; } = true;
    public bool TouchpadPassed => ChkTouchpadPassed.IsChecked == true;
    public bool AudioCamPassed => ChkAudioCamPassed.IsChecked == true;

    public event Action? PeripheralStateChanged;

    public KeyboardTestControl()
    {
        InitializeComponent();
        _hookProc = HookCallback;
        Initialize84Keys();
        KeyMatrixItemsControl.ItemsSource = Keys;
        SetKeyboardPassed(true);

        Loaded += KeyboardTestControl_Loaded;
        Unloaded += KeyboardTestControl_Unloaded;
    }

    private void KeyboardTestControl_Loaded(object sender, RoutedEventArgs e)
    {
        InstallKeyboardHook();
    }

    private void KeyboardTestControl_Unloaded(object sender, RoutedEventArgs e)
    {
        UninstallKeyboardHook();
        StopMicRecording();
    }

    private void Initialize84Keys()
    {
        Keys.Clear();
        (uint vk, string label)[] layout84 =
        {
            // Row 1 (14 keys)
            (0x1B, "ESC"), (0x70, "F1"), (0x71, "F2"), (0x72, "F3"), (0x73, "F4"), (0x74, "F5"), (0x75, "F6"),
            (0x76, "F7"), (0x77, "F8"), (0x78, "F9"), (0x79, "F10"), (0x7A, "F11"), (0x7B, "F12"), (0x2E, "DEL"),
            // Row 2 (14 keys)
            (0xC0, "` ~"), (0x31, "1"), (0x32, "2"), (0x33, "3"), (0x34, "4"), (0x35, "5"), (0x36, "6"),
            (0x37, "7"), (0x38, "8"), (0x39, "9"), (0x30, "0"), (0xBD, "- _"), (0xBB, "= +"), (0x08, "BKSP"),
            // Row 3 (14 keys)
            (0x09, "TAB"), (0x51, "Q"), (0x57, "W"), (0x45, "E"), (0x52, "R"), (0x54, "T"), (0x59, "Y"),
            (0x55, "U"), (0x49, "I"), (0x4F, "O"), (0x50, "P"), (0xDB, "[ {"), (0xDD, "] }"), (0xDC, "\\ |"),
            // Row 4 (14 keys)
            (0x14, "CAPS"), (0x41, "A"), (0x53, "S"), (0x44, "D"), (0x46, "F"), (0x47, "G"), (0x48, "H"),
            (0x4A, "J"), (0x4B, "K"), (0x4C, "L"), (0xBA, "; :"), (0xDE, "' \""), (0x0D, "ENTER"), (0x24, "HOME"),
            // Row 5 (14 keys)
            (0xA0, "L-SHFT"), (0x5A, "Z"), (0x58, "X"), (0x43, "C"), (0x56, "V"), (0x42, "B"), (0x4E, "N"),
            (0x4D, "M"), (0xBC, ", <"), (0xBE, ". >"), (0xBF, "/ ?"), (0xA1, "R-SHFT"), (0x26, "UP"), (0x21, "PGUP"),
            // Row 6 (14 keys)
            (0xA2, "L-CTRL"), (0x5B, "WIN"), (0xA4, "L-ALT"), (0x20, "SPACE"), (0xA5, "R-ALT"), (0x5D, "APPS"), (0xA3, "R-CTRL"),
            (0x2D, "INS"), (0x23, "END"), (0x22, "PGDN"), (0x2C, "PRTSC"), (0x25, "LEFT"), (0x28, "DOWN"), (0x27, "RIGHT")
        };

        foreach (var (vk, label) in layout84)
        {
            Keys.Add(new KeyCapItem
            {
                VirtualKeyCode = vk,
                Label = label,
                BackgroundBrush = DefaultKeyBrush,
                ForegroundBrush = Brushes.White
            });
        }
    }

    public void InstallKeyboardHook()
    {
        if (_hookId != IntPtr.Zero) return;
        using Process curProcess = Process.GetCurrentProcess();
        using ProcessModule? curModule = curProcess.MainModule;
        IntPtr hMod = NativeMethods.GetModuleHandle(curModule?.ModuleName);
        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc, hMod, 0);
    }

    public void UninstallKeyboardHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            uint vk = hookStruct.vkCode;

            bool isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
            bool isUp = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;

            if (isDown || isUp)
            {
                Dispatcher.BeginInvoke(() => UpdateKeyVisual(vk, isDown));
            }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void UpdateKeyVisual(uint vk, bool isDown)
    {
        // Match exact VirtualKeyCode or generic Shift/Ctrl/Alt fallback
        var matchingKeys = Keys.Where(k =>
            k.VirtualKeyCode == vk ||
            (vk == 0x10 && (k.VirtualKeyCode == 0xA0 || k.VirtualKeyCode == 0xA1)) ||
            (vk == 0x11 && (k.VirtualKeyCode == 0xA2 || k.VirtualKeyCode == 0xA3)) ||
            (vk == 0x12 && (k.VirtualKeyCode == 0xA4 || k.VirtualKeyCode == 0xA5))).ToList();

        if (matchingKeys.Count == 0) return;

        foreach (var item in matchingKeys)
        {
            if (isDown)
            {
                item.BackgroundBrush = PressedYellowBrush;
                item.ForegroundBrush = Brushes.Black;
            }
            else
            {
                item.BackgroundBrush = TestedGreenBrush;
                item.ForegroundBrush = Brushes.Black;
                item.IsTested = true;
            }
        }

        int testedCount = Keys.Count(k => k.IsTested);
        TxtKeyProgress.Text = $"  (Đã test: {testedCount}/84 phím)";
        if (testedCount >= 84 && !KeyboardPassed)
        {
            SetKeyboardPassed(true);
        }
    }

    private void SetKeyboardPassed(bool passed)
    {
        KeyboardPassed = passed;
        BtnPassKeyboard.Content = passed ? "✔ ĐÃ ĐẠT (Keyboard Passed +10đ)" : "✔ Pass Keyboard";
        BtnPassKeyboard.Background = passed
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
        PeripheralStateChanged?.Invoke();
    }

    private void BtnResetKeys_Click(object sender, RoutedEventArgs e)
    {
        foreach (var k in Keys)
        {
            k.IsTested = false;
            k.BackgroundBrush = DefaultKeyBrush;
            k.ForegroundBrush = Brushes.White;
        }
        TxtKeyProgress.Text = "  (Đã test: 0/84 phím)";
        SetKeyboardPassed(false);
    }

    private void BtnPassKeyboard_Click(object sender, RoutedEventArgs e)
    {
        SetKeyboardPassed(true);
    }

    private async Task PlaySineWaveChannelAsync(float pan)
    {
        await Task.Run(async () =>
        {
            try
            {
                var signalGen = new SignalGenerator(44100, 1)
                {
                    Gain = 0.35,
                    Frequency = 440.0,
                    Type = SignalGeneratorType.Sin
                }.Take(TimeSpan.FromSeconds(2));

                var panner = new PanningSampleProvider(signalGen)
                {
                    Pan = pan
                };

                using var waveOut = new WaveOutEvent();
                waveOut.Init(panner);
                waveOut.Play();
                while (waveOut.PlaybackState == PlaybackState.Playing)
                {
                    await Task.Delay(50);
                }
            }
            catch { }
        });
    }

    private async void BtnTestLeftSpeaker_Click(object sender, RoutedEventArgs e)
    {
        BtnTestLeftSpeaker.IsEnabled = false;
        await PlaySineWaveChannelAsync(-1.0f);
        BtnTestLeftSpeaker.IsEnabled = true;
    }

    private async void BtnTestRightSpeaker_Click(object sender, RoutedEventArgs e)
    {
        BtnTestRightSpeaker.IsEnabled = false;
        await PlaySineWaveChannelAsync(1.0f);
        BtnTestRightSpeaker.IsEnabled = true;
    }

    private void BtnToggleMic_Click(object sender, RoutedEventArgs e)
    {
        if (_isMicListening)
        {
            StopMicRecording();
        }
        else
        {
            StartMicRecording();
        }
    }

    private void StartMicRecording()
    {
        try
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(44100, 1),
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.StartRecording();
            _isMicListening = true;
            BtnToggleMic.Content = "⏹ Dừng thu Mic";
            BtnToggleMic.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể truy cập Microphone: {ex.Message}", "Lỗi Microphone", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StopMicRecording()
    {
        try
        {
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= WaveIn_DataAvailable;
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }
        }
        catch { }

        _isMicListening = false;
        BtnToggleMic.Content = "🎙 Bắt đầu thu Mic";
        BtnToggleMic.Background = new SolidColorBrush(Color.FromRgb(71, 85, 105));
        MicLevelBar.Value = 0;
        TxtMicPercent.Text = "0%";
    }

    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {
        float maxPeak = 0f;
        for (int index = 0; index < e.BytesRecorded; index += 2)
        {
            short sample = (short)((e.Buffer[index + 1] << 8) | e.Buffer[index]);
            float sample32 = Math.Abs(sample / 32768f);
            if (sample32 > maxPeak) maxPeak = sample32;
        }

        double percent = Math.Clamp(maxPeak * 100.0 * 1.8, 0, 100);
        Dispatcher.BeginInvoke(() =>
        {
            MicLevelBar.Value = percent;
            TxtMicPercent.Text = $"{(int)percent}%";
        });
    }

    private void PeripheralCheck_Changed(object sender, RoutedEventArgs e)
    {
        PeripheralStateChanged?.Invoke();
    }
}
