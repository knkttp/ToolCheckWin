using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Win32;
using ToolCheckWin.Models;

namespace ToolCheckWin.Core;

public class SecurityChecker
{
    public async Task CheckMdmAndLockAsync(HardwareReportModel model)
    {
        await Task.Run(() =>
        {
            bool azureJoined = false;
            bool enrollmentFound = false;
            string mdmProvider = "";

            // 1. Run dsregcmd /status
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dsregcmd",
                    Arguments = "/status",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(4000);
                    foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.Contains("AzureAdJoined", StringComparison.OrdinalIgnoreCase) &&
                            line.Contains("YES", StringComparison.OrdinalIgnoreCase))
                        {
                            azureJoined = true;
                            break;
                        }
                    }
                }
            }
            catch { }

            // 2. Scan HKLM\SOFTWARE\Microsoft\Enrollments
            try
            {
                using var enrollmentsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Enrollments");
                if (enrollmentsKey != null)
                {
                    // Các ProviderID mặc định nội bộ của Windows (không phải MDM / Autopilot doanh nghiệp)
                    var systemBuiltinProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Local_Management",
                        "Deploy Authority",
                        "Cloud Authority",
                        "Local Authority",
                        "MS DM Server"
                    };

                    foreach (string subKeyName in enrollmentsKey.GetSubKeyNames())
                    {
                        using var subKey = enrollmentsKey.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        string upn = subKey.GetValue("UPN")?.ToString()?.Trim() ?? "";
                        string providerId = subKey.GetValue("ProviderID")?.ToString()?.Trim() ?? "";
                        string discoveryUrl = subKey.GetValue("DiscoveryServiceFullURL")?.ToString()?.Trim() ?? "";

                        // Bỏ qua các provider mặc định của Windows
                        if (!string.IsNullOrEmpty(providerId) && systemBuiltinProviders.Contains(providerId))
                        {
                            continue;
                        }

                        // MDM thực tế sẽ có UPN (email công ty), Discovery URL (intune / airwatch) hoặc ProviderID doanh nghiệp
                        if (!string.IsNullOrEmpty(upn) ||
                            !string.IsNullOrEmpty(discoveryUrl) ||
                            (!string.IsNullOrEmpty(providerId) && !systemBuiltinProviders.Contains(providerId)))
                        {
                            enrollmentFound = true;
                            mdmProvider = !string.IsNullOrEmpty(providerId) ? providerId : (!string.IsNullOrEmpty(upn) ? upn : discoveryUrl);
                            break;
                        }
                    }
                }
            }
            catch { }

            model.IsMdmEnrolled = azureJoined || enrollmentFound;
            if (model.IsMdmEnrolled)
            {
                model.MdmDetail = $"CẢNH BÁO: Phát hiện khóa MDM/Autopilot ({(azureJoined ? "AzureAD Joined" : mdmProvider)})";
            }
            else
            {
                model.MdmDetail = "Sạch (Không khóa MDM / Autopilot)";
            }

            // 3. Check Secure Boot State: HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State
            try
            {
                using var sbKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                if (sbKey != null && sbKey.GetValue("UEFISecureBootEnabled") is int sbVal)
                {
                    model.SecureBootEnabled = sbVal == 1;
                }
            }
            catch { }

            // 4. Check TPM 2.0 via root\CIMV2\Security\MicrosoftTpm or Registry Fallback
            try
            {
                using var tpmSearcher = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftTpm", "SELECT SpecVersion, IsActivated_InitialValue FROM Win32_Tpm");
                foreach (ManagementObject obj in tpmSearcher.Get())
                {
                    string specVer = obj["SpecVersion"]?.ToString() ?? "";
                    if (specVer.Contains("2.0"))
                    {
                        model.HasTpm20 = true;
                        break;
                    }
                }
            }
            catch { }

            // Fallback: Kiểm tra Registry TPM nếu WMI quyền bị hạn chế
            if (!model.HasTpm20)
            {
                try
                {
                    using var tpmKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\TPM\WMI");
                    if (tpmKey != null && tpmKey.GetValue("TpmBackedDeviceID") != null)
                    {
                        model.HasTpm20 = true;
                    }
                }
                catch { }
            }
        });
    }
}
