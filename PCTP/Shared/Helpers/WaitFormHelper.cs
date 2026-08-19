using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Shared.Helpers
{
    public class WaitFormService : IWaitFormService
    {
        private readonly Form _owner;

        public WaitFormService(Form owner)
        {
            _owner = owner;
        }

        public void Run(
            Action action,
            string caption = "Đang xử lý...")
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            RunWithLoadingSync(
                () =>
                {
                    action();
                    return true;
                },
                caption);
        }

        public T Run<T>(
            Func<T> func,
            string caption = "Đang xử lý...")
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            return RunWithLoadingSync(func, caption);
        }

        public async Task RunAsync(
            Func<Task> action,
            string caption = "Đang xử lý...")
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            await RunWithLoadingAsync(
                async () =>
                {
                    await action();
                    return true;
                },
                caption);
        }

        public Task<T> RunAsync<T>(
            Func<Task<T>> func,
            string caption = "Đang xử lý...")
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            return RunWithLoadingAsync(func, caption);
        }

        private T RunWithLoadingSync<T>(
            Func<T> action,
            string caption)
        {
            // ĐƯA IMPLEMENTATION RunWithLoadingSync
            // HIỆN TẠI CỦA FormTraHangNGNew VÀO ĐÂY.

            throw new NotImplementedException();
        }

        private async Task<T> RunWithLoadingAsync<T>(
            Func<Task<T>> action,
            string caption)
        {
            // Implementation async tương ứng.

            throw new NotImplementedException();
        }
    }
}
