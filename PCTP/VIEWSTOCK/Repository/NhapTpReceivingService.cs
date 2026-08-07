using PCTP.ClassSQL;
using PCTP.Models;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    /// <summary>
    /// Nhập hàng TP vào Slot — thay thế hoàn toàn luồng NHAP_TP cũ.
    /// Mỗi lần nhập = 1 transaction gồm:
    ///   1) Ghi/Cộng dồn STOCKTP (nguồn sự thật cho tổng tồn kho)
    ///   2) Tạo 1 "phiếu" mới (SlotLot, PhieuStatus=Active) tại Slot đã chọn
    ///   3) Cập nhật tổng hợp Slot (Quantity/ItemCode/ImportDate/IsOccupied)
    /// Không bao giờ được làm rời từng bước — nếu 1 bước lỗi, toàn bộ rollback.
    /// </summary>
    public class NhapTpReceivingService
    {
        private readonly SQLPROVIDER _sql;
        private readonly IStockTpRepository _stockTpRepo;
        private readonly IPhieuTrackingRepository _phieuRepo;

        public NhapTpReceivingService(
            SQLPROVIDER sql,
            IStockTpRepository stockTpRepo,
            IPhieuTrackingRepository phieuRepo)
        {
            _sql = sql;
            _stockTpRepo = stockTpRepo;
            _phieuRepo = phieuRepo;
        }

        /// <summary>
        /// Kiểm tra sơ bộ TRƯỚC khi mở transaction — dùng để UI báo lỗi sớm,
        /// không tốn transaction cho các trường hợp chắc chắn fail.
        /// </summary>
        public ScanResult KiemTraTruocKhiNhap(QRCodeInfo qr)
        {
            if (qr == null)
                return ScanResult.Fail("Không đọc được dữ liệu QR.");

            if (!qr.IsTongPhieu)
                return ScanResult.Fail("Vui lòng bắn tem TỔNG để nhập kho (không nhận tem thùng).");

            if (qr.Quantity <= 0)
                return ScanResult.Fail("Số lượng trên tem không hợp lệ.");

            // Chống quét trùng — mỗi QR gốc chỉ được nhập kho đúng 1 lần
            if (_phieuRepo.ExistsQrData(qr.RawQr))
                return ScanResult.Trung("Tem này đã được nhập kho trước đó!");

            return new ScanResult { IsOK = true };
        }

        /// <summary>
        /// Thực hiện nhập kho thật — TOÀN BỘ trong 1 transaction.
        /// Slot đích lấy từ selectedSlotText dạng
        /// "WH : .. - Rack : .. - Slot : .. - Capacity : ..".
        /// </summary>
        public ScanResult NhapTpVaoSlot(QRCodeInfo qr, string selectedSlotText, PhieuNhapInfo matchedPhieu = null)
        {
            var check = KiemTraTruocKhiNhap(qr);
            if (!check.IsOK) return check;

            //string lotNo = matchedPhieu != null
            //    ? matchedPhieu.LotNo
            //    : LotNoHelper.NormalizeLot(qr.RawLotNo ?? qr.LotNo);
            string lotNo = matchedPhieu.LotNo;

            string caseNo = !string.IsNullOrEmpty(qr.SoPhieuTong)
                ? qr.RawLotNo + qr.SoPhieuTong
                : qr.RawLotNo + "4";

            SlotHelper.ParseSlotString(selectedSlotText,
                out string wh, out string rack, out int slotNumber, out int capacity);

            var slotHelper = new SlotHelper();
            int slotId = slotHelper.GetSlotID(wh, rack, slotNumber);
            if (slotId <= 0)
                return ScanResult.Fail("Không tìm thấy Slot đích.");

            if (capacity <= 0)
                capacity = slotHelper.GetSlotCapacityById(slotId);

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    if (_stockTpRepo.ExistsCaseHistory(conn, tran, caseNo))
                    {
                        tran.Rollback();
                        return ScanResult.Trung($"Case [{caseNo}] đã được nhập kho trước đó!");
                    }

                    if (capacity > 0)
                    {
                        object qtyRaw = _sql.ExecuteScalar(conn, tran,
                            "SELECT ISNULL(Quantity,0) FROM Slot WHERE SlotId = @SlotId",
                            new[] { new SqlParameter("@SlotId", slotId) });
                        int qtyHienTai = qtyRaw == null || qtyRaw == DBNull.Value ? 0 : Convert.ToInt32(qtyRaw);

                        if (qtyHienTai + qr.Quantity > capacity)
                        {
                            tran.Rollback();
                            return ScanResult.Fail(
                                $"Vượt sức chứa Slot ({qtyHienTai + qr.Quantity}/{capacity}). Chọn Slot khác.");
                        }
                    }

                    bool daTonTai = _stockTpRepo.ExistsStockTp(conn, tran, lotNo);

                    var nhapItem = new NhapKhoItem
                    {
                        Lot = lotNo,
                        Part = qr.ItemCode,
                        Name = matchedPhieu?.TenSP ?? qr.ItemCode,
                        NgaySX = matchedPhieu?.NgaySX ?? qr.ImportDate,
                        SlSanXuat = matchedPhieu?.SlSanXuat ?? qr.Quantity,
                        SlNhap = qr.Quantity
                    };

                    // ✅ THÊM: tính tổng SL đã nhập LUỸ KẾ sau lần nhập này, để quyết định
                    // Status = 1 (kết thúc) hay 0 (còn dở), giống logic gốc:
                    // if (SLSENHAP + SLDN == SLSX || SLSENHAP == SLDN) Status = 1;
                    int slDaNhapTruoc = daTonTai ? _stockTpRepo.GetSlDaNhap(conn, tran, lotNo) : 0;
                    int tongSlSauKhiNhap = slDaNhapTruoc + qr.Quantity;
                    int slSanXuatThuc = matchedPhieu?.SlSanXuat ?? nhapItem.SlSanXuat;

                    // Status = 1 khi nhập đủ hoặc vượt SL sản xuất — khớp logic gốc
                    int status = (tongSlSauKhiNhap >= slSanXuatThuc && slSanXuatThuc > 0) ? 1 : 0;

                    if (daTonTai)
                        _stockTpRepo.UpdateStockTp(conn, tran, lotNo, qr.Quantity, status);
                    else
                        _stockTpRepo.InsertStockTp(conn, tran, nhapItem, status);

                    string maPhieuMoi = PhieuNoHelper.NewMaPhieuNhap(lotNo);
                    _phieuRepo.InsertPhieuMoi(conn, tran,
                        slotId: slotId, itemCode: qr.ItemCode, lotNo: lotNo,
                        quantity: qr.Quantity, temCode: qr.MaPhieu, qrData: qr.RawQr,
                        importDate: qr.ImportDate ?? DateTime.Now, ngaySX: qr.NgaySX,
                        soPhieuTong: qr.SoPhieuTong, maPhieuMoi: maPhieuMoi,
                        parentSoPhieu: null, status: PhieuStatus.Active);

                    _sql.ExecuteNonQuery(conn, tran, @"
                UPDATE Slot SET
                    Quantity   = ISNULL(Quantity,0) + @Sl,
                    ItemCode   = @ItemCode,
                    ImportDate = @ImportDate,
                    IsOccupied = 1
                WHERE SlotId = @SlotId",
                        new SqlParameter("@Sl", qr.Quantity),
                        new SqlParameter("@ItemCode", (object)qr.ItemCode ?? DBNull.Value),
                        new SqlParameter("@ImportDate", (object)qr.ImportDate ?? DateTime.Now),
                        new SqlParameter("@SlotId", slotId));

                    _stockTpRepo.InsertCaseHistory(conn, tran, caseNo);

                    tran.Commit();

                    // ✅ Trả thêm cờ để UI biết có nên hiển thị cảnh báo "đã kết thúc LOT" không
                    return new ScanResult
                    {
                        IsOK = true,
                        Message = status == 1
                            ? $"Đã nhập LOT {lotNo} (SL: {qr.Quantity}) — ĐỦ SỐ LƯỢNG, LOT đã tự động KẾT THÚC."
                            : $"Đã nhập LOT {lotNo} (SL: {qr.Quantity}) vào {wh}/{rack}/Slot {slotNumber}."
                    };
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return ScanResult.Fail("Lỗi nhập kho: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Đối chiếu: tổng SlotLot Active của 1 LOT phải khớp STOCKTP.SLCONLAI.
        /// Dùng cho màn hình kiểm tra dữ liệu / báo cáo lệch tồn.
        /// </summary>
        public bool KiemTraKhopTonKho(string lotNo, out int slActive, out int slConLaiStockTp)
        {
            slActive = _phieuRepo.GetTongSlActiveTheoLot(lotNo);
            slConLaiStockTp = _stockTpRepo.GetSlConLai(lotNo);
            return slActive == slConLaiStockTp;
        }
    }
}
