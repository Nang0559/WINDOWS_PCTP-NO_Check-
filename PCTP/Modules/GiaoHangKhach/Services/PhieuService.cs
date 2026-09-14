using DevExpress.DataAccess.DataFederation;
using DevExpress.Office;
using DevExpress.Pdf.Native;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.WorkingState;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Facade nghiệp vụ cho phiếu giao hàng.
    ///
    /// Phase 5/6: LoadPhieu đã được tách sang PhieuLoadService + OrderLoadResult.
    /// Phase 7: các business flow Kho / GiaoDB / YMVN được chuyển sang service riêng;
    /// PhieuService chỉ giữ API tương thích với UI/Presenter hiện tại.
    /// </summary>