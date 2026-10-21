using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ToolCheckWin.Core;
using ToolCheckWin.Models;
using ToolCheckWin.Services;

namespace ToolCheckWin.Views;

public partial class MainWindow : Window
{
    private readonly HardwareReportModel _report = new();
    private readonly HardwareScanner _scanner = new();
    private readonly SecurityChecker _securityChecker = new();
    private bool _isUiInitialized;

    public MainWindow()
    {
        InitializeComponent();
        _isUiInitialized = true;

        KeyboardTester.PeripheralStateChanged += OnPeripheralStateChanged;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RunFullHardwareScanAsync();
    }

    private async Task RunFullHardwareScanAsync()
    {
        try
        {
            BtnRescan.IsEnabled = false;
            TxtScanStatus.Text = "Đang truy vấn WMI, SMART, EDID & MDM...";
            TxtScanStatus.Foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21));

            var sysTask = _scanner.GetSystemSpecAsync(_report);
            var batTask = _scanner.GetBatteryInfoAsync(_report);
            var diskTask = _scanner.GetDiskSmartAsync(_report);
            var secTask = _securityChecker.CheckMdmAndLockAsync(_report);

            await Task.WhenAll(sysTask, batTask, diskTask, secTask);
            _report.DisplayEdid = _scanner.GetDisplayEdidInfo();

            RefreshDashboardUi();

            TxtScanStatus.Text = "✔ Hoàn tất quét phần cứng";
            TxtScanStatus.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
        }
        catch (Exception ex)
        {
            TxtScanStatus.Text = $"Lỗi quét: {ex.Message}";
        }
        finally
        {
            BtnRescan.IsEnabled = true;
        }
    }

    private void OnPeripheralStateChanged()
    {
        _report.KeyboardPassed = KeyboardTester.KeyboardPassed;
        _report.TouchpadPassed = KeyboardTester.TouchpadPassed;
        _report.AudioCamPassed = KeyboardTester.AudioCamPassed;
        RecalculateAndRenderScores();
    }

    private void RefreshDashboardUi()
    {
        // System & EDID
        TxtModelInfo.Text = $"{_report.Manufacturer} {_report.MachineModel}".Trim();
        TxtSerialInfo.Text = string.IsNullOrWhiteSpace(_report.SerialNumber) ? "N/A" : _report.SerialNumber;
        TxtCpuInfo.Text = _report.CpuName;
        TxtRamInfo.Text = $"{_report.TotalRamGB} GB ({_report.RamStickCount} thanh @ {_report.RamBusSpeedMHz} MHz)";
        TxtGpuInfo.Text = _report.GpuList.Count > 0 ? string.Join(" | ", _report.GpuList) : "Standard Display Adapter";
        TxtEdidInfo.Text = $"Mã: {_report.DisplayEdid.ManufacturerCode} (Tuần {_report.DisplayEdid.ManufactureWeek}/{_report.DisplayEdid.ManufactureYear}) — {_report.DisplayEdid.Resolution} @ {_report.DisplayEdid.RefreshRateHz}Hz";

        // Battery
        if (_report.HasBattery)
        {
            TxtBatteryHealth.Text = $"Health: {_report.BatteryHealthPercent:F1}% ({_report.FullChargedCapacityMWh}/{_report.DesignedCapacityMWh} mWh) • Sạc: {_report.CycleCount} lần";
            BarBatteryHealth.Value = _report.BatteryHealthPercent;
            BarBatteryHealth.Foreground = _report.BatteryHealthPercent >= 80
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                : (_report.BatteryHealthPercent >= 50
                    ? new SolidColorBrush(Color.FromRgb(234, 179, 8))
                    : new SolidColorBrush(Color.FromRgb(239, 68, 68)));
        }
        else
        {
            TxtBatteryHealth.Text = "Máy bàn / Không có Pin (20/20 điểm)";
            BarBatteryHealth.Value = 100;
        }

        // Disk SMART
        TxtDiskName.Text = _report.PrimaryDiskName;
        TxtDiskHealthTemp.Text = $"Health: {_report.DiskHealthPercent}%  |  Nhiệt độ: {_report.DiskTempC} °C";
        TxtDiskSmartErrors.Text = $"Giờ chạy: {_report.DiskPowerOnHours} giờ  |  Lỗi Đọc/Ghi: {_report.BadSectorsOrErrors}";

        // Security
        TxtMdmStatus.Text = _report.MdmDetail;
        if (_report.IsMdmEnrolled)
        {
            MdmAlertBanner.Background = new SolidColorBrush(Color.FromRgb(69, 10, 10));
            MdmAlertBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            TxtMdmStatus.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        }
        else
        {
            MdmAlertBanner.Background = new SolidColorBrush(Color.FromRgb(5, 46, 22));
            MdmAlertBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            TxtMdmStatus.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
        }

        TxtTpmStatus.Text = _report.HasTpm20 ? "✔ Đã kích hoạt TPM 2.0 (+5đ)" : "✖ Không tìm thấy TPM 2.0 (0đ)";
        TxtSecureBootStatus.Text = _report.SecureBootEnabled ? "✔ Đang bật (Enabled)" : "⚠ Đang tắt (Disabled)";

        RecalculateAndRenderScores();
    }

    private void RecalculateAndRenderScores()
    {
        ScoringEngine.Calculate(_report);

        TxtTotalScore.Text = _report.TotalScore.ToString();
        TxtGrade.Text = $"HẠNG: {_report.Grade}";
        TxtBatteryScore.Text = $"{_report.BatteryScore}/20";
        TxtDiskScore.Text = $"{_report.DiskScore}/20";
        TxtDisplayScore.Text = $"{_report.DisplayScore}/20";
        TxtPeripheralScore.Text = $"{_report.PeripheralScore}/20";
        TxtSecurityScore.Text = $"{_report.SecurityScore}/20";
        TxtReportHash.Text = _report.ReportHash;

        // Update Score Ring stroke length & color
        // Circumference in stroke thickness units (136 - 10) * pi / 10 = 39.58
        double dashLength = (_report.TotalScore / 100.0) * 39.58;
        ScoreRingEllipse.StrokeDashArray = new DoubleCollection { dashLength, 100 };

        Color gradeColor = _report.Grade.StartsWith("A")
            ? Color.FromRgb(34, 197, 94)
            : (_report.Grade.StartsWith("B")
                ? Color.FromRgb(56, 189, 248)
                : (_report.Grade.StartsWith("C")
                    ? Color.FromRgb(250, 204, 21)
                    : Color.FromRgb(239, 68, 68)));

        ScoreRingEllipse.Stroke = new SolidColorBrush(gradeColor);
        TxtGrade.Foreground = new SolidColorBrush(gradeColor);
        TxtFooterSummary.Text = $"Serial: {_report.SerialNumber} | Tổng điểm: {_report.TotalScore}/100 ({_report.Grade}) | Thời gian (UTC): {_report.GeneratedAtUtc:yyyy-MM-dd HH:mm}";
    }

    private async void BtnRescan_Click(object sender, RoutedEventArgs e)
    {
        await RunFullHardwareScanAsync();
    }

    private void BtnDisplayTest_Click(object sender, RoutedEventArgs e)
    {
        // Temporarily unhook low-level keyboard hook while fullscreen display test window is open
        KeyboardTester.UninstallKeyboardHook();
        try
        {
            var testWin = new DisplayTestWindow();
            if (testWin.ShowDialog() == true)
            {
                _report.DisplayStatus = testWin.SelectedDisplayStatus;
                CboDisplayStatus.SelectedIndex = _report.DisplayStatus;
                RecalculateAndRenderScores();
            }
        }
        finally
        {
            KeyboardTester.InstallKeyboardHook();
        }
    }

    private void CboDisplayStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUiInitialized) return;
        _report.DisplayStatus = CboDisplayStatus.SelectedIndex;
        RecalculateAndRenderScores();
    }

    private void ChkBiosLocked_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isUiInitialized) return;
        _report.IsBiosLocked = ChkBiosLocked.IsChecked == true;
        RecalculateAndRenderScores();
    }

    private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Title = "Xuất Chứng Chỉ Thẩm Định Phần Cứng (PDF A4)",
            Filter = "PDF Document (*.pdf)|*.pdf",
            FileName = $"ToolCheckWin_{_report.SerialNumber}_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                ReportExporter.ExportToPdf(_report, sfd.FileName);
                RecalculateAndRenderScores();
                MessageBox.Show($"Đã xuất chứng chỉ PDF thành công!\nĐường dẫn: {sfd.FileName}\nMã xác thực SHA-256: {_report.ReportHash}",
                    "Xuất PDF Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất PDF: {ex.Message}", "Lỗi Xuất File", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnExportPng_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Title = "Xuất Ảnh Chụp Báo Cáo (PNG)",
            Filter = "PNG Image (*.png)|*.png",
            FileName = $"ToolCheckWin_{_report.SerialNumber}_{DateTime.Now:yyyyMMdd_HHmm}.png"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                RecalculateAndRenderScores();
                ReportExporter.ExportToPng(DashboardCaptureRoot, _report, sfd.FileName);
                MessageBox.Show($"Đã xuất ảnh báo cáo PNG thành công!\nĐường dẫn: {sfd.FileName}\nMã xác thực SHA-256: {_report.ReportHash}",
                    "Xuất PNG Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất PNG: {ex.Message}", "Lỗi Xuất File", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
