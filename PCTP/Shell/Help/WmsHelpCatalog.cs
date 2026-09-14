using System.Collections.Generic;

namespace PCTP.Shell.Help
{
    internal static class WmsHelpCatalog
    {
        internal static IList<WmsHelpTopic> GetAll()
        {
            return new List<WmsHelpTopic>
            {
                Topic(
                    "Dashboard",
                    "Tổng quan WMS / Control Center",
                    "Theo dõi nhanh tình trạng nhập kho, giao hàng, hàng lỗi và các việc cần xử lý.",
                    "Đăng nhập hệ thống và chọn đúng nhà máy/máy làm việc.",
                    "1. Xem các chỉ số trên Dashboard.\n2. Chọn nhóm công việc cần xử lý.\n3. Nhấn vào vùng dữ liệu hoặc nút chức năng để mở module tương ứng.\n4. Dùng Làm mới khi cần cập nhật dữ liệu.",
                    "Dashboard phải phản ánh số liệu đọc được từ các module nghiệp vụ; không dùng Dashboard để thay đổi nghiệp vụ.",
                    "Số liệu chưa cập nhật; dashboard không mở được module; số liệu khác với màn hình nghiệp vụ.",
                    "Nhấn Làm mới trước. Nếu vẫn sai, kiểm tra thời gian truy vấn và liên hệ IT kèm tên module, thời điểm và mã phiếu/LOT.",
                    "flowchart TD\nA[Đăng nhập WMS] --> B[Dashboard]\nB --> C[Nhập kho]\nB --> D[Giao hàng]\nB --> E[Hàng lỗi]\nB --> F[Báo cáo & Tra cứu]"),

                Topic(
                    "NhapKho.QR",
                    "Nhập kho QR",
                    "Tiếp nhận hàng vào kho theo QR/tem và ghi nhận trạng thái nhập kho.",
                    "Hàng có QR hợp lệ; người dùng có quyền nhập kho; khu vực/slot kho đã sẵn sàng.",
                    "1. Mở Nhập kho QR.\n2. Quét QR/tem.\n3. Kiểm tra Part, LOT, số lượng và trạng thái.\n4. Chọn vị trí/slot nếu nghiệp vụ yêu cầu.\n5. Xác nhận nhập kho.\n6. Kiểm tra kết quả sau khi ghi nhận.",
                    "Chỉ xác nhận khi QR, LOT, Part, số lượng và trạng thái đều hợp lệ.",
                    "QR trùng; LOT không tồn tại; số lượng không hợp lệ; slot không phù hợp; hàng đã nhập.",
                    "Không quét lại liên tục khi hệ thống đang xử lý. Kiểm tra mã QR và LOT trước; nếu lỗi lặp lại ghi nhận mã phiếu/thời điểm cho IT.",
                    "flowchart TD\nA[Quét QR] --> B{QR hợp lệ?}\nB -- Không --> E[Thông báo lỗi]\nB -- Có --> C[Kiểm tra LOT/Part/SL]\nC --> D{Hợp lệ?}\nD -- Không --> E\nD -- Có --> F[Chọn slot]\nF --> G[Xác nhận nhập kho]\nG --> H[Kiểm tra kết quả]"),

                Topic(
                    "NhapKho.KhongQR",
                    "Nhập kho không QR",
                    "Xử lý luồng nhập kho không sử dụng QR khi nghiệp vụ cho phép.",
                    "Được phép dùng luồng không QR và có đủ thông tin Part/LOT/số lượng/chứng từ.",
                    "1. Mở luồng Nhập kho không QR.\n2. Nhập mã Part/LOT và số lượng.\n3. Kiểm tra chứng từ.\n4. Chọn vị trí nếu cần.\n5. Xác nhận và kiểm tra tồn sau nhập.",
                    "Thông tin chứng từ và số lượng phải khớp trước khi xác nhận.",
                    "Thiếu LOT; sai Part; vượt số lượng; không được phép nhập không QR.",
                    "Dừng thao tác nếu dữ liệu không khớp. Không tự sửa dữ liệu nguồn để bỏ qua kiểm soát.",
                    "flowchart TD\nA[Nhập Part/LOT/SL] --> B[Kiểm tra chứng từ]\nB --> C{Hợp lệ?}\nC -- Không --> D[Điều chỉnh dữ liệu nguồn]\nC -- Có --> E[Xác nhận nhập kho]\nE --> F[Cập nhật tồn]"),

                Topic(
                    "XuLyHangLoi",
                    "Xử lý hàng lỗi / bất thường",
                    "Tạo và theo dõi tiến trình xử lý NG, rework, OK/NG và các bước xác nhận.",
                    "Có mã hàng/LOT/phiếu và nguyên nhân bất thường rõ ràng.",
                    "1. Mở danh sách phiếu xử lý.\n2. Chọn hoặc tạo phiếu.\n3. Kiểm tra số lượng và nguyên nhân.\n4. Thực hiện bước xử lý theo workflow.\n5. Ghi nhận kết quả OK/NG.\n6. Theo dõi trạng thái đến khi kết thúc.",
                    "Mỗi bước phải đúng trạng thái trước đó và đúng người thực hiện theo phân quyền.",
                    "Sai trạng thái; thiếu số lượng; không có công đoạn tiếp theo; phiếu đã kết thúc.",
                    "Không ép trạng thái bằng thao tác ngoài workflow. Tra cứu lịch sử phiếu để xác định bước đang thiếu.",
                    "flowchart TD\nA[Phiếu bất thường] --> B[Kiểm tra nguyên nhân/SL]\nB --> C[Xử lý/Rework]\nC --> D{Kết quả}\nD -- OK --> E[Hoàn tất]\nD -- NG --> F[Chuyển bước tiếp theo]\nF --> C"),

                Topic(
                    "GiaoHang.HVN",
                    "Giao hàng HVN",
                    "Thực hiện các bước chuẩn bị, kiểm tra và xác nhận giao hàng cho HVN.",
                    "Phiếu giao hàng hợp lệ; LOT/Part đủ điều kiện giao; dữ liệu khách hàng đã xác định.",
                    "1. Mở giao hàng HVN.\n2. Chọn loại giao hàng.\n3. Kiểm tra phiếu và danh sách LOT.\n4. Kiểm tra thiếu/thừa và trạng thái hàng.\n5. In chứng từ/tem nếu cần.\n6. Xác nhận giao hàng.",
                    "Không xác nhận khi còn thiếu LOT, sai Part hoặc sai số lượng.",
                    "Thiếu LOT; LOT chưa đủ điều kiện; sai số lượng; trùng phiếu; QR chưa xác nhận.",
                    "Dùng Tra cứu LOT/QR để đối chiếu trước khi xác nhận. Nếu lỗi dữ liệu nguồn, dừng và báo người phụ trách nghiệp vụ.",
                    "flowchart TD\nA[Phiếu giao HVN] --> B[Kiểm tra LOT/Part/SL]\nB --> C{Đủ điều kiện?}\nC -- Không --> D[Tra cứu/điều chỉnh nghiệp vụ]\nC -- Có --> E[Chuẩn bị giao]\nE --> F[Xác nhận giao hàng]"),

                Topic(
                    "GiaoHang.YMVN",
                    "Giao hàng YMVN",
                    "Thực hiện quy trình giao hàng YMVN theo loại MP/SP và kiểm soát LOT trước xác nhận.",
                    "Phiếu giao hàng YMVN hợp lệ và dữ liệu MP/SP đã xác định.",
                    "1. Chọn YMVN MP hoặc SP.\n2. Kiểm tra phiếu.\n3. Kiểm tra LOT/Part/số lượng.\n4. Hoàn tất chuẩn bị chứng từ.\n5. Xác nhận giao.",
                    "Phiếu, loại giao và số lượng phải khớp trước xác nhận.",
                    "Sai MP/SP; thiếu LOT; số lượng không khớp; phiếu đã giao.",
                    "Đối chiếu phiếu và LOT bằng Tra cứu trước khi xác nhận; không tạo phiếu thay thế chỉ để bỏ qua lỗi.",
                    "flowchart TD\nA[Phiếu YMVN] --> B{MP hay SP?}\nB --> C[Kiểm tra LOT/Part/SL]\nC --> D{Hợp lệ?}\nD -- Không --> E[Tra cứu/điều chỉnh]\nD -- Có --> F[Xác nhận giao]"),

                Topic(
                    "BaoCao.TraCuu",
                    "Báo cáo & Tra cứu",
                    "Tra cứu read-only theo QR, LOT, Part, phiếu, khách hàng và lịch sử tồn kho.",
                    "Có ít nhất một khóa tra cứu đáng tin cậy và khoảng thời gian phù hợp với loại báo cáo.",
                    "1. Mở Báo cáo & Tra cứu.\n2. Chọn loại tra cứu.\n3. Nhập QR/LOT/Part/phiếu/khách hàng.\n4. Chọn khoảng thời gian.\n5. Chạy truy vấn.\n6. Đọc timeline hoặc bảng kết quả.\n7. Export/print nếu màn hình hỗ trợ.",
                    "Kết quả tra cứu là read-only; không dùng màn hình báo cáo để thay đổi nghiệp vụ.",
                    "Không có dữ liệu; khoảng thời gian quá rộng; kết quả thiếu liên kết giữa các module.",
                    "Thu hẹp khoảng thời gian và dùng khóa tra cứu chính xác hơn. Ghi lại query key khi báo lỗi.",
                    "flowchart TD\nA[Chọn loại tra cứu] --> B[Nhập QR/LOT/Part/Phiếu]\nB --> C[Chọn thời gian]\nC --> D[Query read-only]\nD --> E[Timeline/Kết quả]\nE --> F[Export/Print]"),

                Topic(
                    "BaoCao.LOT",
                    "Tra cứu LOT",
                    "Theo dõi LOT từ nhập kho, vị trí, xuất kho đến giao hàng và lịch sử liên quan.",
                    "Có mã LOT hoặc khóa tra cứu tương đương.",
                    "1. Nhập LOT.\n2. Chạy tra cứu.\n3. Kiểm tra Part và tồn.\n4. Kiểm tra lịch sử nhập/xuất.\n5. Kiểm tra giao hàng nếu có.\n6. Đối chiếu timeline.",
                    "Các mốc thời gian phải được đọc theo thứ tự và không suy diễn khi nguồn không có liên kết chắc chắn.",
                    "LOT có nhiều nguồn; thiếu quan hệ giao hàng; không tìm thấy lịch sử.",
                    "Kiểm tra đúng LOT/Part và khoảng thời gian. Không coi dữ liệu thiếu liên kết là dữ liệu giao hàng chắc chắn.",
                    "flowchart TD\nA[LOT] --> B[Tồn hiện tại]\nA --> C[Lịch sử kho]\nA --> D[Quality/QC]\nA --> E[Giao hàng]\nB --> F[Timeline tổng hợp]\nC --> F\nD --> F\nE --> F"),

                Topic(
                    "BaoCao.QR",
                    "Tra cứu QR / Traceability",
                    "Theo dõi QR/tem qua các mốc nghiệp vụ mà dữ liệu nguồn hỗ trợ.",
                    "Có QR/Tem code hợp lệ.",
                    "1. Nhập QR.\n2. Kiểm tra thông tin Part/LOT.\n3. Xem lịch sử QC.\n4. Xem lịch sử kho.\n5. Xem giao hàng nếu có liên kết.",
                    "Chỉ kết luận các mốc có dữ liệu nguồn xác thực.",
                    "QR không tồn tại; QR trùng; thiếu liên kết sang giao hàng.",
                    "Thử tìm theo LOT hoặc Part để đối chiếu. Nếu thiếu liên kết, ghi rõ nguồn/mốc thiếu khi báo IT.",
                    "flowchart TD\nA[QR/Tem] --> B[Part/LOT]\nB --> C[QC]\nB --> D[Kho]\nB --> E[Giao hàng]\nC --> F[Trace timeline]\nD --> F\nE --> F"),

                Topic(
                    "QR.MachineSwitch",
                    "Chuyển máy bắn QR",
                    "Chuyển máy đang được hệ thống ghi nhận là máy bắn QR hiện hành.",
                    "Biết rõ hostname máy mới và có quyền thực hiện thao tác.",
                    "1. Chọn chức năng chuyển máy.\n2. Kiểm tra máy hiện tại và máy đích.\n3. Xác nhận.\n4. Hệ thống cập nhật máy hiện hành và khởi động lại ứng dụng.",
                    "Chỉ chuyển khi máy đích chính xác và đang sẵn sàng sử dụng.",
                    "Nhầm máy; máy đích không đúng hostname; trạng thái máy chưa cập nhật.",
                    "Không chuyển liên tục. Sau khi ứng dụng khởi động lại, kiểm tra lại hostname và trạng thái QR.",
                    "flowchart TD\nA[Máy hiện tại] --> B[Chọn máy đích]\nB --> C{Xác nhận?}\nC -- Không --> D[Giữ nguyên]\nC -- Có --> E[Cập nhật máy QR]\nE --> F[Khởi động lại ứng dụng]\nF --> G[Kiểm tra trạng thái]"),

                Topic(
                    "Troubleshooting",
                    "Xử lý lỗi thường gặp",
                    "Hướng dẫn cách thu thập thông tin và xử lý các lỗi WMS trước khi chuyển cho IT.",
                    "Người dùng xác định được module, chức năng và thời điểm lỗi.",
                    "1. Ghi lại module/chức năng.\n2. Ghi mã QR/LOT/phiếu nếu có.\n3. Ghi thời gian xảy ra.\n4. Chụp màn hình lỗi.\n5. Thử lại một lần với dữ liệu đã kiểm tra.\n6. Báo IT nếu lỗi lặp lại.",
                    "Không tự sửa DB hoặc dùng chức năng khác để bỏ qua kiểm soát.",
                    "SQL timeout; không có dữ liệu; quyền truy cập; ứng dụng treo; QR/LOT không hợp lệ.",
                    "IT cần tối thiểu: tên máy, user, module, chức năng, thời gian, mã dữ liệu và ảnh lỗi.",
                    "flowchart TD\nA[Lỗi WMS] --> B[Kiểm tra dữ liệu đầu vào]\nB --> C[Thử lại 1 lần]\nC --> D{Còn lỗi?}\nD -- Không --> E[Tiếp tục nghiệp vụ]\nD -- Có --> F[Thu thập thông tin]\nF --> G[Báo IT]"),
            };
        }

        private static WmsHelpTopic Topic(
            string key,
            string title,
            string purpose,
            string preconditions,
            string steps,
            string confirmation,
            string commonErrors,
            string troubleshooting,
            string mermaid)
        {
            return new WmsHelpTopic(
                key,
                title,
                purpose,
                preconditions,
                steps,
                confirmation,
                commonErrors,
                troubleshooting,
                mermaid);
        }
    }
}
