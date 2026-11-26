using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Framework.Documents;

namespace PCTP.Framework.UiLayout
{
    /// <summary>Designer rules: open/list needs VIEW; save and create need EDIT on ADMIN.DOCUMENT_DESIGNER. SQL re-checks each.</summary>
    public sealed class UiMenuDesignerService
    {
        private readonly IUiMenuStore _store;
        private readonly IDocumentDesignerAuthorizer _auth;

        public UiMenuDesignerService(IUiMenuStore store, IDocumentDesignerAuthorizer auth)
        {
            if (store == null) throw new ArgumentNullException("store");
            if (auth == null) throw new ArgumentNullException("auth");
            _store = store;
            _auth = auth;
        }

        public bool CanEdit { get { return _auth.Has(DocumentDesignerPermissions.Edit); } }

        public IList<UiMenuProfileInfo> ListProfiles()
        {
            _auth.Demand(DocumentDesignerPermissions.View);
            return _store.ListProfiles(_auth.ActorUserId);
        }

        public IList<UiMenuRole> ListRoles()
        {
            _auth.Demand(DocumentDesignerPermissions.View);
            return _store.ListRoles(_auth.ActorUserId);
        }

        public UiMenuProfile Open(string profileCode)
        {
            _auth.Demand(DocumentDesignerPermissions.View);
            return _store.Load(_auth.ActorUserId, profileCode);
        }

        public void Create(string profileCode, string displayName)
        {
            _auth.Demand(DocumentDesignerPermissions.Edit);
            string code = (profileCode ?? string.Empty).Trim().ToUpperInvariant();
            if (!UiMenuValidator.IsValidProfileCode(code))
                throw new ArgumentException("Mã profile chỉ gồm A-Z, 0-9 và dấu gạch dưới (tối đa 40 ký tự).");
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Tên profile là bắt buộc.");
            _store.CreateProfile(_auth.ActorUserId, _auth.ActorCode, code, displayName.Trim());
        }

        /// <summary>
        /// Items the designer may place from approved UI screens, independent of the designer's own VIEW rights on their data
        /// sources. A user without the right simply never gets the item in the shell, and the shell drops the unknown key.
        /// </summary>
        public IList<UiMenuItemInfo> ListScreenItems()
        {
            _auth.Demand(DocumentDesignerPermissions.View);
            var all = _store.ListScreenItems(_auth.ActorUserId) ?? new List<UiMenuItemInfo>();
            var own = UiMenuScreenKey.For(UiLayoutModule.ComponentPoScreenCode);   // has its own shell item
            return all.Where(i => !string.Equals(i.ItemKey, own, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>Priority of the profile for users in several profiles (1 = first). Returns the new ConfigVersion.</summary>
        public int SetOrder(string profileCode, int expectedConfigVersion, int profileOrder)
        {
            _auth.Demand(DocumentDesignerPermissions.Edit);
            if (string.Equals(profileCode, "DEFAULT", StringComparison.Ordinal))
                throw new InvalidOperationException("Profile DEFAULT là menu dự phòng, không có thứ tự ưu tiên.");
            if (!UiMenuValidator.IsValidOrder(profileOrder))
                throw new ArgumentOutOfRangeException("profileOrder", "Thứ tự ưu tiên phải từ " + UiMenuValidator.MinOrder + " đến " + UiMenuValidator.MaxOrder + ".");
            return _store.SetOrder(_auth.ActorUserId, _auth.ActorCode, profileCode, expectedConfigVersion, profileOrder);
        }

        /// <summary>Deletes a profile (roles are freed, a copy stays in the audit trail). DEFAULT cannot be deleted.</summary>
        public void Delete(string profileCode, int expectedConfigVersion)
        {
            _auth.Demand(DocumentDesignerPermissions.Edit);
            if (string.Equals(profileCode, "DEFAULT", StringComparison.Ordinal))
                throw new InvalidOperationException("Không thể xóa profile DEFAULT (dùng \"Về menu chuẩn\" để làm rỗng).");
            _store.DeleteProfile(_auth.ActorUserId, _auth.ActorCode, profileCode, expectedConfigVersion);
        }

        /// <summary>Validates, then saves. Returns the new ConfigVersion.</summary>
        public int Save(UiMenuProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            _auth.Demand(DocumentDesignerPermissions.Edit);
            var errors = UiMenuValidator.Validate(profile.ProfileCode, profile.Nodes, profile.RoleCodes);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            return _store.Save(_auth.ActorUserId, _auth.ActorCode, profile, profile.ConfigVersion);
        }
    }
}
