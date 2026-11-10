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
    /// Mọi cửa sổ modeless đều được gắn cùng MỘT owner (Shell Main_APP). Windows luôn giữ form
    /// được sở hữu (owned) nằm trên owner của nó, nên nếu form B được mở với owner là form A thì
    /// bấm vào A không bao giờ đưa A lên trên B. Gắn chung owner Shell thì các form là "anh em":
    /// bấm/focus vào form nào, form đó nổi lên trên các form còn lại.
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
                IWin32Window shellOwner = ResolveSharedOwner(owner);
                if (shellOwner != null)
                    form.Show(shellOwner);
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

        /// <summary>
        /// Chuẩn hoá owner của cửa sổ modeless về Shell (Main_APP) để mọi form cùng cấp, không lồng nhau.
        /// - Owner là form MODAL đang mở (ShowDialog) thì giữ nguyên, nếu không form mới sẽ bị khoá bởi hộp thoại đó.
        /// - Còn lại: đi ngược chuỗi Owner lên gốc rồi dùng Shell nếu có.
        /// </summary>
        private static IWin32Window ResolveSharedOwner(IWin32Window owner)
        {
            Form start = owner as Form;
            if (start == null)
            {
                Control control = owner as Control;
                if (control != null)
                    start = control.FindForm();
            }

            if (start == null || start.IsDisposed)
                return FindShell() ?? owner;

            Form top = start;
            while (!top.Modal && top.Owner != null && !top.Owner.IsDisposed)
                top = top.Owner;

            if (top.Modal)
                return top;

            return FindShell() ?? top;
        }

        private static Form FindShell()
        {
            return Application.OpenForms
                .Cast<Form>()
                .FirstOrDefault(f => f is Main_APP && !f.IsDisposed);
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
