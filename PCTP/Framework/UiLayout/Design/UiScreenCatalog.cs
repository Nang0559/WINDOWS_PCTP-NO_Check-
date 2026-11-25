using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PCTP.Framework.UiLayout
{
    /// <summary>One approved, openable layout screen as reported by SQL for the signed-in user.</summary>
    public sealed class UiScreenInfo
    {
        public string ScreenCode { get; set; }
        public string Title { get; set; }
        public string SourceCode { get; set; }
        public string ViewFunctionCode { get; set; }
    }

    public interface IUiScreenCatalogStore
    {
        IList<UiScreenInfo> ListScreens(long actorUserId);
    }

    /// <summary>
    /// "Open UI screen by code": the only way the shell opens a screen it has no dedicated C# menu item for.
    /// A code is openable only when SQL lists it for the current user (approved source, active screen, VIEW right);
    /// the list is read fresh each time so deactivation or a revoked right takes effect without a restart.
    /// The code never reaches SQL text: it is validated here and passed as a parameter by the store.
    /// </summary>
    public sealed class UiScreenCatalog
    {
        private static readonly Regex CodePattern = new Regex("^[A-Z0-9_]{1,60}$", RegexOptions.CultureInvariant);
        private readonly IUiScreenCatalogStore _store;
        private readonly Func<long> _actorUserId;

        public UiScreenCatalog(IUiScreenCatalogStore store, Func<long> actorUserId)
        {
            if (store == null) throw new ArgumentNullException("store");
            if (actorUserId == null) throw new ArgumentNullException("actorUserId");
            _store = store;
            _actorUserId = actorUserId;
        }

        public static bool IsValidCode(string screenCode)
        {
            return screenCode != null && CodePattern.IsMatch(screenCode);
        }

        /// <summary>Screens the user may open, ordered by title. Rows with a malformed code, title or function are dropped.</summary>
        public IList<UiScreenInfo> List()
        {
            var rows = _store.ListScreens(_actorUserId()) ?? new List<UiScreenInfo>();
            return rows
                .Where(r => r != null && IsValidCode(r.ScreenCode)
                            && !string.IsNullOrWhiteSpace(r.Title)
                            && !string.IsNullOrWhiteSpace(r.ViewFunctionCode))
                .GroupBy(r => r.ScreenCode, StringComparer.Ordinal).Select(g => g.First())
                .OrderBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.ScreenCode, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>The listed screen for <paramref name="screenCode"/>; throws when the code is malformed or not listed.</summary>
        public UiScreenInfo Find(string screenCode)
        {
            string code = (screenCode ?? string.Empty).Trim();
            if (!IsValidCode(code))
                throw new ArgumentException("Mã màn hình không hợp lệ (chỉ gồm A-Z, 0-9 và dấu gạch dưới).", "screenCode");
            var found = List().FirstOrDefault(s => string.Equals(s.ScreenCode, code, StringComparison.Ordinal));
            if (found == null)
                throw new UnauthorizedAccessException("Màn hình " + code +
                    " không có trong danh sách đã duyệt, đã tắt hoặc bạn không có quyền xem.");
            return found;
        }
    }
}
