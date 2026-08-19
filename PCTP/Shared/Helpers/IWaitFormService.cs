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
    }
}
