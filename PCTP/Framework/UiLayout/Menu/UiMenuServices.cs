using System;
using System.Collections.Generic;
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
