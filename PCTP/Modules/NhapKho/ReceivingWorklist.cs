using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PCTP.Modules.NhapKho
{
    /// <summary>
    /// NGUỒN DỮ LIỆU DUY NHẤT cho danh sách "Phiếu SX chờ nhập kho".
    ///
    /// Được FormNhapKhoTienTrinh tạo + nạp 1 lần, sau đó truyền SANG FormEnterItemSV
    /// (cùng 1 instance, KHÔNG nạp lại). Hai grid bind chung <see cref="Items"/> nên:
    ///   - không có 2 query khác điều kiện lọc → 2 grid không bao giờ lệch nhau;
    ///   - FormEnterItemSV cập nhật SL đã nhập → grid ở form cha tự đổi theo;
    ///   - rule "tem quét phải nằm trong danh sách chờ nhập" kiểm tra được bằng
    ///     <see cref="TryGet"/> mà không cần query lại.
    ///
    /// Giữ nguyên dòng trong phiên (kể cả khi đã nhập đủ) để cảnh báo "vượt sản lượng"
    /// vẫn hoạt động; dòng đủ hàng được tô xanh, và biến mất ở lần <see cref="Reload"/>.
    /// </summary>
    public sealed class ReceivingWorklist
    {
        private readonly Func<IList<PhieuNhapInfo>> _loader;
        private readonly Dictionary<string, PhieuNhapInfo> _index =
            new Dictionary<string, PhieuNhapInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _touched =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public BindingList<PhieuNhapInfo> Items { get; } = new BindingList<PhieuNhapInfo>();

        public ReceivingWorklist(Func<IList<PhieuNhapInfo>> loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        /// <summary>Nạp lại từ DB. Giữ nguyên instance BindingList để các grid đang bind không mất nguồn.</summary>
        public void Reload()
        {
            var data = _loader() ?? new List<PhieuNhapInfo>();

            Items.RaiseListChangedEvents = false;
            try
            {
                Items.Clear();
                _index.Clear();
                _touched.Clear();

                foreach (var p in data)
                {
                    if (p == null || string.IsNullOrWhiteSpace(p.Find)) continue;
                    if (_index.ContainsKey(p.Find)) continue; // FIND trùng: giữ dòng đầu
                    Items.Add(p);
                    _index[p.Find] = p;
                }
            }
            finally
            {
                Items.RaiseListChangedEvents = true;
                Items.ResetBindings();
            }
        }

        public bool TryGet(string find, out PhieuNhapInfo row)
        {
            row = null;
            return !string.IsNullOrWhiteSpace(find) && _index.TryGetValue(find, out row);
        }

        public PhieuNhapInfo FindByLot(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot)) return null;
            return Items.FirstOrDefault(x =>
                string.Equals(x.LotNo, lot, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsTouched(string find) => !string.IsNullOrEmpty(find) && _touched.Contains(find);

        /// <summary>Đồng bộ số liệu mới nhất (từ DB) vào đúng dòng đang hiển thị.</summary>
        public void Sync(PhieuNhapInfo latest)
        {
            if (latest == null || !TryGet(latest.Find, out var row)) return;
            row.SlSanXuat = latest.SlSanXuat;
            row.SlDaNhap = latest.SlDaNhap;
            row.KetThucLot = latest.KetThucLot;
            Items.ResetItem(Items.IndexOf(row));
        }

        /// <summary>Gọi SAU KHI lưu thành công: cộng SL, đánh dấu dòng đã bắn trong phiên.</summary>
        public void MarkReceived(string find, int qty)
        {
            if (!TryGet(find, out var row)) return;
            _touched.Add(find);
            row.SlDaNhap += qty;
            row.KetThucLot = row.SlSanXuat > 0 && row.SlDaNhap >= row.SlSanXuat;
            Items.ResetItem(Items.IndexOf(row));
        }
    }
}
