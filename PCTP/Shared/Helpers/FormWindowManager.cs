using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PCTP.Shared.Helpers
{
    /// <summary>
    /// Quản lý các cửa sổ modeless của ứng dụng.
    /// Mặc định mỗi loại Form chỉ có một instance; các màn hình cần nhiều phiên
    /// (ví dụ Phiếu giao hàng) truyền instanceKey riêng theo khách hàng.
    /// </summary>
    public static class FormWindowManager
    {
        private static readonly Dictionary<string, Form> OpenForms =
            new Dictionary<string, Form>(StringComparer.OrdinalIgnoreCase);

        public static T ShowSingle<T>(
            IWin32Window owner,
            Func<T> factory,
            string instanceKey = null) where T : Form
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            string key = string.IsNullOrWhiteSpace(instanceKey)
                ? typeof(T).FullName
                : instanceKey.Trim();

            Form existing;
            if (OpenForms.TryGetValue(key, out existing))
            {
                if (existing != null && !existing.IsDisposed)
                {
                    BringToFront(existing);
                    return existing as T;
                }

                OpenForms.Remove(key);
            }

            T form = factory();
            if (form == null)
                throw new InvalidOperationException("Không thể khởi tạo cửa sổ " + typeof(T).Name + ".");

            OpenForms[key] = form;
            form.FormClosed += (sender, args) =>
            {
                Form registered;
                if (OpenForms.TryGetValue(key, out registered) && ReferenceEquals(registered, form))
                    OpenForms.Remove(key);
            };

            try
            {
                if (owner != null)
                    form.Show(owner);
                else
                    form.Show();

                BringToFront(form);
                return form;
            }
            catch
            {
                Form registered;
                if (OpenForms.TryGetValue(key, out registered) && ReferenceEquals(registered, form))
                    OpenForms.Remove(key);

                if (!form.IsDisposed)
                    form.Dispose();

                throw;
            }
        }

        private static void BringToFront(Form form)
        {
            if (form == null || form.IsDisposed)
                return;

            if (form.WindowState == FormWindowState.Minimized)
                form.WindowState = FormWindowState.Normal;

            if (!form.Visible)
                form.Show();

            form.BringToFront();
            form.Activate();
        }
    }
}
