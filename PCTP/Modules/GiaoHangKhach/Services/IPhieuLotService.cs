using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// LOT business workflow — không phụ thuộc WinForms.
    /// Implementation: <see cref="PhieuLotService"/>.
    /// </summary>
    public interface IPhieuLotService
    {
        /// <summary>
        /// Tính và lưu LOT cho các dòng trong bảng tạm; khi trùng MAHANG/SOLUONG
        /// cho nhiều lựa chọn, <paramref name="chonSttKhiTrung"/> được gọi để
        /// presenter/UI chọn STT phù hợp.
        /// </summary>
        List<(int Stt, string Lot)> TinhTongLot(
            DataTable bangTam,
            string tenBan,
            string docQRTable,
            string tmpTable,
            Func<DataTable, int> chonSttKhiTrung);
    }
}