using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using ToolCheckWin.Models;

namespace ToolCheckWin.Core;

public class HardwareScanner
{
    public async Task GetSystemSpecAsync(HardwareReportModel model)
    {
        await Task.Run(() =>
        {
            try
            {
                using var csSearcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT Manufacturer, Model FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in csSearcher.Get())
                {
                    model.Manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "Unknown";
                    model.MachineModel = obj["Model"]?.ToString()?.Trim() ?? "Unknown";
                }
            }
            catch { }

            try
            {
                using var biosSearcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT SerialNumber FROM Win32_BIOS");
                foreach (ManagementObject obj in biosSearcher.Get())
                {
                    model.SerialNumber = obj["SerialNumber"]?.ToString()?.Trim() ?? "N/A";
                }
            }
            catch { }

            try
            {
                using var cpuSearcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT Name FROM Win32_Processor");
                foreach (ManagementObject obj in cpuSearcher.Get())
                {
                    model.CpuName = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                    break;
                }
            }
            catch { }

            try
            {
                using var ramSearcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT Capacity, Speed FROM Win32_PhysicalMemory");
                ulong totalBytes = 0;
                int sticks = 0;
                int speed = 0;
                foreach (ManagementObject obj in ramSearcher.Get())
                {
                    sticks++;
                    if (obj["Capacity"] != null && ulong.TryParse(obj["Capacity"].ToString(), out ulong cap))
                    {
                        totalBytes += cap;
                    }
                    if (speed == 0 && obj["Speed"] != null && int.TryParse(obj["Speed"].ToString(), out int spd))
                    {
                        speed = spd;
                    }
                }
                model.TotalRamGB = Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 1);
                model.RamStickCount = sticks;
                model.RamBusSpeedMHz = speed;
            }
            catch { }

            try
            {
                model.GpuList.Clear();
                using var gpuSearcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT Name FROM Win32_VideoController");
                foreach (ManagementObject obj in gpuSearcher.Get())
                {
                    string? gpuName = obj["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(gpuName))
                    {
                        model.GpuList.Add(gpuName);
                    }
                }
            }
            catch { }
        });
    }

    public async Task GetBatteryInfoAsync(HardwareReportModel model)
    {
        await Task.Run(() =>
        {
            try
            {
                int designed = 0;
                int fullCharged = 0;
                int cycleCount = 0;
                bool foundBattery = false;

                using (var staticSearcher = new ManagementObjectSearcher(@"root\WMI", "SELECT DesignedCapacity FROM BatteryStaticData"))
                {
                    foreach (ManagementObject obj in staticSearcher.Get())
                    {
                        if (obj["DesignedCapacity"] != null && int.TryParse(obj["DesignedCapacity"].ToString(), out int dc) && dc > 0)
                        {
                            designed += dc;
                            foundBattery = true;
                        }
                    }
                }

                using (var fullSearcher = new ManagementObjectSearcher(@"root\WMI", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity"))
                {
                    foreach (ManagementObject obj in fullSearcher.Get())
                    {
                        if (obj["FullChargedCapacity"] != null && int.TryParse(obj["FullChargedCapacity"].ToString(), out int fc) && fc > 0)
                        {
                            fullCharged += fc;
                        }
                    }
                }

                using (var cycleSearcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CycleCount FROM BatteryCycleCount"))
                {
                    foreach (ManagementObject obj in cycleSearcher.Get())
                    {
                        if (obj["CycleCount"] != null && int.TryParse(obj["CycleCount"].ToString(), out int cc))
                        {
                            cycleCount = Math.Max(cycleCount, cc);
                        }
                    }
                }

                if (!foundBattery || designed <= 0)
                {
                    model.HasBattery = false;
                    model.DesignedCapacityMWh = 0;
                    model.FullChargedCapacityMWh = 0;
                    model.CycleCount = 0;
                    model.BatteryHealthPercent = 100.0;
                    return;
                }

                model.HasBattery = true;
                model.DesignedCapacityMWh = designed;
                model.FullChargedCapacityMWh = fullCharged;
                model.CycleCount = cycleCount;
                model.BatteryHealthPercent = Math.Round(Math.Clamp((double)fullCharged / designed * 100.0, 0.0, 100.0), 1);
            }
            catch
            {
                model.HasBattery = false;
                model.BatteryHealthPercent = 100.0;
            }
        });
    }

    public async Task GetDiskSmartAsync(HardwareReportModel model)
    {
        await Task.Run(() =>
        {
            model.Disks.Clear();
            var diskMap = new Dictionary<string, DiskSmartInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var diskSearcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT DeviceId, FriendlyName, Size FROM MSFT_PhysicalDisk");
                foreach (ManagementObject obj in diskSearcher.Get())
                {
                    string devId = obj["DeviceId"]?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(devId)) continue;

                    ulong sizeBytes = 0;
                    if (obj["Size"] != null) ulong.TryParse(obj["Size"].ToString(), out sizeBytes);

                    var info = new DiskSmartInfo
                    {
                        DeviceId = devId,
                        FriendlyName = obj["FriendlyName"]?.ToString()?.Trim() ?? $"Disk {devId}",
                        SizeGB = Math.Round(sizeBytes / (1024.0 * 1024.0 * 1024.0), 1),
                        Wear = 0
                    };
                    diskMap[devId] = info;
                }

                using var smartSearcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage",
                    "SELECT DeviceId, Temperature, Wear, PowerOnHours, ReadErrorsTotal, WriteErrorsTotal FROM MSFT_StorageReliabilityCounter");
                foreach (ManagementObject obj in smartSearcher.Get())
                {
                    string devId = obj["DeviceId"]?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(devId)) continue;

                    if (!diskMap.TryGetValue(devId, out var info))
                    {
                        info = new DiskSmartInfo { DeviceId = devId, FriendlyName = $"Disk {devId}" };
                        diskMap[devId] = info;
                    }

                    if (obj["Temperature"] != null && int.TryParse(obj["Temperature"].ToString(), out int temp))
                        info.TemperatureC = temp;

                    if (obj["Wear"] != null && int.TryParse(obj["Wear"].ToString(), out int wear))
                        info.Wear = Math.Clamp(wear, 0, 100);

                    if (obj["PowerOnHours"] != null && ulong.TryParse(obj["PowerOnHours"].ToString(), out ulong poh))
                        info.PowerOnHours = poh;

                    if (obj["ReadErrorsTotal"] != null && ulong.TryParse(obj["ReadErrorsTotal"].ToString(), out ulong rErr))
                        info.ReadErrorsTotal = rErr;

                    if (obj["WriteErrorsTotal"] != null && ulong.TryParse(obj["WriteErrorsTotal"].ToString(), out ulong wErr))
                        info.WriteErrorsTotal = wErr;
                }
            }
            catch { }

            model.Disks.AddRange(diskMap.Values.OrderBy(d => d.DeviceId));
            var primary = model.Disks.FirstOrDefault();
            if (primary != null)
            {
                model.PrimaryDiskName = $"{primary.FriendlyName} ({primary.SizeGB} GB)";
                model.DiskHealthPercent = primary.HealthPercent;
                model.DiskTempC = primary.TemperatureC;
                model.DiskPowerOnHours = primary.PowerOnHours;
                model.BadSectorsOrErrors = primary.BadSectorsOrErrors;
            }
            else
            {
                model.PrimaryDiskName = "Unknown Storage";
                model.DiskHealthPercent = 100;
                model.DiskTempC = 35;
                model.DiskPowerOnHours = 0;
                model.BadSectorsOrErrors = 0;
            }
        });
    }

    public DisplayEdidModel GetDisplayEdidInfo()
    {
        var result = new DisplayEdidModel();

        try
        {
            var devMode = new NativeMethods.DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
            if (NativeMethods.EnumDisplaySettings(null, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
            {
                result.RefreshRateHz = devMode.dmDisplayFrequency;
                result.Resolution = $"{devMode.dmPelsWidth} x {devMode.dmPelsHeight}";
            }
        }
        catch { }

        try
        {
            using var displayKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY");
            if (displayKey != null)
            {
                foreach (string monitorType in displayKey.GetSubKeyNames())
                {
                    using var monitorTypeKey = displayKey.OpenSubKey(monitorType);
                    if (monitorTypeKey == null) continue;

                    foreach (string instance in monitorTypeKey.GetSubKeyNames())
                    {
                        using var devParamKey = monitorTypeKey.OpenSubKey($@"{instance}\Device Parameters");
                        if (devParamKey?.GetValue("EDID") is byte[] edid && edid.Length >= 18)
                        {
                            // Decode bytes 8-9: 3-letter Compressed ASCII Manufacturer ID
                            ushort mfgRaw = (ushort)((edid[8] << 8) | edid[9]);
                            char c1 = (char)(((mfgRaw >> 10) & 0x1F) + 'A' - 1);
                            char c2 = (char)(((mfgRaw >> 5) & 0x1F) + 'A' - 1);
                            char c3 = (char)((mfgRaw & 0x1F) + 'A' - 1);
                            result.ManufacturerCode = $"{c1}{c2}{c3}";

                            // Decode bytes 16-17: Week and Year of Manufacture
                            result.ManufactureWeek = edid[16];
                            result.ManufactureYear = 1990 + edid[17];
                            return result;
                        }
                    }
                }
            }
        }
        catch { }

        return result;
    }
}
