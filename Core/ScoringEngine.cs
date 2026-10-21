using System;
using System.Security.Cryptography;
using System.Text;
using ToolCheckWin.Models;

namespace ToolCheckWin.Core;

public static class ScoringEngine
{
    public static void Calculate(HardwareReportModel m)
    {
        // 1. BatteryScore (Max 20)
        if (!m.HasBattery)
        {
            m.BatteryScore = 20;
        }
        else if (m.BatteryHealthPercent >= 85)
        {
            m.BatteryScore = 20;
        }
        else if (m.BatteryHealthPercent < 50)
        {
            m.BatteryScore = 0;
        }
        else
        {
            m.BatteryScore = Math.Max(0, 20 - (int)((85 - m.BatteryHealthPercent) / 5) * 3);
        }

        // 2. DiskScore (Max 20)
        if (m.BadSectorsOrErrors > 0 || m.DiskHealthPercent < 50)
        {
            m.DiskScore = 0;
        }
        else
        {
            int baseDiskScore = m.DiskHealthPercent >= 90 ? 20 : 15;
            if (m.DiskTempC > 65)
            {
                baseDiskScore -= 5;
            }
            m.DiskScore = Math.Max(0, baseDiskScore);
        }

        // 3. DisplayScore (Max 20)
        m.DisplayScore = m.DisplayStatus == 0 ? 20 : (m.DisplayStatus == 1 ? 12 : 0);

        // 4. PeripheralScore (Max 20)
        m.PeripheralScore = (m.KeyboardPassed ? 10 : 0) + (m.TouchpadPassed ? 4 : 0) + (m.AudioCamPassed ? 6 : 0);

        // 5. SecurityScore (Max 20)
        m.SecurityScore = (m.IsMdmEnrolled ? 0 : 10) + (m.IsBiosLocked ? 0 : 5) + (m.HasTpm20 ? 5 : 0);

        // 6. TotalScore & Grade
        m.TotalScore = m.BatteryScore + m.DiskScore + m.DisplayScore + m.PeripheralScore + m.SecurityScore;

        if (m.IsMdmEnrolled || m.IsBiosLocked || m.TotalScore < 60)
        {
            m.Grade = "F (KHÔNG ĐẠT)";
        }
        else if (m.TotalScore >= 90)
        {
            m.Grade = "A (XUẤT SẮC)";
        }
        else if (m.TotalScore >= 75)
        {
            m.Grade = "B (TỐT)";
        }
        else
        {
            m.Grade = "C (CẦN LƯU Ý)";
        }

        // 7. SHA-256 Anti-Tamper Hash
        m.GeneratedAtUtc = DateTime.UtcNow;
        string rawPayload = $"{m.SerialNumber}|{m.TotalScore}|{m.GeneratedAtUtc:yyyyMMddHHmm}|TOOLCHECKWIN_SALT";
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload));
        m.ReportHash = Convert.ToHexString(hashBytes).Substring(0, 16);
    }
}
