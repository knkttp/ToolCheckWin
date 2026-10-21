using System;
using System.Collections.Generic;

namespace ToolCheckWin.Models;

public class DiskSmartInfo
{
    public string DeviceId { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public double SizeGB { get; set; }
    public int TemperatureC { get; set; }
    public int Wear { get; set; }
    public int HealthPercent => Math.Clamp(100 - Wear, 0, 100);
    public ulong PowerOnHours { get; set; }
    public ulong ReadErrorsTotal { get; set; }
    public ulong WriteErrorsTotal { get; set; }
    public ulong BadSectorsOrErrors => ReadErrorsTotal + WriteErrorsTotal;
}

public class DisplayEdidModel
{
    public string ManufacturerCode { get; set; } = "N/A";
    public int ManufactureWeek { get; set; }
    public int ManufactureYear { get; set; }
    public int RefreshRateHz { get; set; }
    public string Resolution { get; set; } = "";
}

public class HardwareReportModel
{
    // System Spec
    public string Manufacturer { get; set; } = "";
    public string MachineModel { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string CpuName { get; set; } = "";
    public double TotalRamGB { get; set; }
    public int RamStickCount { get; set; }
    public int RamBusSpeedMHz { get; set; }
    public List<string> GpuList { get; set; } = new();

    // Battery Info
    public bool HasBattery { get; set; }
    public int DesignedCapacityMWh { get; set; }
    public int FullChargedCapacityMWh { get; set; }
    public int CycleCount { get; set; }
    public double BatteryHealthPercent { get; set; }

    // Disk SMART Info
    public List<DiskSmartInfo> Disks { get; set; } = new();
    public string PrimaryDiskName { get; set; } = "N/A";
    public int DiskHealthPercent { get; set; } = 100;
    public int DiskTempC { get; set; }
    public ulong DiskPowerOnHours { get; set; }
    public ulong BadSectorsOrErrors { get; set; }

    // Display Info
    public DisplayEdidModel DisplayEdid { get; set; } = new();
    /// <summary>
    /// 0 = Không điểm chết (Đạt chuẩn), 1 = Có lỗi nhẹ (1-2 điểm / ám nhẹ), 2 = Lỗi nặng / Chưa đạt
    /// </summary>
    public int DisplayStatus { get; set; } = 0;

    // Interactive Peripherals
    public bool KeyboardPassed { get; set; }
    public bool TouchpadPassed { get; set; }
    public bool AudioCamPassed { get; set; }

    // Security & Lock Status
    public bool IsMdmEnrolled { get; set; }
    public string MdmDetail { get; set; } = "Sạch (Không khóa MDM/Autopilot)";
    public bool IsBiosLocked { get; set; }
    public bool HasTpm20 { get; set; }
    public bool SecureBootEnabled { get; set; }

    // Scores (Max 20 each, Total 100)
    public int BatteryScore { get; set; }
    public int DiskScore { get; set; }
    public int DisplayScore { get; set; }
    public int PeripheralScore { get; set; }
    public int SecurityScore { get; set; }
    public int TotalScore { get; set; }
    public string Grade { get; set; } = "C (CẦN LƯU Ý)";
    public string ReportHash { get; set; } = "";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}
