using System;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH
    {
        /// <summary>
        /// Đăng ký sau khi Form đã Load xong để handler này đứng SAU presenter
        /// trong chuỗi HoanThanhClicked.
        ///
        /// Presenter vẫn chịu trách nhiệm nghiệp vụ Hoàn thành và gọi
        /// SwitchToPhieuView(). Handler này chỉ đảm bảo UI cuối cùng thực sự
        /// ở trạng thái GridView Đơn hàng, không còn panel/GridView DocQR nằm
        /// trên cùng do Z-order của các UserControl/panel.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // ============================================================
            // UI FIX - Hoàn thành DocQR phải quay về GridView Đơn hàng.
            //
            // Đăng ký sau base.OnLoad() để presenter đã đăng ký handler
            // HoanThanhClicked trước. BeginInvoke tiếp tục đẩy thao tác
            // reset UI xuống cuối message queue, tránh bị presenter hoặc
            // một callback UI khác đưa DocQR lên lại ngay sau đó.
            // ============================================================
            HoanThanhClicked += OnHoanThanhResetPhieuView;
        }

        /// <summary>
        /// Chỉ xử lý trạng thái hiển thị UI, không xử lý nghiệp vụ/data.
        /// Nghiệp vụ Hoàn thành vẫn nằm ở PhieuPresenter.
        /// </summary>
        private void OnHoanThanhResetPhieuView(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || IsHandleCreated == false)
                    return;

                // ========================================================
                // Bước 1: đóng hoàn toàn vùng DocQR.
                // Chỉ BringToFront Grid đơn hàng là chưa đủ vì DocQR
                // vẫn có thể còn Visible và nằm trên một container khác.
                // ========================================================
                _docQrControl.Visible = false;
                PN_DOCQR_SUASL1.Visible = false;

                // ========================================================
                // Bước 2: bật lại vùng Phiếu + GridView Đơn hàng.
                // ========================================================
                _phieuHeaderControl.Visible = true;
                _phieuGridControl.Visible = true;
                _phieuHeaderControl.BringToFront();
                _phieuGridControl.BringToFrontGrid();

                // ========================================================
                // Bước 3: bảo đảm các control phụ của màn hình Phiếu
                // không bị vùng DocQR cũ che lên.
                // ActionBar đã được PhieuPresenter cấu hình; ở đây chỉ
                // đảm bảo trạng thái visual cuối cùng.
                // ========================================================
                try
                {
                    _hangThieuControl.Visible = true;
                    _hangThieuControl.BringToFront();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[OnHoanThanhResetPhieuView] Không thể đưa HangThieu lên trước: " + ex.Message);
                }
            }));
        }
    }
}
