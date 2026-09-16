using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.Shared.UiMd;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public interface IQTChungService
    {
        int TaoPhieuXuLyBatThuong(int phieuTraHangCTId, string model, string phanLoaiXuLy, string boPhanPhatHanh, string nguoiThucHien);
        ScanResult QCDinhHuong(int phieuXuLyId, HuongXuLyBatThuong huong, string nguoiThucHien);
        AffectedLotTraceResult TruyVetLOT(int phieuXuLyId, string nguoiThucHien);
        List<LotInfo> GetLotsCanRework(int phieuXuLyId);
        ScanResult XuatKhoRework(int phieuXuLyId, int slotId, string lotNo, int soLuong, string nguoiXuat);
        ScanResult GiaoHangRework(int phieuXuLyId, List<LotInfo> lots, string ngayGiao, string nguoiNhan, string boPhanNhan);
        void GhiNhanDangRework(int phieuXuLyId, string ghiChu, string nguoiThucHien);
        ScanResult QCXacNhanCuoi(int phieuXuLyId, int soLuongOK, int soLuongNG, string nguoiQC, int? slotIdOK = null, int? slotIdNG = null, string lotNo = null);
        void GhiNhanKiemTraTem(int qcId, bool daKiemTra);
        ScanResult NhapLaiHangNG(int phieuXuLyId, string lotNo, int soLuongNG, int? slotIdOK, int? slotIdNG, string nguoiNhap);
        ScanResult HoanTat(int phieuXuLyId, string nguoiThucHien);
        ScanResult XacNhanChoGiaoBu(int phieuXuLyId, string nguoiThucHien);
        ScanResult DanhDauChoGiaoBu(int phieuXuLyId, string nguoiThucHien);
        ScanResult GiaoLaiBoPhanPhatHien(int phieuXuLyId, string boPhanNhan, int soLuongGiaoLai, string nguoiThucHien);
        ScanResult HuyQTChung(int phieuXuLyId, string lyDoHuy, string nguoiThucHien);
        PhieuXuLyBatThuong GetById(int phieuXuLyId);
        IReadOnlyList<QTChungStatus> GetAllowedNext(int phieuXuLyId);
        List<QTChungTimelineItem> GetTimeline(int phieuXuLyId);
    }

    public sealed class ReworkQCResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }
        public int SoLuongRework { get; set; }
        public int SoLuongOK { get; set; }
        public int SoLuongNG { get; set; }
        public DateTime ConfirmedAt { get; set; }
        public string ConfirmedBy { get; set; }
        public string KetLuan { get; set; }
    }

    public sealed class DispositionResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }
        public int SoLuongLoaiBoBanDau { get; set; }
        public int SoLuongNGRework { get; set; }
        public int SoLuongLoaiBoCuoi { get; set; }
        public string Reason { get; set; }
        public DateTime ConfirmedAt { get; set; }
        public string ConfirmedBy { get; set; }
    }

    public sealed class CompensationResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }
        public int SoLuongYeuCau { get; set; }
        public int SoLuongDaGiao { get; set; }
        public int SoLuongConLai { get { return Math.Max(0, SoLuongYeuCau - SoLuongDaGiao); } }
        public int Status { get; set; }
        public string SourceReference { get; set; }
    }

    public sealed class HangLoiProcessReportRow
    {
        public int PhieuXuLyBatThuongId { get; set; }
        public string SoPhieu { get; set; }
        public string Model { get; set; }
        public string MaSanPham { get; set; }
        public int Status { get; set; }
        public string HuongXuLy { get; set; }
        public int SoLuongAnhHuong { get; set; }
        public int InitialOK { get; set; }
        public int InitialNG { get; set; }
        public int SoLuongRework { get; set; }
        public int SoLuongLoaiBoBanDau { get; set; }
        public int ReworkDaXuat { get; set; }
        public int ReworkDaGiao { get; set; }
        public int ReworkOK { get; set; }
        public int ReworkNG { get; set; }
        public int SoLuongLoaiBoCuoi { get; set; }
        public int CompensationRequired { get; set; }
        public int CompensationDelivered { get; set; }
        public int CompensationRemaining { get; set; }
    }

    public interface IHangLoiPhase5To9Service
    {
        ReworkQCResult ConfirmReworkQC(int phieuXuLyId, int soLuongOK, int soLuongNG, string ketLuan, string nguoiQC);
        ReworkQCResult GetReworkQC(int phieuXuLyId);
        DispositionResult ConfirmDisposition(int phieuXuLyId, string reason, string nguoiThucHien);
        DispositionResult GetDisposition(int phieuXuLyId);
        CompensationResult CreateCompensation(int phieuXuLyId, int soLuongYeuCau, string sourceReference, string nguoiThucHien);
        CompensationResult RecordCompensationDelivery(int phieuXuLyId, string lotNo, int? slotId, int soLuong, string nguoiThucHien);
        CompensationResult GetCompensation(int phieuXuLyId);
        ScanResult Transition(int phieuXuLyId, QTChungStatus toStatus, string actionName, string actor, string note);
        List<HangLoiProcessReportRow> GetProcessReport(int? phieuXuLyId = null);
    }

    public sealed class HangLoiPhase5To9Service : SqlRepositoryBase, IHangLoiPhase5To9Service
    {
        private const string ProcessCode = "QT_CHUNG";
        private readonly IPhieuXuLyBatThuongRepository _phieuRepository;
        private readonly IInitialQCService _initialQC;
        private readonly IReworkPhase4Service _reworkPhase4;
        private readonly IWorkflowTransitionService _workflow;

        public HangLoiPhase5To9Service(PhieuSqlExecutor db, IUnitOfWork uow, IPhieuXuLyBatThuongRepository phieuRepository, IInitialQCService initialQC, IReworkPhase4Service reworkPhase4, IWorkflowTransitionService workflow)
            : base(db, uow)
        {
            _phieuRepository = phieuRepository ?? throw new ArgumentNullException(nameof(phieuRepository));
            _initialQC = initialQC ?? throw new ArgumentNullException(nameof(initialQC));
            _reworkPhase4 = reworkPhase4 ?? throw new ArgumentNullException(nameof(reworkPhase4));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        }

        public ReworkQCResult ConfirmReworkQC(int phieuXuLyId, int soLuongOK, int soLuongNG, string ketLuan, string nguoiQC)
        {
            RequireActor(nguoiQC);
            if (soLuongOK < 0 || soLuongNG < 0 || soLuongOK + soLuongNG <= 0) throw new ArgumentException("Kết quả QC Rework không hợp lệ.");
            var initial = _initialQC.Get(phieuXuLyId);
            if (initial == null) throw new InvalidOperationException("Chưa có Initial QC.");
            if (initial.SoLuongRework <= 0) throw new InvalidOperationException("Initial QC không phân bổ số lượng Rework.");
            var plan = _reworkPhase4.GetPlan(phieuXuLyId);
            if (plan == null || !plan.DaXuatDu || !plan.DaGiaoDu) throw new InvalidOperationException("Chỉ được QC Rework sau khi đã xuất và giao đủ Rework.");
            if (soLuongOK + soLuongNG != initial.SoLuongRework) throw new InvalidOperationException("QC Rework phải phân loại đủ đúng số lượng Rework của Initial QC.");
            if (GetReworkQC(phieuXuLyId) != null) throw new InvalidOperationException("Phiếu đã có kết quả QC Rework.");
            try
            {
                Uow.Begin();
                ExecuteNonQuery(@"INSERT INTO dbo.FVN_PXLB_ReworkQC(PhieuXuLyBatThuongId,SoLuongRework,SoLuongOK,SoLuongNG,ConfirmedAt,ConfirmedBy,KetLuan) VALUES(@P,@R,@O,@N,GETDATE(),@By,@K);",
                    new SqlParameter("@P", phieuXuLyId), new SqlParameter("@R", initial.SoLuongRework), new SqlParameter("@O", soLuongOK), new SqlParameter("@N", soLuongNG), new SqlParameter("@By", nguoiQC), new SqlParameter("@K", DbValueHelper.DbValue(ketLuan)));
                Uow.Commit();
                return GetReworkQC(phieuXuLyId);
            }
            catch { SafeRollback(); throw; }
        }

        public ReworkQCResult GetReworkQC(int phieuXuLyId)
        {
            var dt = LoadData(@"SELECT TOP 1 Id,PhieuXuLyBatThuongId,SoLuongRework,SoLuongOK,SoLuongNG,ConfirmedAt,ConfirmedBy,KetLuan FROM dbo.FVN_PXLB_ReworkQC WHERE PhieuXuLyBatThuongId=@P;", new SqlParameter("@P", phieuXuLyId));
            if (dt.Rows.Count == 0) return null;
            var r = dt.Rows[0];
            return new ReworkQCResult { Id=DbValueHelper.ToInt(r["Id"]), PhieuXuLyBatThuongId=DbValueHelper.ToInt(r["PhieuXuLyBatThuongId"]), SoLuongRework=DbValueHelper.ToInt(r["SoLuongRework"]), SoLuongOK=DbValueHelper.ToInt(r["SoLuongOK"]), SoLuongNG=DbValueHelper.ToInt(r["SoLuongNG"]), ConfirmedAt=DbValueHelper.ToDateTime(r["ConfirmedAt"]) ?? DateTime.MinValue, ConfirmedBy=DbValueHelper.ToString(r["ConfirmedBy"]), KetLuan=DbValueHelper.ToString(r["KetLuan"]) };
        }

        public DispositionResult ConfirmDisposition(int phieuXuLyId, string reason, string nguoiThucHien)
        {
            RequireActor(nguoiThucHien);
            var initial = _initialQC.Get(phieuXuLyId);
            if (initial == null) throw new InvalidOperationException("Chưa có Initial QC.");
            var reworkQc = GetReworkQC(phieuXuLyId);
            if (initial.SoLuongRework > 0 && reworkQc == null) throw new InvalidOperationException("Phải QC Rework trước khi xác nhận Disposition.");
            if (GetDisposition(phieuXuLyId) != null) throw new InvalidOperationException("Phiếu đã có Disposition.");
            int reworkNg = reworkQc == null ? 0 : reworkQc.SoLuongNG;
            int finalScrap = initial.SoLuongLoaiBoBanDau + reworkNg;
            try
            {
                Uow.Begin();
                ExecuteNonQuery(@"INSERT INTO dbo.FVN_PXLB_Disposition(PhieuXuLyBatThuongId,SoLuongLoaiBoBanDau,SoLuongNGRework,SoLuongLoaiBoCuoi,Reason,ConfirmedAt,ConfirmedBy) VALUES(@P,@S,@N,@F,@R,GETDATE(),@By);",
                    new SqlParameter("@P", phieuXuLyId), new SqlParameter("@S", initial.SoLuongLoaiBoBanDau), new SqlParameter("@N", reworkNg), new SqlParameter("@F", finalScrap), new SqlParameter("@R", DbValueHelper.DbValue(reason)), new SqlParameter("@By", nguoiThucHien));
                Uow.Commit();
                return GetDisposition(phieuXuLyId);
            }
            catch { SafeRollback(); throw; }
        }

        public DispositionResult GetDisposition(int phieuXuLyId)
        {
            var dt = LoadData(@"SELECT TOP 1 Id,PhieuXuLyBatThuongId,SoLuongLoaiBoBanDau,SoLuongNGRework,SoLuongLoaiBoCuoi,Reason,ConfirmedAt,ConfirmedBy FROM dbo.FVN_PXLB_Disposition WHERE PhieuXuLyBatThuongId=@P;", new SqlParameter("@P", phieuXuLyId));
            if (dt.Rows.Count == 0) return null;
            var r=dt.Rows[0];
            return new DispositionResult { Id=DbValueHelper.ToInt(r["Id"]), PhieuXuLyBatThuongId=DbValueHelper.ToInt(r["PhieuXuLyBatThuongId"]), SoLuongLoaiBoBanDau=DbValueHelper.ToInt(r["SoLuongLoaiBoBanDau"]), SoLuongNGRework=DbValueHelper.ToInt(r["SoLuongNGRework"]), SoLuongLoaiBoCuoi=DbValueHelper.ToInt(r["SoLuongLoaiBoCuoi"]), Reason=DbValueHelper.ToString(r["Reason"]), ConfirmedAt=DbValueHelper.ToDateTime(r["ConfirmedAt"]) ?? DateTime.MinValue, ConfirmedBy=DbValueHelper.ToString(r["ConfirmedBy"]) };
        }

        public CompensationResult CreateCompensation(int phieuXuLyId, int soLuongYeuCau, string sourceReference, string nguoiThucHien)
        {
            RequireActor(nguoiThucHien);
            if (soLuongYeuCau <= 0) throw new ArgumentException("SoLuongYeuCau phải lớn hơn 0.");
            if (GetCompensation(phieuXuLyId) != null) throw new InvalidOperationException("Phiếu đã có nghĩa vụ giao bù.");
            try
            {
                Uow.Begin();
                ExecuteNonQuery(@"INSERT INTO dbo.FVN_PXLB_Compensation(PhieuXuLyBatThuongId,SoLuongYeuCau,SoLuongDaGiao,Status,SourceReference,CreatedAt,CreatedBy) VALUES(@P,@Q,0,0,@S,GETDATE(),@By);",
                    new SqlParameter("@P", phieuXuLyId), new SqlParameter("@Q", soLuongYeuCau), new SqlParameter("@S", DbValueHelper.DbValue(sourceReference)), new SqlParameter("@By", nguoiThucHien));
                Uow.Commit();
                return GetCompensation(phieuXuLyId);
            }
            catch { SafeRollback(); throw; }
        }

        public CompensationResult RecordCompensationDelivery(int phieuXuLyId, string lotNo, int? slotId, int soLuong, string nguoiThucHien)
        {
            RequireActor(nguoiThucHien);
            if (string.IsNullOrWhiteSpace(lotNo) || soLuong <= 0) throw new ArgumentException("LOT và số lượng giao bù không hợp lệ.");
            var comp=GetCompensation(phieuXuLyId);
            if (comp==null) throw new InvalidOperationException("Chưa tạo nghĩa vụ giao bù.");
            if (comp.SoLuongDaGiao + soLuong > comp.SoLuongYeuCau) throw new InvalidOperationException("Số lượng giao bù vượt nghĩa vụ còn lại.");
            try
            {
                Uow.Begin();
                ExecuteNonQuery(@"INSERT INTO dbo.FVN_PXLB_CompensationMovement(CompensationId,LotNo,SlotId,SoLuong,ThoiGian,NguoiThucHien) VALUES((SELECT Id FROM dbo.FVN_PXLB_Compensation WHERE PhieuXuLyBatThuongId=@P),@Lot,@Slot,@Q,GETDATE(),@By);",
                    new SqlParameter("@P", phieuXuLyId), new SqlParameter("@Lot", lotNo.Trim()), new SqlParameter("@Slot", slotId.HasValue ? (object)slotId.Value : DBNull.Value), new SqlParameter("@Q", soLuong), new SqlParameter("@By", nguoiThucHien));
                ExecuteNonQuery(@"UPDATE dbo.FVN_PXLB_Compensation SET SoLuongDaGiao=SoLuongDaGiao+@Q,Status=CASE WHEN SoLuongDaGiao+@Q>=SoLuongYeuCau THEN 1 ELSE 0 END,CompletedAt=CASE WHEN SoLuongDaGiao+@Q>=SoLuongYeuCau THEN GETDATE() ELSE CompletedAt END,CompletedBy=CASE WHEN SoLuongDaGiao+@Q>=SoLuongYeuCau THEN @By ELSE CompletedBy END WHERE PhieuXuLyBatThuongId=@P;",
                    new SqlParameter("@P", phieuXuLyId), new SqlParameter("@Q", soLuong), new SqlParameter("@By", nguoiThucHien));
                Uow.Commit();
                return GetCompensation(phieuXuLyId);
            }
            catch { SafeRollback(); throw; }
        }

        public CompensationResult GetCompensation(int phieuXuLyId)
        {
            var dt=LoadData(@"SELECT TOP 1 Id,PhieuXuLyBatThuongId,SoLuongYeuCau,SoLuongDaGiao,Status,SourceReference FROM dbo.FVN_PXLB_Compensation WHERE PhieuXuLyBatThuongId=@P;", new SqlParameter("@P", phieuXuLyId));
            if(dt.Rows.Count==0)return null; var r=dt.Rows[0];
            return new CompensationResult { Id=DbValueHelper.ToInt(r["Id"]), PhieuXuLyBatThuongId=DbValueHelper.ToInt(r["PhieuXuLyBatThuongId"]), SoLuongYeuCau=DbValueHelper.ToInt(r["SoLuongYeuCau"]), SoLuongDaGiao=DbValueHelper.ToInt(r["SoLuongDaGiao"]), Status=DbValueHelper.ToInt(r["Status"]), SourceReference=DbValueHelper.ToString(r["SourceReference"]) };
        }

        public ScanResult Transition(int phieuXuLyId, QTChungStatus toStatus, string actionName, string actor, string note)
        {
            RequireActor(actor); var phieu=_phieuRepository.GetById(phieuXuLyId); if(phieu==null)return ScanResult.Fail("Không tìm thấy phiếu.");
            try
            {
                _workflow.EnsureCanTransition(ProcessCode,(int)phieu.Status,(int)toStatus);
                Uow.Begin();
                ExecuteNonQuery(@"INSERT INTO dbo.FVN_PXLB_WorkflowAudit(PhieuXuLyBatThuongId,FromStatus,ToStatus,ActionName,Actor,OccurredAt,Note) VALUES(@P,@F,@T,@A,@By,GETDATE(),@N);", new SqlParameter("@P",phieuXuLyId),new SqlParameter("@F",(int)phieu.Status),new SqlParameter("@T",(int)toStatus),new SqlParameter("@A",string.IsNullOrWhiteSpace(actionName)?toStatus.ToString():actionName),new SqlParameter("@By",actor),new SqlParameter("@N",DbValueHelper.DbValue(note)));
                _phieuRepository.UpdateStatus(phieuXuLyId,toStatus,actor); Uow.Commit();
                return ScanResult.OK("Đã chuyển trạng thái.");
            }
            catch(Exception ex){SafeRollback();return ScanResult.Fail(ex.Message);}
        }

        public List<HangLoiProcessReportRow> GetProcessReport(int? phieuXuLyId = null)
        {
            string sql=@"SELECT * FROM dbo.vFVN_PXLB_ProcessReport"+(phieuXuLyId.HasValue?" WHERE PhieuXuLyBatThuongId=@P":"")+" ORDER BY PhieuXuLyBatThuongId DESC;";
            var dt=phieuXuLyId.HasValue?LoadData(sql,new SqlParameter("@P",phieuXuLyId.Value)):LoadData(sql);
            var result=new List<HangLoiProcessReportRow>();
            foreach(DataRow r in dt.Rows) result.Add(new HangLoiProcessReportRow { PhieuXuLyBatThuongId=DbValueHelper.ToInt(r["PhieuXuLyBatThuongId"]),SoPhieu=DbValueHelper.ToString(r["SoPhieu"]),Model=DbValueHelper.ToString(r["Model"]),MaSanPham=DbValueHelper.ToString(r["MaSanPham"]),Status=DbValueHelper.ToInt(r["Status"]),HuongXuLy=DbValueHelper.ToString(r["HuongXuLy"]),SoLuongAnhHuong=DbValueHelper.ToInt(r["SoLuongAnhHuong"]),InitialOK=DbValueHelper.ToInt(r["InitialOK"]),InitialNG=DbValueHelper.ToInt(r["InitialNG"]),SoLuongRework=DbValueHelper.ToInt(r["SoLuongRework"]),SoLuongLoaiBoBanDau=DbValueHelper.ToInt(r["SoLuongLoaiBoBanDau"]),ReworkDaXuat=DbValueHelper.ToInt(r["ReworkDaXuat"]),ReworkDaGiao=DbValueHelper.ToInt(r["ReworkDaGiao"]),ReworkOK=DbValueHelper.ToInt(r["ReworkOK"]),ReworkNG=DbValueHelper.ToInt(r["ReworkNG"]),SoLuongLoaiBoCuoi=DbValueHelper.ToInt(r["SoLuongLoaiBoCuoi"]),CompensationRequired=DbValueHelper.ToInt(r["CompensationRequired"]),CompensationDelivered=DbValueHelper.ToInt(r["CompensationDelivered"]),CompensationRemaining=DbValueHelper.ToInt(r["CompensationRemaining"]) });
            return result;
        }

        private static void RequireActor(string actor){if(string.IsNullOrWhiteSpace(actor))throw new ArgumentException("NguoiThucHien không được rỗng.",nameof(actor));}
        private void SafeRollback(){try{Uow.Rollback();}catch{}}
    }
}
