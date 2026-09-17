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
                    "Xử lý hàng lỗi / bất thường - Hướng dẫn đầy đủ",
                    "Quản lý trọn quy trình từ tạo phiếu, truy vết LOT, QC định hướng, Initial QC, Rework, QC sau Rework, Disposition và Giao bù.",
                    "Có mã hàng/Part, LOT hoặc thông tin truy vết tương ứng; xác định được nguyên nhân bất thường và người thực hiện có quyền.",
                    "1. Tạo phiếu xử lý hàng lỗi/bất thường.\n2. Truy vết LOT từ kho thành phẩm, WIP/sản xuất và hàng khách trả; kiểm tra tổng số lượng ảnh hưởng.\n3. QC định hướng và xác nhận phạm vi xử lý.\n4. Initial QC phải kiểm tra đủ số lượng ảnh hưởng: DaKiemTra = OK + NG.\n5. Phân loại NG: NG = Rework + LoaiBoBanDau.\n6. Nếu có Rework: xuất rework không vượt số lượng Rework của Initial QC; giao sản xuất đủ số lượng đã xuất.\n7. QC sau Rework: OK + NG = tổng số lượng Rework đã giao.\n8. NG sau Rework được cộng vào Disposition; tổng loại bỏ cuối = LoaiBoBanDau + NG sau Rework.\n9. Nếu có Giao bù, xử lý theo nghĩa vụ giao bù độc lập và kiểm soát QR/tồn kho/FIFO; không tự suy ra Giao bù từ NG/Rework.\n10. Theo dõi workflow đến Hoàn tất hoặc Hủy.",
                    "Không bỏ qua bước workflow. Số lượng phải bảo toàn ở từng công đoạn; không được nhập lại/ xuất lại một lượng đã ghi nhận. Giao bù là nghĩa vụ độc lập, không phải phần NG còn lại sau Rework.",
                    "LOT không truy được hoặc snapshot không đầy đủ; Initial QC sai tổng; NG khác Rework + loại bỏ; xuất Rework vượt kế hoạch; giao sản xuất khi chưa xuất đủ; QC sau Rework không khớp; Disposition sai; giao bù vượt nghĩa vụ hoặc vượt tồn/FIFO; thao tác sai trạng thái.",
                    "Dừng thao tác và kiểm tra snapshot/phiếu, trạng thái workflow và số lượng lũy kế. Không sửa DB để vượt kiểm soát. Khi báo IT cung cấp mã phiếu, LOT, bước workflow, số lượng và thời điểm lỗi.",
                    "flowchart TD\nA[Tạo phiếu] --> B[Truy vết LOT]\nB --> C[QC định hướng]\nC --> D[Initial QC]\nD --> E{Có Rework?}\nE -- Không --> F[Disposition nếu có NG loại bỏ]\nE -- Có --> G[Xuất Rework]\nG --> H[Giao sản xuất]\nH --> I[QC sau Rework]\nI --> J[Disposition NG sau Rework]\nF --> K{Có Giao bù?}\nJ --> K\nK -- Có --> L[Giao bù theo nghĩa vụ độc lập + FIFO]\nK -- Không --> M[Hoàn tất theo workflow]\nL --> M"),

                Topic(
                    "XuLyHangLoi.TraceLOT",
                    "Xử lý hàng lỗi - Truy vết LOT",
                    "Xác định đầy đủ các nguồn hàng bị ảnh hưởng trước khi QC: kho thành phẩm, WIP/sản xuất và hàng khách trả.",
                    "Có MaSanPham và LotNo hoặc phiếu đã xác định thông tin truy vết.",
                    "1. Mở phiếu và chọn Truy vết LOT.\n2. Kiểm tra tồn kho thành phẩm theo LOT.\n3. Kiểm tra số lượng WIP/sản xuất liên quan.\n4. Kiểm tra hàng khách trả.\n5. Đối chiếu Part/Model/LOT và nguồn.\n6. Xác nhận snapshot truy vết trước khi QC.",
                    "Snapshot là phạm vi làm việc của phiếu; không tự thay đổi số lượng ảnh hưởng sau khi QC nếu chưa thực hiện lại nghiệp vụ được quy định.",
                    "Không tìm thấy LOT; nguồn truy vết không đầy đủ; Part/LOT không khớp; tổng ảnh hưởng bằng 0.",
                    "Kiểm tra lại MaSanPham, LotNo và nguồn dữ liệu. Nếu một nguồn không truy được, không coi snapshot là đầy đủ để tiếp tục QC.",
                    "flowchart TD\nA[Part + LOT] --> B[Kho TP]\nA --> C[WIP/Sản xuất]\nA --> D[Khách trả]\nB --> E[Đối chiếu]\nC --> E\nD --> E\nE --> F[Snapshot ảnh hưởng]"),

                Topic(
                    "XuLyHangLoi.InitialQC",
                    "Xử lý hàng lỗi - Initial QC",
                    "Xác nhận kết quả kiểm tra ban đầu và phân tách OK, Rework, loại bỏ ban đầu một cách bảo toàn số lượng.",
                    "Phiếu đã QC định hướng và có snapshot LOT đầy đủ.",
                    "1. Nhập kết quả theo từng LOT.\n2. Nhập số lượng đã kiểm tra.\n3. Phân loại OK và NG.\n4. Trong NG, phân tách Rework và LoaiBoBanDau.\n5. Kiểm tra DaKiemTra = OK + NG.\n6. Kiểm tra NG = Rework + LoaiBoBanDau.\n7. Xác nhận Initial QC.",
                    "Tổng số lượng đã kiểm tra phải bằng tổng số lượng ảnh hưởng của snapshot. Mỗi LOT không được âm hoặc vượt số lượng ảnh hưởng.",
                    "Thiếu LOT; kiểm tra chưa đủ; OK/NG không khớp; Rework + loại bỏ không bằng NG; hướng xử lý không cho phép Rework nhưng lại nhập Rework.",
                    "Không xác nhận khi phương trình số lượng chưa cân bằng. Kiểm tra lại từng LOT trước khi xác nhận toàn phiếu.",
                    "flowchart TD\nA[Snapshot LOT] --> B[Kiểm tra từng LOT]\nB --> C[OK + NG]\nC --> D[NG = Rework + Loại bỏ ban đầu]\nD --> E[Confirm Initial QC]"),

                Topic(
                    "XuLyHangLoi.Rework",
                    "Xử lý hàng lỗi - Rework",
                    "Thực hiện xuất hàng Rework, giao sản xuất và QC kết quả Rework theo kế hoạch Initial QC.",
                    "Initial QC đã xác nhận và HuongXuLy cho phép Rework.",
                    "1. Xem kế hoạch Rework từ Initial QC.\n2. Xuất Rework theo tồn kho và kế hoạch.\n3. Không xuất lũy kế vượt SoLuongRework của Initial QC.\n4. Chỉ giao sản xuất sau khi đã xuất đủ kế hoạch.\n5. Theo dõi trạng thái DaXuatKhoRework và DaGiaoSanXuat.\n6. QC sau Rework với OK + NG đúng bằng số lượng Rework đã giao.",
                    "Rework là phương thức xử lý NG; không làm thay đổi nghĩa vụ Giao bù. Không tự tăng kế hoạch Rework từ số lượng tồn còn lại.",
                    "Chưa có Initial QC; không phải hướng Rework; xuất vượt kế hoạch; giao khi chưa xuất đủ; QC cuối không khớp số lượng đã giao.",
                    "Kiểm tra plan, số lượng đã xuất và đã giao theo phiếu. Nếu lệch, dừng và kiểm tra giao dịch Rework trước khi thao tác tiếp.",
                    "flowchart TD\nA[Initial QC Rework Plan] --> B[Xuất Rework]\nB --> C{Đã xuất đủ?}\nC -- Không --> B\nC -- Có --> D[Giao sản xuất]\nD --> E[QC sau Rework]\nE --> F[OK + NG = Rework]"),

                Topic(
                    "XuLyHangLoi.Disposition",
                    "Xử lý hàng lỗi - Disposition",
                    "Quản lý số lượng không thể phục hồi và kết quả NG sau Rework để xác định số lượng loại bỏ cuối cùng.",
                    "Initial QC hoặc QC sau Rework đã xác định số lượng NG không thể tiếp tục xử lý.",
                    "1. Lấy LoaiBoBanDau từ Initial QC.\n2. Lấy NG từ QC sau Rework nếu có Rework.\n3. Tính loại bỏ cuối = LoaiBoBanDau + NG sau Rework.\n4. Ghi nhận Disposition theo quy định nghiệp vụ.\n5. Kiểm tra tổng số lượng trước khi hoàn tất.",
                    "NG sau Rework là phần bổ sung vào loại bỏ; không được ghi đè hoặc thay thế LoaiBoBanDau.",
                    "Disposition thiếu phần NG sau Rework; tính trùng; số lượng loại bỏ vượt nguồn QC.",
                    "Đối chiếu Initial QC và QC sau Rework. Không nhập một số lượng loại bỏ tổng nếu không truy được nguồn cấu thành.",
                    "flowchart TD\nA[Initial QC] --> B[Loại bỏ ban đầu]\nC[QC sau Rework] --> D[NG sau Rework]\nB --> E[Disposition cuối]\nD --> E\nE --> F[Hoàn tất]"),

                Topic(
                    "XuLyHangLoi.GiaoBu",
                    "Xử lý hàng lỗi - Giao bù",
                    "Thực hiện nghĩa vụ giao bù độc lập với kết quả NG/Rework/Disposition và kiểm soát xuất hàng theo QR/FIFO.",
                    "Có nghĩa vụ giao bù được xác định từ đơn hàng/giao hàng hoặc nghiệp vụ khách hàng; tồn kho đủ điều kiện xuất.",
                    "1. Xác định số lượng phải giao bù từ nghĩa vụ giao hàng, không suy ra tự động từ NG.\n2. Chọn danh sách hàng đủ điều kiện.\n3. Quét QR từng thùng/lot.\n4. Kiểm tra Part, LOT, số lượng và trạng thái.\n5. Áp dụng FIFO theo cấu hình tồn kho.\n6. Cộng dồn số lượng đã giao bù.\n7. Không cho phép vượt nghĩa vụ giao bù hoặc vượt tồn kho hợp lệ.\n8. Hoàn tất giao bù theo workflow.",
                    "Giao bù độc lập với Rework. Một phiếu có thể có Giao bù mà không có Rework, hoặc có cả hai nếu nghiệp vụ yêu cầu.",
                    "QR không hợp lệ; LOT không đúng FIFO; thiếu tồn; quét trùng; giao vượt nghĩa vụ; giao sai Part.",
                    "Kiểm tra kế hoạch giao bù, FIFO candidate và lịch sử QR đã giao. Không sửa số lượng để vượt giới hạn.",
                    "flowchart TD\nA[Nghĩa vụ giao bù] --> B[Lập kế hoạch]\nB --> C[FIFO tồn kho]\nC --> D[Quét QR/thùng]\nD --> E{Hợp lệ?}\nE -- Không --> F[Từ chối scan]\nE -- Có --> G[Cộng dồn giao bù]\nG --> H{Đủ nghĩa vụ?}\nH -- Không --> D\nH -- Có --> I[Hoàn tất giao bù]"),

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
