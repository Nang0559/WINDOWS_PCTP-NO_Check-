using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Shell.Services
{
    /// <summary>Tóm tắt "khách nào đang do máy nào đọc QR" cho Dashboard / Ribbon.</summary>
    internal static class QrLockSummary
    {
        /// <param name="hostName">Giữ để tương thích; chủ khoá được xác định theo IP server đọc (IsOwnedByMe).</param>
        internal static string Build(string hostName, out bool hostOwnsAny)
        {
            var locks = new CustomerQrLockService(new SQLPROVIDER()).ListAll();
            var parts = new List<string>();
            hostOwnsAny = false;

            foreach (CustomerConfig cfg in CustomerTableConfig.All)
            {
                if (cfg.Delivery == null) continue;

                foreach (string mode in new[] { "MP", "SP" })
                {
                    QrLockInfo info = locks.FirstOrDefault(l =>
                        string.Equals(l.CustomerNo, cfg.CustomerNo, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(l.ScanMode, mode, StringComparison.OrdinalIgnoreCase));
                    string owner = "(trống)";
                    if (info != null && info.HasOwner)
                    {
                        owner = info.OwnerDisplay + (info.IsStale ? " (không phản hồi)" : string.Empty);
                        if (info.IsOwnedByMe && !info.IsStale) hostOwnsAny = true;
                    }
                    parts.Add(cfg.DisplayName + " " + mode + ": " + owner);
                }
            }

            return parts.Count == 0 ? "(chưa cấu hình)" : string.Join("  |  ", parts);
        }
    }
}
