using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// FACADE — KHÔNG chứa bất kỳ logic nghiệp vụ nào của riêng nó.
    ///
    /// LÝ DO TỒN TẠI: các Form/Presenter cũ (composition root cũ, vd. HVN_PGH hiện
    /// tại) inject "1 IPhieuRepository" gộp duy nhất qua constructor. Thay vì viết
    /// lại 6 interface đó (như bản cũ đã làm — gây lệch hành vi với 6 repo con),
    /// class này CHỈ dựng 6 repo con rồi ủy quyền (delegate) MỌI method — mỗi
    /// method ở đây đúng 1 dòng gọi thẳng sang repo con tương ứng.
    ///
    /// HỆ QUẢ: sửa 1 bug ở `PhieuKhoRepository.CapNhapKho` → `PhieuRepository`
    /// (facade) và mọi form dùng `IPhieuKhoRepository` trực tiếp đều thấy bản vá
    /// NGAY LẬP TỨC, vì cả 2 đường cùng chạy chung 1 implementation vật lý.
    ///
    /// ⚠️ QUY TẮC BẮT BUỘC: TUYỆT ĐỐI không thêm dòng SQL/nghiệp vụ nào trực tiếp
    /// vào class này (trừ 3 method generic pass-through hạ tầng ở cuối file — xem
    /// ghi chú tại đó). Nếu thấy mình sắp gõ SqlParameter hay ExecuteScalar ở đây,
    /// dừng lại — method đó thuộc về 1 trong 6 repo con, không phải Facade.
    /// </summary>
    public sealed class PhieuRepository :IPhieuRepository
    {
        private readonly IPhieuValidationRepository _validation;
        private readonly IPhieuTmpRepository _tmp;
        private readonly IPhieuLotRepository _lot;
        private readonly IPhieuKhoRepository _kho;
        private readonly IPhieuLuuTruRepository _luuTru;
        private readonly IPhieuGiaoDBRepository _giaoDB;

        // Giữ CHỈ để phục vụ 3 method generic pass-through hạ tầng ở cuối file —
        // đây là wrapper thuần kỹ thuật (exec SP theo tên), không phải nghiệp vụ,
        // nên không thuộc về bất kỳ repo con nào trong 6 repo trên.
        private readonly PhieuSqlExecutor _db;

        /// <summary>
        /// Constructor tiện dụng — tự dựng cả 6 repo con từ các dependency gốc.
        /// Dùng khi composition root (HVN_PGH cũ) chưa sẵn sàng inject riêng từng repo.
        /// </summary>
        public PhieuRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            CustomerConfig cfg,
            IBulkStockSlotRepository bulkStockSlotRepo,
            IStockHistoryRepository historyRepo,
            IHangChoGiaoRepository hangChoGiaoRepo = null,
            IIFSRepository ifsRepo = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));

            _validation = new PhieuValidationRepository(db, uow);   // ← dựng TRƯỚC
            _tmp = new PhieuTmpRepository(db,uow);
            _lot = new PhieuLotRepository(db, uow);
            _giaoDB = new PhieuGiaoDBRepository(db,uow);
            _luuTru = new PhieuLuuTruRepository(db,uow);
            _kho = new PhieuKhoRepository(
                              db, uow, bulkStockSlotRepo, historyRepo,
                              _validation,   // ← TÁI DÙNG đúng instance vừa dựng, không tạo mới
                              cfg, hangChoGiaoRepo);
        }
        #region IPhieuValidationRepository — 100% delegate, KHÔNG chứa logic

        public int CountDocQRCode(string docQRTable)
            => _validation.CountDocQRCode(docQRTable);

        public bool CheckCoMaNG(string tenBan)
            => _validation.CheckCoMaNG(tenBan);

        public bool KiemTraMaTrongPhieu(string maHang, string tenBan)
            => _validation.KiemTraMaTrongPhieu(maHang, tenBan);

        public DataTable GetDanhSachTrungMaSl(string maHang, int sl, PhieuTableSet tables)
            => _validation.GetDanhSachTrungMaSl(maHang, sl, tables);

        public DataTable GetDanhSachTrungMaSl(string maHang, int sl, string tenBan, string docQRTable)
            => _validation.GetDanhSachTrungMaSl(maHang, sl, tenBan, docQRTable);

        public int CountTrungMaSl(string maHang, int sl, PhieuTableSet tables)
            => _validation.CountTrungMaSl(maHang, sl, tables);

        public int CountTrungMaSl(string maHang, int sl, string tenBan, string docQRTable)
            => _validation.CountTrungMaSl(maHang, sl, tenBan, docQRTable);

        public DataTable GetDonHangChuaLot(PhieuTableSet tables)
            => _validation.GetDonHangChuaLot(tables);

        public DataTable GetDonHangChuaLot(string tenBan, string docQRTable)
            => _validation.GetDonHangChuaLot(tenBan, docQRTable);

        public List<FifoViolation> CheckFifoViolations(string tenBangTmp)
    => _validation.CheckFifoViolations(tenBangTmp);
        public DataTable SoSanhLechIFS(DataTable donHang, DataTable ifsTable)
        => _validation.SoSanhLechIFS(donHang, ifsTable);
        #endregion
        #region Phieu Giao DB
        // ── IPhieuGiaoDBRepository pass-through ─────────────────────────
        public DataTable GetDanhSachMaHang()
            => _giaoDB.GetDanhSachMaHang();

        public DataTable LoadTmpPhieuGiaoDB(string tenBan, DateTime ngayGiao, int addNm)
            => _giaoDB.LoadTmpPhieuGiaoDB(tenBan, ngayGiao, addNm);

        public DataTable BuildDonHangTuUpload()
            => _giaoDB.BuildDonHangTuUpload();

        public void LuuGiaoDB(DataTable donHang, string gioFccMoTa, int addNm,
            string tmpTable, string ifsTable, string nhaMayOverride = "")
            => _giaoDB.LuuGiaoDB(donHang, gioFccMoTa, addNm, tmpTable, ifsTable, nhaMayOverride);

        public int TaoPhieuVaChiTietGiaoDB(string ten, DateTime ngayLap, int nhaMay,
                                 string nhaMayName, string note, DataTable chiTiet)
            => _giaoDB.TaoPhieuVaChiTietGiaoDB(ten,  ngayLap, nhaMay,
                                 nhaMayName,  note,  chiTiet);

        #endregion



        #region IPhieuTmpRepository — 100% delegate, KHÔNG chứa logic

        public DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay, string gioFcc, int addNm, PhieuTableSet tables)
            => _tmp.LoadPhieuDocQR(ngayGiao, nhaMay, gioFcc, addNm, tables);

        public DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tmpTable, string ifsTable, string docQRTable)
            => _tmp.LoadPhieuDocQR(ngayGiao, nhaMay, gioFcc, addNm, tmpTable, ifsTable, docQRTable);

        public DataTable LuuVaLoad(PhieuTableSet tables, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm)
            => _tmp.LuuVaLoad(tables, tenSP, donHang, ngayGiao, nhaMay, gioFcc, addNm);

        public DataTable LuuVaLoad(string tenSPBang, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tenBan, string docQRTable, string ifsView = "")
            => _tmp.LuuVaLoad(tenSPBang, tenSP, donHang, ngayGiao, nhaMay, gioFcc, addNm, tenBan, docQRTable, ifsView);
        public void PushIfsSnapshot(string ifsTable, DataTable donHang)
    => _tmp.PushIfsSnapshot(ifsTable, donHang);
        public DataTable LoadTuTmpTable(string tmpTable)
            => _tmp.LoadTuTmpTable(tmpTable);

        public DataTable GetDonHangHienTai(string tenBan)
            => _tmp.GetDonHangHienTai(tenBan);

        public void XoaTmpPhieu(string tenBan)
            => _tmp.XoaTmpPhieu(tenBan);

        public void XoaDocQRCode(string docQRTable)
            => _tmp.XoaDocQRCode(docQRTable);

        public TrangThaiBan GetTrangThaiDangBan(PhieuTableSet tables)
            => _tmp.GetTrangThaiDangBan(tables);

        public TrangThaiBan GetTrangThaiDangBan(string tmpTable, string docQRTable)
            => _tmp.GetTrangThaiDangBan(tmpTable, docQRTable);

        public TrangThaiBan GetTrangThaiDangBanYMVN(PhieuTableSet tables)
            => _tmp.GetTrangThaiDangBanYMVN(tables);

        public TrangThaiBan GetTrangThaiDangBanYMVN(string tmpTable, string docQRTable)
            => _tmp.GetTrangThaiDangBanYMVN(tmpTable, docQRTable);

        public void EnsureTablesExist()
            => _tmp.EnsureTablesExist();

        public void InsertTmpRow(
            string tmpTable,
            string stt, string cua, string truyen, string maHang, string tenHang,
            string lot, string dv, int slXuat, string ngayGiao, string gear,
            string gioXuat, string poNo = "", string cusPoNo = "")
            => _tmp.InsertTmpRow(tmpTable, stt, cua, truyen, maHang, tenHang, lot, dv, slXuat, ngayGiao, gear, gioXuat, poNo, cusPoNo);

        #endregion

        #region IPhieuLotRepository — 100% delegate, KHÔNG chứa logic

        public string GetLotNo(string maHang, int stt, int dem, int slGiao, PhieuTableSet tables)
            => _lot.GetLotNo(maHang, stt, dem, slGiao, tables);

        public string GetLotNo(string maHang, int stt, int dem, int slGiao,
            string docQRTable = "DOCQRCODE", string tmpTable = "TMPPHIEUGIAOHANG")
            => _lot.GetLotNo(maHang, stt, dem, slGiao, docQRTable, tmpTable);

        public void CapNhapLotTmpPhieu(int stt, string lot, string tenBan)
            => _lot.CapNhapLotTmpPhieu(stt, lot, tenBan);

        public void LayLaiLotNo(int stt, PhieuTableSet tables)
            => _lot.LayLaiLotNo(stt, tables);

        public void LayLaiLotNo(int stt, string tenBan, string docQRTable)
            => _lot.LayLaiLotNo(stt, tenBan, docQRTable);

        public DataTable LoadGhepLot(
             string tenBan = "TMPPHIEUGIAOHANG", string ifsTable = "IFSPHIEUGIAOHANG")
             => _lot.LoadGhepLot(tenBan, ifsTable);

        public DataTable GetDanhSachLotTuKho(string maHang)
            => _lot.GetDanhSachLotTuKho(maHang);

        #endregion

        #region IPhieuKhoRepository — 100% delegate, KHÔNG chứa logic

        public int CapNhapKho(string gioGiaoFcc, string nhaMay, PhieuTableSet tables, out DataTable errors)
            => _kho.CapNhapKho(gioGiaoFcc, nhaMay, tables, out errors);

        public int CapNhapKho(string gioGiaoFcc, string nhaMay, string tmpTable, string docQRTable, out DataTable errors)
            => _kho.CapNhapKho(gioGiaoFcc, nhaMay, tmpTable, docQRTable, out errors);

        public int CapNhapKhoHTN(string nhaMay, PhieuTableSet tables, out DataTable errors)
            => _kho.CapNhapKhoHTN(nhaMay, tables, out errors);

        public int CapNhapKhoHTN(string nhaMay, string tmpTable, string docQRTable, out DataTable errors)
            => _kho.CapNhapKhoHTN(nhaMay, tmpTable, docQRTable, out errors);

        public int CapNhapKhoSP(string gioGiaoFcc, string nhaMay, out DataTable errors)
            => _kho.CapNhapKhoSP(gioGiaoFcc, nhaMay, out errors);

        public bool CapNhapKhoYMVN(int stt, string lotSl, string maHang, string ngayGiao,
            string gioGiao, string nhaMay, out DS_ERR_CNK error)
            => _kho.CapNhapKhoYMVN(stt, lotSl, maHang, ngayGiao, gioGiao, nhaMay, out error);

        public void DanhDauDaGiao(string poNo, string maHang, string ngayGiao, CustomerConfig cfg)
            => _kho.DanhDauDaGiao(poNo, maHang, ngayGiao, cfg);

       

        #endregion

        #region IPhieuLuuTruRepository — 100% delegate, KHÔNG chứa logic

        public DataTable LoadLuuPhieu(string nhaMay, string ngayGiao, string gioGiaoFcc)
            => _luuTru.LoadLuuPhieu(nhaMay, ngayGiao, gioGiaoFcc);

        public int LuuPhieuSP(string nhaMay, string ngayGiao, string gioGiaoFcc, string loaiPhieu)
            => _luuTru.LuuPhieuSP(nhaMay, ngayGiao, gioGiaoFcc, loaiPhieu);

        public void CapNhapTTPHIEU(string nhaMay, string ngayGiao, string gioGiaoFcc, int stt, string ghiChu)
            => _luuTru.CapNhapTTPHIEU(nhaMay, ngayGiao, gioGiaoFcc, stt, ghiChu);

        #endregion

        #region IPhieuGiaoDBRepository — 100% delegate, KHÔNG chứa logic

       
        public DataTable LoadLuuPhieuCaNgay(string nhaMay, string ngayGiao)=>

            _luuTru.LoadLuuPhieuCaNgay(nhaMay,ngayGiao);
        public Dictionary<string, int> LoadTonKhoBatch(List<string> maHangList) =>

           _luuTru.LoadTonKhoBatch( maHangList);
        #endregion

        #region Generic pass-through hạ tầng — KHÔNG phải nghiệp vụ, KHÔNG thuộc 6 repo con

        // 3 method này chỉ là wrapper mỏng gọi thẳng PhieuSqlExecutor theo tên SP bất kỳ
        // (dùng cho các form gọi SP đặc thù, không gắn với 1 nghiệp vụ Phiếu cụ thể nào).
        // Vì không có logic rẽ nhánh/điều kiện gì, giữ ở Facade là chấp nhận được — không
        // vi phạm nguyên tắc "không viết lại nghiệp vụ" vì bản thân nó không phải nghiệp vụ.
        //public DataTable LoadHangThieu(bool isMayBanQR, string tenBan)
        //  => _kho.LoadHangThieu(isMayBanQR, tenBan);

        public Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList)
             => _validation.GetQcDongGoiBatch(maHangList);

        public DataTable TakeLotYMVN(string tmpTable, string docQRTable, bool isLoaiSP)
            => _lot.TakeLotYMVN(tmpTable, docQRTable, isLoaiSP);
        public DataTable TinhHangThieuTuDonHang(DataTable donHang)
           => _validation.TinhHangThieuTuDonHang(donHang);
        #endregion
    }
}