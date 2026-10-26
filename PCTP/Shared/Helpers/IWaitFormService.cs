using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Helpers
{
    public interface IWaitFormService
    {
        void Run(
            Action action,
            string caption = "Đang xử lý...");

        T Run<T>(
            Func<T> func,
            string caption = "Đang xử lý...");

        Task RunAsync(
            Func<Task> action,
            string caption = "Đang xử lý...");

        Task<T> RunAsync<T>(
            Func<Task<T>> func,
            string caption = "Đang xử lý...");

        // ── Bật/tắt thủ công — dùng cho View nào còn giữ pattern
        // ShowLoading(bool)/ShowLoading(false) theo cặp thay vì bọc quanh 1 Action
        // (VD: IHVNView.ShowLoading — nhiều điểm gọi rải rác trong Presenter,
        // không tiện gói lại thành 1 Action duy nhất). Show()/Close() PHẢI gọi
        // theo cặp, không tự có timeout — caller (View) tự chịu trách nhiệm đóng.
        void Show(string caption = "Đang xử lý...");
        void SetCaption(string caption);
        void Close();
    }
}
