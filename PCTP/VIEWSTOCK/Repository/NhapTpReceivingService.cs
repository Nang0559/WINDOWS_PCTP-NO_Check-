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
        public ScanResult NhapTpVaoSlot(QRCodeInfo qr, string selectedSlotText)
        {
            var check = KiemTraTruocKhiNhap(qr);
            if (!check.IsOK) return check;

            string lotNo = LotNoHelper.NormalizeLot(qr.RawLotNo ?? qr.LotNo); // hoặc LotNoHelper sau khi gộp

            // ── Build CASE_NO giống logic gốc NHAP_TP: LotNoSL + SoPhieuTong (hoặc "4" nếu NG) ──
            string caseNo = !string.IsNullOrEmpty(qr.SoPhieuTong)
                ? qr.RawLotNo + qr.SoPhieuTong
                : qr.RawLotNo + "4";

            SlotHelper.ParseSlotString(selectedSlotText,
                out string wh, out string rack, out int slotNumber, out int capacity);

            var slotHelper = new SlotHelper();
            int slotId = slotHelper.GetSlotID(wh, rack, slotNumber);
            if (slotId <= 0)
                return ScanResult.Fail("Không tìm thấy Slot đích.");

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    // ── Check trùng case NGAY TRONG TRANSACTION (tránh race condition) ──
                    if (_stockTpRepo.ExistsCaseHistory(conn, tran, caseNo))
                    {
                        tran.Rollback();
                        return ScanResult.Trung($"Case [{caseNo}] đã được nhập kho trước đó!");
                    }

                    // ── Bước 1: STOCKTP — nguồn sự thật duy nhất về tổng tồn ────
                    bool daTonTai = _stockTpRepo.ExistsStockTp(conn, tran, lotNo);

                    var nhapItem = new NhapKhoItem
                    {
                        Lot = lotNo,
                        Part = qr.ItemCode,
                        Name = qr.ItemCode,
                        NgaySX = qr.ImportDate,
                        SlSanXuat = qr.Quantity,
                        SlNhap = qr.Quantity
                    };

                    if (daTonTai)
                        _stockTpRepo.UpdateStockTp(conn, tran, lotNo, qr.Quantity, 0);
                    else
                        _stockTpRepo.InsertStockTp(conn, tran, nhapItem, 0);

                    // ── Bước 2: Tạo phiếu kho mới (Active) — giữ nguyên như cũ ──
                    string maPhieuMoi = PhieuNoHelper.NewMaPhieuNhap(lotNo);
                    _phieuRepo.InsertPhieuMoi(conn, tran,
                        slotId: slotId, itemCode: qr.ItemCode, lotNo: lotNo,
                        quantity: qr.Quantity, temCode: qr.MaPhieu, qrData: qr.RawQr,
                        importDate: qr.ImportDate ?? DateTime.Now, ngaySX: qr.NgaySX,
                        soPhieuTong: qr.SoPhieuTong, maPhieuMoi: maPhieuMoi,
                        parentSoPhieu: null, status: PhieuStatus.Active);

                    // ── Bước 3: Cộng dồn tổng hợp lên Slot — giữ nguyên ──
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

                    // ── Bước 4: Ghi lịch sử case — chống bắn lại QR này ──────────
                    _stockTpRepo.InsertCaseHistory(conn, tran, caseNo);

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return ScanResult.Fail("Lỗi nhập kho: " + ex.Message);
                }
            }

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã nhập LOT {lotNo} (SL: {qr.Quantity}) vào {wh}/{rack}/Slot {slotNumber}."
            };
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
