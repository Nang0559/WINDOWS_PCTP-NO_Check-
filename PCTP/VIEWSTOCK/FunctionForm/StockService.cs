using PCTP.ClassSQL;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.FunctionForm
{
    /// <summary>
    /// Tầng nghiệp vụ (business layer) cho quản lý kho, tổng hợp từ các helper hiện có:
    ///   - SlotHelper      : CRUD Slot / SlotLot / lịch sử
    ///   - CheckInfor      : Warehouse / InspectionConfig / tìm slot trống
    ///   - LotNoHelper     : xử lý LotInfo (split, merge, print data...)
    ///   - QRCodeParser/Builder : parse & build chuỗi QR
    ///
    /// StockService KHÔNG dùng Dependency Injection — new trực tiếp các helper,
    /// giữ đúng phong cách code hiện tại của dự án.
    ///
    /// GHI CHÚ / GIẢ ĐỊNH cần rà soát lại với schema DB thực tế:
    ///   - Bảng Rack được giả định có cột RowCount, ColumnCount (theo model Rack.cs / RackDefinition.cs)
    ///     nhưng các câu query cũ trong CheckInfor/SlotHelper chưa từng SELECT các cột này.
    ///   - Bảng Slot được giả định có cột RowIndex, ColumnIndex (theo model Slot.cs) — nếu DB
    ///     chưa có, cần thêm cột hoặc bỏ 2 field này khi build SlotRenderInfo.
    ///   - ScanResult.Pending (DocQRCode) không được StockService gán giá trị vì chưa có
    ///     định nghĩa entity DocQRCode trong phạm vi được cung cấp; UI/service gọi vào QR tổng
    ///     (tem tổng chờ tách) cần tự bổ sung logic gán Pending nếu cần.
    /// </summary>
    public class StockService
    {
        private readonly SlotHelper _slotHelper;
        private readonly CheckInfor _checkInfor;
        private readonly SQLPROVIDER _sql;

        public StockService()
        {
            _slotHelper = new SlotHelper();
            _checkInfor = new CheckInfor();
            _sql = new SQLPROVIDER();
        }

        // ============================================================
        // 1. NHẬP KHO
        // ============================================================
        #region Nhập kho

        /// <summary>
        /// Lấy danh sách slot còn chỗ (ưu tiên slot đã chứa cùng ItemCode) hoặc slot trống,
        /// phù hợp để nhập ItemCode với số lượng cần nhập.
        /// Định dạng: "WH : {wh} - Rack : {rack} - Slot : {slot} - Capacity : {cap}[ - TemCode: .. - Qty: ..]"
        /// </summary>
        public List<string> GetAvailableSlotsForImport(string itemCode, int soLuongNhap)
            => _checkInfor.GetEmptySlots(warehouseCode: null, itemCode: itemCode, soLuongNhap: soLuongNhap);

        /// <summary>Lấy cấu hình kiểm tra tem theo ItemCode (null = không cần kiểm tra riêng).</summary>
        public InspectionConfig GetInspectionConfig(string itemCode)
            => _checkInfor.GetInspectionConfig(itemCode);

        /// <summary>Parse QR text -> QRCodeInfo. Ném FormatException nếu QR không hợp lệ.</summary>
        public QRCodeInfo ParseQr(string qrText) => QRCodeParser.ParseQRCode(qrText);

        /// <summary>
        /// Kiểm tra tem quét được so với cấu hình InspectionConfig (nếu có) và so với ItemCode/LotNo
        /// mong đợi (ví dụ ItemCode/LotNo của đơn hàng đang xử lý). Không throw — trả ScanResult
        /// để UI tự quyết định báo lỗi hay hỏi xác nhận khi số lượng không khớp.
        /// </summary>
        public ScanResult ValidateScan(
            QRCodeInfo qr,
            InspectionConfig config,
            string expectedItemCode = null,
            string expectedLotNo = null)
        {
            if (qr == null)
                return new ScanResult { IsOK = false, Message = "Không đọc được dữ liệu QR." };

            if ((config?.CheckItemCode ?? true) &&
                !string.IsNullOrWhiteSpace(expectedItemCode) &&
                !string.Equals(qr.ItemCode, expectedItemCode, StringComparison.OrdinalIgnoreCase))
            {
                return new ScanResult
                {
                    IsOK = false,
                    Message = $"Sai mã hàng! QR: {qr.ItemCode} - Yêu cầu: {expectedItemCode}"
                };
            }

            if ((config?.CheckLotNo ?? true) && !string.IsNullOrWhiteSpace(expectedLotNo))
            {
                string normalizedQr = LotNoHelper.NormalizeLot(qr.RawLotNo ?? qr.LotNo);
                string normalizedExpected = LotNoHelper.NormalizeLot(expectedLotNo);

                if (!string.Equals(normalizedQr, normalizedExpected, StringComparison.OrdinalIgnoreCase))
                {
                    return new ScanResult
                    {
                        IsOK = false,
                        Message = $"Sai Lot No! QR: {normalizedQr} - Yêu cầu: {normalizedExpected}"
                    };
                }
            }

            if ((config?.CheckNSX ?? true) && qr.ImportDate == null)
            {
                return new ScanResult
                {
                    IsOK = false,
                    Message = $"Ngày sản xuất không hợp lệ: '{qr.NgaySX}'"
                };
            }

            bool slKhongKhop = config != null && config.DefaultQty > 0 && qr.Quantity != config.DefaultQty;

            return new ScanResult
            {
                IsOK = true,
                IsSlKhongKhop = slKhongKhop,
                Message = slKhongKhop
                    ? $"Số lượng quét ({qr.Quantity}) khác số lượng chuẩn ({config.DefaultQty}). Vui lòng xác nhận."
                    : "OK"
            };
        }

        /// <summary>
        /// Nhập 1 tem (QR, dạng text) vào slot đã chọn (chuỗi dạng
        /// "WH : .. - Rack : .. - Slot : .. - Capacity : .."). Parse QR rồi gọi overload chính.
        /// </summary>
        public ScanResult ImportLotToSlot(string qrText, string selectedSlotText)
        {
            QRCodeInfo qr;
            try
            {
                qr = QRCodeParser.ParseQRCode(qrText);
            }
            catch (FormatException ex)
            {
                return new ScanResult { IsOK = false, Message = ex.Message };
            }

            return ImportLotToSlot(qr, selectedSlotText);
        }

        /// <summary>
        /// Nhập 1 tem (QR đã parse sẵn) vào slot đã chọn. Dùng cho trường hợp form đã parse QR từ
        /// trước (ví dụ để hiển thị thông tin / chạy FormInspection) và không muốn parse lại.
        ///
        /// Hành vi: NẾU slot đã có Lot cùng LotNo -> cộng dồn số lượng (giống ImportToDataSlot cũ),
        /// nếu chưa có -> thêm mới (giống ImportToEmptySlot cũ). Không cần phân biệt slot trống hay
        /// đã có hàng — GetSlotLots trả về list rỗng cho slot trống nên MergeLotInfos tự xử lý đúng
        /// cho cả 2 trường hợp.
        /// </summary>
        public ScanResult ImportLotToSlot(QRCodeInfo qr, string selectedSlotText)
        {
            if (qr == null)
                return new ScanResult { IsOK = false, Message = "Không có dữ liệu QR." };

            SlotHelper.ParseSlotString(selectedSlotText, out string wh, out string rack, out int slotNo, out int capacity);
            int slotId = _slotHelper.GetSlotID(wh, rack, slotNo);

            if (slotId <= 0)
                return new ScanResult { IsOK = false, Message = "Không tìm thấy Slot." };

            if (capacity <= 0)
                capacity = _slotHelper.GetSlotCapacityById(slotId);

            var newLot = LotNoHelper.CreateLot(qr);
            var existingLots = _slotHelper.GetSlotLots(slotId);
            var mergedLots = LotNoHelper.MergeLotInfos(existingLots, new List<LotInfo> { newLot });
            int finalQty = LotNoHelper.GetTotalQuantity(mergedLots);

            if (capacity > 0 && finalQty > capacity)
            {
                return new ScanResult
                {
                    IsOK = false,
                    Message = $"Tổng số lượng ({finalQty}) vượt quá sức chứa Slot ({capacity})."
                };
            }

            _slotHelper.SaveSlotLots(slotId, mergedLots, updateSlot: true);
            _slotHelper.UpdateSlotInfo(slotId, qr.ItemCode, qr.ImportDate ?? DateTime.Now, finalQty);

            SlotHelper.SaveHistory("IMPORT", qr.ItemCode, newLot, fromSlotId: null, toSlotId: slotId);

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã nhập Lot {newLot.LotNo} (SL: {newLot.Quantity}) vào {wh}/{rack}/Slot {slotNo}. Tổng slot: {finalQty}."
            };
        }

        #endregion

        // ============================================================
        // 2. XUẤT KHO
        // ============================================================
        #region Xuất kho

        /// <summary>
        /// Xuất một số lượng hàng ra khỏi slot: tách lot theo thứ tự các lot hiện có trong slot
        /// (LotNoHelper.SubtractLots), cập nhật lại Slot với các lot còn lại, ghi lịch sử xuất
        /// cho từng lot đã xuất.
        /// </summary>
        public LotSplitResult ExportFromSlot(int slotId, int exportQty, string itemCode = null)
        {
            var currentLots = _slotHelper.GetSlotLots(slotId);
            var result = LotNoHelper.SubtractLots(currentLots, exportQty);

            _slotHelper.SaveSlotLots(slotId, result.RemainingLots, updateSlot: true);

            // SaveSlotLots(.., updateSlot: true) đã tự gọi UpdateSlotQuantity ở DB
            // (tự tính lại Quantity/ItemCode/ImportDate/IsOccupied từ SlotLot còn lại,
            // kể cả về 0/null khi hết hàng) — không cần gọi UpdateSlotInfo thêm ở đây.

            foreach (var exportedLot in result.ExportLots)
            {
                SlotHelper.SaveHistory(
                    "EXPORT",
                    itemCode ?? exportedLot.QRInfo?.ItemCode,
                    exportedLot,
                    fromSlotId: slotId,
                    toSlotId: null);
            }

            return result;
        }

        /// <summary>Xuất theo chuỗi slot dạng "WH : .. - Rack : .. - Slot : ..".</summary>
        public LotSplitResult ExportFromSlot(string selectedSlotText, int exportQty, string itemCode = null)
        {
            int slotId = _slotHelper.GetSlotIDFromString(selectedSlotText);
            if (slotId <= 0)
                throw new ArgumentException($"Không xác định được Slot từ chuỗi: {selectedSlotText}");

            return ExportFromSlot(slotId, exportQty, itemCode);
        }

        /// <summary>Kết quả của thao tác xuất + chuyển phần dư sang slot khác.</summary>
        public class ExportMoveResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public LotSplitResult Split { get; set; }
        }

        /// <summary>
        /// Xuất <paramref name="exportQty"/> từ slot nguồn, sau đó chuyển toàn bộ phần còn lại
        /// (nếu có) sang slot đích (chọn từ danh sách dạng "WH : .. - Rack : .. - Slot : .."),
        /// rồi xoá sạch slot nguồn. Dùng khi muốn dồn/di chuyển hết 1 slot thay vì để lại phần dư
        /// tại chỗ. Kiểm tra sức chứa slot đích trước khi lưu — không đổi gì nếu vượt sức chứa.
        /// </summary>
        public ExportMoveResult ExportAndMoveRemaining(
            int fromSlotId,
            string toSlotSelectedText,
            int exportQty,
            string itemCode = null)
        {
            SlotHelper.ParseSlotString(toSlotSelectedText, out string whDest, out string rackDest, out int slotNumber, out int capacity);
            int toSlotId = _slotHelper.GetSlotID(whDest, rackDest, slotNumber);

            if (toSlotId <= 0)
                return new ExportMoveResult { Success = false, Message = "Không tìm thấy Slot đích." };

            if (capacity <= 0)
                capacity = _slotHelper.GetSlotCapacityById(toSlotId);

            var sourceLots = _slotHelper.GetSlotLots(fromSlotId);
            var destLots = _slotHelper.GetSlotLots(toSlotId);

            var split = LotNoHelper.SubtractLots(sourceLots, exportQty);
            var mergedLots = LotNoHelper.MergeLotInfos(destLots, split.RemainingLots);
            int finalQty = LotNoHelper.GetTotalQuantity(mergedLots);

            if (capacity > 0 && finalQty > capacity)
            {
                return new ExportMoveResult
                {
                    Success = false,
                    Message = $"Không thể chuyển. Tổng số lượng ({finalQty}) vượt quá sức chứa ({capacity})."
                };
            }

            // Lưu slot đích trước, sau đó mới xoá slot nguồn — tránh mất dữ liệu nếu bước lưu lỗi.
            _slotHelper.SaveSlotLots(toSlotId, mergedLots, updateSlot: true);
            _slotHelper.ClearSlot(fromSlotId);

            foreach (var lot in split.ExportLots)
                SlotHelper.SaveHistory("EXPORT", itemCode ?? lot.QRInfo?.ItemCode, lot, fromSlotId, toSlotId: null);

            foreach (var lot in split.RemainingLots)
                SlotHelper.SaveHistory("MOVE", itemCode ?? lot.QRInfo?.ItemCode, lot, fromSlotId, toSlotId);

            return new ExportMoveResult { Success = true, Split = split };
        }

        /// <summary>
        /// Chuyển 1 Lot cụ thể từ slot nguồn sang slot đích (gộp kệ / sắp xếp lại kho).
        /// Merge với lot cùng LotNo nếu slot đích đã có sẵn.
        /// </summary>
        public void MoveLot(int fromSlotId, int toSlotId, string lotNo)
        {
            var sourceLots = _slotHelper.GetSlotLots(fromSlotId);
            var lot = LotNoHelper.FindLot(sourceLots, lotNo);

            if (lot == null)
                throw new InvalidOperationException($"Không tìm thấy Lot {lotNo} trong slot nguồn.");

            var remaining = sourceLots.Where(x => x.LotNo != lotNo).ToList();
            _slotHelper.SaveSlotLots(fromSlotId, remaining, updateSlot: true);

            var destLots = _slotHelper.GetSlotLots(toSlotId);
            var merged = LotNoHelper.MergeLotInfos(destLots, new List<LotInfo> { lot });
            _slotHelper.SaveSlotLots(toSlotId, merged, updateSlot: true);

            SlotHelper.SaveHistory("MOVE", lot.QRInfo?.ItemCode, lot, fromSlotId, toSlotId);
        }

        /// <summary>
        /// Đồng bộ lại object Slot đang hiển thị trên form (in-memory) sau khi xuất tại chỗ,
        /// dựa theo phần Lot còn lại trả về từ ExportFromSlot.
        /// </summary>
        public void SyncSlotFromSplitResult(Slot slot, LotSplitResult result)
        {
            if (slot == null || result == null) return;

            slot.Lots = result.RemainingLots;
            slot.Quantity = LotNoHelper.GetTotalQuantity(result.RemainingLots);
            slot.IsOccupied = slot.Quantity > 0;

            if (result.RemainingLots.Any())
            {
                slot.ItemCode = result.RemainingLots.First().QRInfo?.ItemCode;
                slot.ImportDate = result.RemainingLots.Max(x => x.QRInfo?.ImportDate);
            }
            else
            {
                slot.ItemCode = null;
                slot.ImportDate = null;
            }
        }

        #endregion

        // ============================================================
        // 3. QUẢN LÝ SLOT
        // ============================================================
        #region Quản lý Slot

        public List<LotInfo> GetSlotLots(int slotId) => _slotHelper.GetSlotLots(slotId);

        public bool ExistsLot(int slotId, string lotNo) => _slotHelper.ExistsLot(slotId, lotNo);

        public int GetSlotID(string wh, string rack, int slotNumber) => _slotHelper.GetSlotID(wh, rack, slotNumber);

        public int GetSlotIDFromString(string slotText) => _slotHelper.GetSlotIDFromString(slotText);

        public int GetSlotCapacityById(int slotId) => _slotHelper.GetSlotCapacityById(slotId);

        /// <summary>Xoá sạch slot trong DB (dùng khi huỷ thao tác hoặc dọn kho thủ công).</summary>
        public void ClearSlot(int slotId) => _slotHelper.ClearSlot(slotId);

        /// <summary>Sao lưu trạng thái slot trong bộ nhớ trước khi thao tác thử (để undo nếu lỗi).</summary>
        public void BackupSlot(Slot slot, out Slot backup) => SlotHelper.BackupSlot(slot, out backup);

        /// <summary>Khôi phục slot trong bộ nhớ từ bản backup.</summary>
        public void RestoreSlot(Slot slot, Slot backup) => SlotHelper.RestoreSlot(slot, backup);

        /// <summary>Xoá trạng thái slot trong bộ nhớ (không đụng DB) — dùng khi preview trước khi Save.</summary>
        public void ClearSlotTemporarily(Slot slot) => SlotHelper.ClearSlotTemporarily(slot);

        public static void ParseSlotString(string text, out string wh, out string rack, out int slot, out int capacity)
            => SlotHelper.ParseSlotString(text, out wh, out rack, out slot, out capacity);

        #endregion

        // ============================================================
        // 4. IN TEM / PHIẾU
        // ============================================================
        #region In tem / phiếu

        /// <summary>Gộp danh sách Lot thành dữ liệu in tem/phiếu (tổng SL, chuỗi LotNo/TemCode, QR gộp...).</summary>
        public PrintLotResult CreatePrintData(List<LotInfo> lots) => LotNoHelper.CreatePrintData(lots);

        /// <summary>Tạo dữ liệu in tem cho toàn bộ Lot hiện có trong 1 slot.</summary>
        public PrintLotResult CreatePrintDataForSlot(int slotId)
        {
            var lots = _slotHelper.GetSlotLots(slotId);
            return LotNoHelper.CreatePrintData(lots);
        }

        /// <summary>Dựng model PXuatINModel để in phiếu xuất kho (dùng cho report Xuất kho).</summary>
        public PXuatINModel BuildXuatInModel(
            string loaiPhieu,
            string ca,
            string soThuTuXe,
            string tenSanPham,
            string maSanPham,
            LotInfo exportedLot,
            string nguoiThucHien,
            int soLuongTonSauXuat)
        {
            return new PXuatINModel
            {
                LoaiPhieu = loaiPhieu,
                Ca = ca,
                SoThuTuXe = soThuTuXe,
                TenSanPham = tenSanPham,
                MaSanPham = maSanPham,
                LotNo = exportedLot?.LotNo,
                SoLuong = exportedLot?.Quantity ?? 0,
                CheckTem = exportedLot?.TemCode,
                NguoiThucHien = nguoiThucHien,
                QrData = exportedLot?.RawQr,

                Ngay = DateTime.Now.ToString("dd/MM/yyyy"),
                Gio = DateTime.Now.ToString("HH:mm"),
                SoLuongXuat = exportedLot?.Quantity ?? 0,
                NguoiXuat = nguoiThucHien,
                SoLuongTon = soLuongTonSauXuat
            };
        }

        /// <summary>
        /// Dựng preview dữ liệu in phiếu xuất kho (KHÔNG lưu DB — chỉ tính toán để hiển thị report).
        /// Luôn lấy Lot mới nhất từ DB cho slot trước khi tách. Trả 1 dòng "PHIẾU XUẤT" và thêm
        /// 1 dòng "PHIẾU NHẬP LẠI KHO" nếu còn phần dư trong slot sau khi xuất.
        /// </summary>
        public List<PXuatINModel> BuildExportPreview(
            Slot slot,
            int exportQty,
            string productName,
            string nguoiThucHien = "")
        {
            if (slot == null)
                throw new ArgumentNullException(nameof(slot));

            var lots = _slotHelper.GetSlotLots(slot.SlotId);
            int tongSoLuong = LotNoHelper.GetTotalQuantity(lots);

            if (exportQty > tongSoLuong)
                throw new InvalidOperationException("Số lượng xuất lớn hơn tồn kho.");

            var split = LotNoHelper.SubtractLots(lots, exportQty);
            var exportPrint = LotNoHelper.CreatePrintData(split.ExportLots);
            var remainPrint = LotNoHelper.CreatePrintData(split.RemainingLots);

            // ✅ REFACTOR: build PXuatINModel qua PrintHelper.CreatePrintModel (nơi DUY NHẤT quyết
            // định cách map PrintLotResult -> PXuatINModel) thay vì tự "new PXuatINModel" ở đây —
            // sau này cần đổi chỉ số in (Ca, format ngày giờ...) chỉ sửa 1 chỗ trong PrintHelper.
            var dataSource = new List<PXuatINModel>
            {
                PrintHelper.CreatePrintModel(
                    printData: exportPrint,
                    loaiPhieu: "PHIẾU XUẤT",
                    productName: productName,
                    slotNumber: slot.SlotNumber,
                    soLuongXuat: exportPrint.Quantity,
                    soLuongTon: remainPrint.Quantity,
                    nguoiThucHien: nguoiThucHien)
            };

            if (remainPrint.Quantity > 0)
            {
                dataSource.Add(PrintHelper.CreatePrintModel(
                    printData: remainPrint,
                    loaiPhieu: "PHIẾU NHẬP LẠI KHO",
                    productName: productName,
                    slotNumber: slot.SlotNumber,
                    // Giữ đúng hành vi gốc: dòng "nhập lại kho" vẫn hiển thị SoLuongXuat = SL đã xuất
                    // (không phải SL nhập lại), để 2 dòng trên phiếu tham chiếu cùng 1 số xuất.
                    soLuongXuat: exportPrint.Quantity,
                    soLuongTon: remainPrint.Quantity,
                    nguoiThucHien: nguoiThucHien));
            }

            return dataSource;
        }

        /// <summary>Lấy tên sản phẩm theo ItemCode (wrapper của SQLPROVIDER.GetProductNameByCode có sẵn).</summary>
        public string GetProductNameByCode(string itemCode) => _sql.GetProductNameByCode(itemCode);

        #endregion

        // ============================================================
        // 5. RENDER RACK / WAREHOUSE CHO UI
        // ============================================================
        #region Render Rack / Warehouse

        public bool IsWarehouseExists(string warehouseName) => _checkInfor.IsWarehouseExists(warehouseName);

        /// <summary>
        /// Lấy toàn bộ Slot của 1 Rack (kèm thống kê EmptySlotCount, ItemSummary) để vẽ sơ đồ kho lên UI.
        /// Giả định bảng Rack có cột RowCount/ColumnCount và bảng Slot có cột RowIndex/ColumnIndex
        /// — cần kiểm tra lại với schema thực tế, chỉnh câu SELECT nếu tên cột khác.
        /// </summary>
        public RackRenderInfo GetRackRenderInfo(string warehouseName, string rackName)
        {
            string query = @"
                SELECT
                    r.RackId, r.RackName, r.RowCount, r.ColumnCount,
                    s.SlotId, s.SlotNumber, s.RowIndex, s.ColumnIndex,
                    s.IsOccupied, s.ItemCode, s.Quantity, s.Capacity, s.ImportDate
                FROM Rack r
                JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
                LEFT JOIN Slot s ON s.RackId = r.RackId
                WHERE w.Name = @WarehouseName AND r.RackName = @RackName
                ORDER BY s.RowIndex, s.ColumnIndex, s.SlotNumber";

            var parameters = new[]
            {
                new SqlParameter("@WarehouseName", warehouseName),
                new SqlParameter("@RackName", rackName)
            };

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb, query, parameters);

            var info = new RackRenderInfo
            {
                WarehouseName = warehouseName,
                RackName = rackName
            };

            foreach (DataRow row in dt.Rows)
            {
                if (info.RackId == 0 && row["RackId"] != DBNull.Value)
                    info.RackId = Convert.ToInt32(row["RackId"]);

                if (row["RowCount"] != DBNull.Value)
                    info.RowCount = Convert.ToInt32(row["RowCount"]);

                if (row["ColumnCount"] != DBNull.Value)
                    info.ColumnCount = Convert.ToInt32(row["ColumnCount"]);

                if (row["SlotId"] == DBNull.Value)
                    continue; // rack chưa có slot nào

                var slot = new Slot
                {
                    SlotId = Convert.ToInt32(row["SlotId"]),
                    whname = warehouseName,
                    RackName = rackName,
                    SlotNumber = Convert.ToInt32(row["SlotNumber"]),
                    RowIndex = row["RowIndex"] == DBNull.Value ? 0 : Convert.ToInt32(row["RowIndex"]),
                    ColumnIndex = row["ColumnIndex"] == DBNull.Value ? 0 : Convert.ToInt32(row["ColumnIndex"]),
                    IsOccupied = Convert.ToBoolean(row["IsOccupied"]),
                    ItemCode = row["ItemCode"] == DBNull.Value ? null : row["ItemCode"].ToString(),
                    Quantity = row["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(row["Quantity"]),
                    Capacity = row["Capacity"] == DBNull.Value ? 0 : Convert.ToInt32(row["Capacity"]),
                    ImportDate = row["ImportDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ImportDate"])
                };

                info.Slots.Add(new SlotRenderInfo
                {
                    Slot = slot,
                    RackName = rackName,
                    WarehouseName = warehouseName,
                    Row = slot.RowIndex,
                    Column = slot.ColumnIndex
                });

                info.SlotCount++;
                if (!slot.IsOccupied)
                    info.EmptySlotCount++;

                if (!string.IsNullOrWhiteSpace(slot.ItemCode))
                {
                    if (info.ItemSummary.TryGetValue(slot.ItemCode, out var summary))
                        info.ItemSummary[slot.ItemCode] = (summary.Count + 1, summary.TotalQty + slot.Quantity);
                    else
                        info.ItemSummary[slot.ItemCode] = (1, slot.Quantity);
                }
            }

            return info;
        }

        /// <summary>Lấy danh sách định nghĩa các Rack (không kèm chi tiết từng Slot) của 1 Warehouse.</summary>
        public List<RackDefinition> GetRackDefinitions(string warehouseName)
        {
            string query = @"
                SELECT r.RackId, r.RackName, r.RowCount, r.ColumnCount,
                       (SELECT COUNT(*) FROM Slot s WHERE s.RackId = r.RackId) AS SlotCount
                FROM Rack r
                JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
                WHERE w.Name = @WarehouseName
                ORDER BY r.RackName";

            var parameters = new[] { new SqlParameter("@WarehouseName", warehouseName) };
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb, query, parameters);

            var result = new List<RackDefinition>();
            foreach (DataRow row in dt.Rows)
            {
                result.Add(new RackDefinition
                {
                    WarehouseName = warehouseName,
                    RackName = row["RackName"].ToString(),
                    RackId = Convert.ToInt32(row["RackId"]),
                    RowCount = row["RowCount"] == DBNull.Value ? 0 : Convert.ToInt32(row["RowCount"]),
                    ColumnCount = row["ColumnCount"] == DBNull.Value ? 0 : Convert.ToInt32(row["ColumnCount"]),
                    SlotCount = Convert.ToInt32(row["SlotCount"])
                });
            }

            return result;
        }

        /// <summary>Lấy toàn bộ Warehouse kèm danh sách Rack (không kèm Slot chi tiết) — dùng cho combobox/tree.</summary>
        public List<Warehouse> GetAllWarehouses()
        {
            string query = @"
                SELECT w.Name AS WarehouseName, r.RackName
                FROM Warehouse w
                LEFT JOIN Rack r ON r.WarehouseId = w.WarehouseId
                ORDER BY w.Name, r.RackName";

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb, query, new SqlParameter[0]);

            var warehouses = new Dictionary<string, Warehouse>();

            foreach (DataRow row in dt.Rows)
            {
                string whName = row["WarehouseName"].ToString();

                if (!warehouses.TryGetValue(whName, out var wh))
                {
                    wh = new Warehouse { Name = whName, Racks = new List<Rack>() };
                    warehouses[whName] = wh;
                }

                if (row["RackName"] != DBNull.Value)
                    wh.Racks.Add(new Rack { Name = row["RackName"].ToString(), Slots = new List<Slot>() });
            }

            return warehouses.Values.ToList();
        }

        #endregion
        public string GetOrCreateBulkImportSlotText()
        {
            string query = @"
        SELECT s.SlotNumber, s.Capacity
        FROM Slot s
        JOIN Rack r ON r.RackId = s.RackId
        JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
        WHERE w.Name = @wh AND r.RackName = @rack";

            var dt = _sql.LoadData1(_sql.B7R2_FCCdbb, query,
                new SqlParameter("@wh", BulkImportConfig.WarehouseName),
                new SqlParameter("@rack", BulkImportConfig.RackName));

            if (dt.Rows.Count > 0)
            {
                int slotNo = Convert.ToInt32(dt.Rows[0]["SlotNumber"]);
                int cap = Convert.ToInt32(dt.Rows[0]["Capacity"]);
                return $"WH : {BulkImportConfig.WarehouseName} - Rack : {BulkImportConfig.RackName} - Slot : {slotNo} - Capacity : {cap}";
            }

            int whId = Convert.ToInt32(_sql.ExecuteScalar(_sql.B7R2_FCCdbb,
                "INSERT INTO Warehouse (Name) OUTPUT INSERTED.WarehouseId VALUES (@n)",
                new[] { new SqlParameter("@n", BulkImportConfig.WarehouseName) }));

            int rackId = Convert.ToInt32(_sql.ExecuteScalar(_sql.B7R2_FCCdbb,
                "INSERT INTO Rack (WarehouseId, RackName) OUTPUT INSERTED.RackId VALUES (@w,@r)",
                new[] { new SqlParameter("@w", whId), new SqlParameter("@r", BulkImportConfig.RackName) }));

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdbb,
                "INSERT INTO Slot (RackId, SlotNumber, IsOccupied, Capacity, Quantity) VALUES (@rk, 1, 0, @cap, 0)",
                new SqlParameter("@rk", rackId),
                new SqlParameter("@cap", BulkImportConfig.Capacity));

            return $"WH : {BulkImportConfig.WarehouseName} - Rack : {BulkImportConfig.RackName} - Slot : 1 - Capacity : {BulkImportConfig.Capacity}";
        }
        /// <summary>
        /// Nhập trực tiếp 1 LOT (không qua QR) vào slot — dùng cho khách trả hàng,
        /// nơi ta chỉ có LOT_GOC + tổng SL đã quét, không có QR gốc từng thùng.
        /// </summary>
        public ScanResult ImportLotDirectly(string selectedSlotText, string lotNo,
            string itemCode, int quantity)
        {
            SlotHelper.ParseSlotString(selectedSlotText, out string wh, out string rack,
                out int slotNo, out int capacity);
            int slotId = _slotHelper.GetSlotID(wh, rack, slotNo);
            if (slotId <= 0)
                return new ScanResult { IsOK = false, Message = "Không tìm thấy Slot." };

            var newLot = new LotInfo { LotNo = lotNo, Quantity = quantity, TemCode = "" };
            var existingLots = _slotHelper.GetSlotLots(slotId);
            var mergedLots = LotNoHelper.MergeLotInfos(existingLots, new List<LotInfo> { newLot });
            int finalQty = LotNoHelper.GetTotalQuantity(mergedLots);

            if (capacity > 0 && finalQty > capacity)
                return new ScanResult { IsOK = false, Message = $"Vượt sức chứa Slot ({finalQty}/{capacity})." };

            _slotHelper.SaveSlotLots(slotId, mergedLots, updateSlot: true);
            _slotHelper.UpdateSlotInfo(slotId, itemCode, DateTime.Now, finalQty);
            SlotHelper.SaveHistory("CUSTOMER_RETURN", itemCode, newLot, fromSlotId: null, toSlotId: slotId);

            return new ScanResult { IsOK = true, Message = $"Đã nhập LOT {lotNo} (SL: {quantity}) vào {wh}/{rack}/Slot {slotNo}." };
        }

        /// <summary>
        /// Tạo/lấy Slot ảo dùng chung cho 1 mục đích đặc biệt (VD: "KHACH_TRA_NG"),
        /// tương tự GetOrCreateBulkImportSlotText nhưng đặt tên kho/rack tùy biến.
        /// </summary>
        public string GetOrCreateVirtualSlotText(string warehouseName, string rackName, int capacity = 999999999)
        {
            string query = @"
        SELECT s.SlotNumber, s.Capacity
        FROM Slot s
        JOIN Rack r ON r.RackId = s.RackId
        JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
        WHERE w.Name = @wh AND r.RackName = @rack";

            var dt = _sql.LoadData1(_sql.B7R2_FCCdbb, query,
                new SqlParameter("@wh", warehouseName), new SqlParameter("@rack", rackName));

            if (dt.Rows.Count > 0)
            {
                int slotNo = Convert.ToInt32(dt.Rows[0]["SlotNumber"]);
                int cap = Convert.ToInt32(dt.Rows[0]["Capacity"]);
                return $"WH : {warehouseName} - Rack : {rackName} - Slot : {slotNo} - Capacity : {cap}";
            }

            int whId = Convert.ToInt32(_sql.ExecuteScalar(_sql.B7R2_FCCdbb,
                "INSERT INTO Warehouse (Name) OUTPUT INSERTED.WarehouseId VALUES (@n)",
                new[] { new SqlParameter("@n", warehouseName) }));

            int rackId = Convert.ToInt32(_sql.ExecuteScalar(_sql.B7R2_FCCdbb,
                "INSERT INTO Rack (WarehouseId, RackName) OUTPUT INSERTED.RackId VALUES (@w,@r)",
                new[] { new SqlParameter("@w", whId), new SqlParameter("@r", rackName) }));

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdbb,
                "INSERT INTO Slot (RackId, SlotNumber, IsOccupied, Capacity, Quantity) VALUES (@rk, 1, 0, @cap, 0)",
                new SqlParameter("@rk", rackId), new SqlParameter("@cap", capacity));

            return $"WH : {warehouseName} - Rack : {rackName} - Slot : 1 - Capacity : {capacity}";
        }
    }
}