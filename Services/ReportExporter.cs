using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ToolCheckWin.Core;
using ToolCheckWin.Models;

namespace ToolCheckWin.Services;

public static class ReportExporter
{
    static ReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void ExportToPdf(HardwareReportModel m, string filePath)
    {
        ScoringEngine.Calculate(m);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(QuestPDF.Helpers.Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI").FontColor("#1E293B"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("CHỨNG CHỈ THẨM ĐỊNH PHẦN CỨNG WINDOWS").FontSize(18).Bold().FontColor("#0F172A");
                            c.Item().Text("TOOLCHECKWIN // AUTOMATED HARDWARE INSPECTION REPORT").FontSize(9).FontColor("#64748B");
                        });

                        row.ConstantItem(150).Border(1).BorderColor("#CBD5E1").Background("#F8FAFC").Padding(8).Column(c =>
                        {
                            c.Item().AlignCenter().Text($"ĐIỂM TỔNG: {m.TotalScore}/100").FontSize(13).Bold().FontColor("#0284C7");
                            c.Item().AlignCenter().Text($"HẠNG: {m.Grade}").FontSize(10).Bold().FontColor(m.TotalScore >= 75 && !m.IsMdmEnrolled ? "#16A34A" : "#DC2626");
                        });
                    });

                    col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor("#0284C7");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    // Section 1: System Spec
                    col.Item().Text("1. THÔNG SỐ HỆ THỐNG (SYSTEM SPECIFICATION)").FontSize(12).Bold().FontColor("#0F172A");
                    col.Item().Border(1).BorderColor("#E2E8F0").Padding(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(140);
                            c.RelativeColumn();
                        });

                        void AddRow(string label, string val)
                        {
                            t.Cell().PaddingVertical(3).Text(label).SemiBold().FontColor("#475569");
                            t.Cell().PaddingVertical(3).Text(val);
                        }

                        AddRow("Hãng / Model:", $"{m.Manufacturer} - {m.MachineModel}");
                        AddRow("Serial Number (BIOS):", m.SerialNumber);
                        AddRow("Vi xử lý (CPU):", m.CpuName);
                        AddRow("Bộ nhớ RAM:", $"{m.TotalRamGB} GB ({m.RamStickCount} thanh @ {m.RamBusSpeedMHz} MHz)");
                        AddRow("Card đồ họa (GPU):", m.GpuList.Count > 0 ? string.Join(" | ", m.GpuList) : "N/A");
                        AddRow("Màn hình (EDID):", $"Mã tấm nền: {m.DisplayEdid.ManufacturerCode} ({m.DisplayEdid.ManufactureWeek}/{m.DisplayEdid.ManufactureYear}) - {m.DisplayEdid.Resolution} @ {m.DisplayEdid.RefreshRateHz}Hz");
                    });

                    // Section 2: Battery & Storage
                    col.Item().Text("2. SỨC KHỎE PIN & Ổ CỨNG (BATTERY & SMART STORAGE)").FontSize(12).Bold().FontColor("#0F172A");
                    col.Item().Border(1).BorderColor("#E2E8F0").Padding(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(140);
                            c.RelativeColumn();
                        });

                        void AddRow(string label, string val)
                        {
                            t.Cell().PaddingVertical(3).Text(label).SemiBold().FontColor("#475569");
                            t.Cell().PaddingVertical(3).Text(val);
                        }

                        if (m.HasBattery)
                        {
                            AddRow("Trạng thái Pin:", $"Health: {m.BatteryHealthPercent:F1}% ({m.FullChargedCapacityMWh} / {m.DesignedCapacityMWh} mWh) | Chu kỳ sạc: {m.CycleCount}");
                        }
                        else
                        {
                            AddRow("Trạng thái Pin:", "Máy bàn / Không có Pin (Mặc định đạt 20/20 điểm)");
                        }

                        AddRow("Ổ cứng chính:", m.PrimaryDiskName);
                        AddRow("Sức khỏe & Nhiệt độ SSD:", $"Health: {m.DiskHealthPercent}% | Nhiệt độ: {m.DiskTempC} °C | Giờ chạy: {m.DiskPowerOnHours}h | Lỗi Đọc/Ghi: {m.BadSectorsOrErrors}");
                    });

                    // Section 3: Security & MDM Lock
                    col.Item().Text("3. AN NINH & KHÓA QUẢN TRỊ (SECURITY & MDM / AUTOPILOT)").FontSize(12).Bold().FontColor("#0F172A");
                    col.Item().Border(1).BorderColor("#E2E8F0").Padding(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(140);
                            c.RelativeColumn();
                        });

                        t.Cell().PaddingVertical(3).Text("Trạng thái MDM / Intune:").SemiBold().FontColor("#475569");
                        t.Cell().PaddingVertical(3).Text(m.MdmDetail).Bold().FontColor(m.IsMdmEnrolled ? "#DC2626" : "#16A34A");

                        t.Cell().PaddingVertical(3).Text("Khóa mật khẩu BIOS:").SemiBold().FontColor("#475569");
                        t.Cell().PaddingVertical(3).Text(m.IsBiosLocked ? "BỊ KHÓA (BIOS Locked)" : "Không khóa (Sạch)");

                        t.Cell().PaddingVertical(3).Text("Bảo mật phần cứng:").SemiBold().FontColor("#475569");
                        t.Cell().PaddingVertical(3).Text($"TPM 2.0: {(m.HasTpm20 ? "Có" : "Không")} | Secure Boot: {(m.SecureBootEnabled ? "Bật" : "Tắt")}");
                    });

                    // Section 4: Score Breakdown
                    col.Item().Text("4. BẢNG ĐIỂM CHI TIẾT (SCORING BREAKDOWN)").FontSize(12).Bold().FontColor("#0F172A");
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.ConstantColumn(90);
                        });

                        t.Header(h =>
                        {
                            h.Cell().Background("#0F172A").Padding(6).Text("Hạng mục kiểm định").FontColor("#FFFFFF").Bold();
                            h.Cell().Background("#0F172A").Padding(6).Text("Trạng thái thực tế").FontColor("#FFFFFF").Bold();
                            h.Cell().Background("#0F172A").Padding(6).AlignRight().Text("Điểm số").FontColor("#FFFFFF").Bold();
                        });

                        void AddScoreRow(string name, string detail, int score)
                        {
                            t.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).Text(name);
                            t.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).Text(detail);
                            t.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignRight().Text($"{score} / 20").Bold();
                        }

                        AddScoreRow("1. Pin (Battery Score)", m.HasBattery ? $"Health {m.BatteryHealthPercent:F1}%" : "Desktop (No Battery)", m.BatteryScore);
                        AddScoreRow("2. Ổ cứng (Disk Score)", $"Health {m.DiskHealthPercent}%, Temp {m.DiskTempC}°C, Err: {m.BadSectorsOrErrors}", m.DiskScore);
                        AddScoreRow("3. Màn hình (Display Score)", m.DisplayStatus == 0 ? "Không điểm chết" : (m.DisplayStatus == 1 ? "Lỗi nhẹ / Ám nhẹ" : "Lỗi nặng / Chưa đạt"), m.DisplayScore);
                        AddScoreRow("4. Ngoại vi (Peripheral Score)", $"Phím: {(m.KeyboardPassed ? "Đạt" : "Chưa")} | Touchpad: {(m.TouchpadPassed ? "Đạt" : "Chưa")} | Loa/Mic: {(m.AudioCamPassed ? "Đạt" : "Chưa")}", m.PeripheralScore);
                        AddScoreRow("5. Bảo mật (Security Score)", $"MDM: {(m.IsMdmEnrolled ? "Khóa" : "Sạch")} | BIOS: {(m.IsBiosLocked ? "Khóa" : "Mở")} | TPM 2.0: {(m.HasTpm20 ? "Có" : "Không")}", m.SecurityScore);
                    });
                });

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor("#CBD5E1");
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text($"Thời gian tạo (UTC): {m.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor("#64748B");
                        row.RelativeItem().AlignRight().Text($"SHA-256 Anti-Tamper Hash: {m.ReportHash}").FontSize(8).Bold().FontColor("#0F172A");
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    public static void ExportToPng(FrameworkElement targetElement, HardwareReportModel m, string filePath)
    {
        ScoringEngine.Calculate(m);

        double width = targetElement.ActualWidth > 0 ? targetElement.ActualWidth : 1200;
        double height = targetElement.ActualHeight > 0 ? targetElement.ActualHeight : 800;

        var renderBitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height),
            96d,
            96d,
            PixelFormats.Pbgra32);

        renderBitmap.Render(targetElement);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }
}
