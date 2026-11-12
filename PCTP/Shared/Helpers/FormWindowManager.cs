using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Shared.Helpers
{
    /// <summary>
    /// Quản lý các cửa sổ modeless của ứng dụng.
    /// Mặc định mỗi loại Form chỉ có một instance; các màn hình cần nhiều phiên
    /// (ví dụ Phiếu giao hàng) truyền instanceKey riêng theo khách hàng.
    ///
    /// Cửa sổ modeless được mở KHÔNG có owner (top-level độc lập). Windows luôn giữ form được sở hữu
    /// (owned) nằm trên owner của nó: nếu gắn owner (dù là form khác hay Shell Main_APP) thì bấm vào
    /// owner không bao giờ đưa owner lên trên form con, người dùng phải hạ form con xuống mới thấy.
    /// Không owner thì mọi form (kể cả Shell) là "anh em": bấm vào form nào, form đó nổi lên trên.
    /// Chỉ khi form đang mở từ một hộp thoại MODAL (ShowDialog) mới giữ owner là hộp thoại đó,
    /// nếu không form mới sẽ nằm dưới/bị khoá bởi hộp thoại.
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
                IWin32Window modalOwner = ResolveModalOwner(owner);
                if (modalOwner != null)
                    form.Show(modalOwner);   // mở từ hộp thoại modal: phải nằm trên hộp thoại đó
                else
                    form.Show();             // không owner: click vào form nào form đó nổi lên trên

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

        /// <summary>
        /// Owner cho cửa sổ modeless: chỉ trả về form MODAL đang mở (đi ngược chuỗi Owner lên gốc),
        /// còn lại trả null để form mới là top-level độc lập (không lồng, không bị kẹt dưới form khác).
        /// </summary>
        private static IWin32Window ResolveModalOwner(IWin32Window owner)
        {
            Form start = owner as Form;
            if (start == null)
            {
                Control control = owner as Control;
                if (control != null)
                    start = control.FindForm();
            }

            if (start == null || start.IsDisposed)
                return null;

            Form top = start;
            while (!top.Modal && top.Owner != null && !top.Owner.IsDisposed)
                top = top.Owner;

            return top.Modal ? top : null;
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
