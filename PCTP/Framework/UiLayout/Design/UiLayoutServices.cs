using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using PCTP.Framework.Documents;
using PCTP.Infrastructure.Security;
using PCTP.Security;

namespace PCTP.Framework.UiLayout
{
    /// <summary>RBAC action + SITE scope for reading an approved view. Same guard as every other screen; SQL re-checks on load.</summary>
    public interface IUiScreenAuthorizer
    {
        void DemandView(string functionCode, string siteCode);
    }

    public sealed class SessionUiScreenAuthorizer : IUiScreenAuthorizer
    {
        public void DemandView(string functionCode, string siteCode)
        {
            AuthorizationGuard.DemandScope(functionCode, "SITE", siteCode, siteCode);
        }
    }

    /// <summary>Designer rules: Open/preview needs VIEW; Save needs EDIT; declaring sources and creating screens needs APPROVE.</summary>
    public sealed class UiLayoutDesignerService
    {
        private readonly IUiLayoutStore _store;
        private readonly IDocumentDesignerAuthorizer _auth;

        public UiLayoutDesignerService(IUiLayoutStore store, IDocumentDesignerAuthorizer auth)
        {
            if (store == null) throw new ArgumentNullException("store");
            if (auth == null) throw new ArgumentNullException("auth");
            _store = store;
            _auth = auth;
        }

        public bool CanEdit { get { return _auth.Has(DocumentDesignerPermissions.Edit); } }
        public bool CanApprove { get { return _auth.Has(DocumentDesignerPermissions.Approve); } }

        public IList<UiDataSourceInfo> ListSources()
        {
            _auth.Demand(DocumentDesignerPermissions.View);
            return _store.ListSources(_auth.ActorUserId);
        }

        public IList<UiSourceColumn> GetSourceColumns(string sourceCode)
        {
            _auth.Demand(DocumentDesignerPermissions.View);
            return _store.GetSourceColumns(_auth.ActorUserId, sourceCode);
        }

        public UiScreenLoadResult Open(string screenCode)
        {
            _auth.Demand(DocumentDesignerPermissions.View);
            return _store.Load(_auth.ActorUserId, screenCode);
        }

        public IList<string> ListCandidateViews()
        {
            _auth.Demand(DocumentDesignerPermissions.Approve);
            return _store.ListCandidateViews(_auth.ActorUserId);
        }

        public void ApproveSource(UiDataSourceInfo source)
        {
            _auth.Demand(DocumentDesignerPermissions.Approve);
            _store.ApproveSource(_auth.ActorUserId, _auth.ActorCode, source);
        }

        public void CreateScreen(string screenCode, string title, string sourceCode)
        {
            _auth.Demand(DocumentDesignerPermissions.Approve);
            _store.CreateScreen(_auth.ActorUserId, _auth.ActorCode, screenCode, title, sourceCode);
        }

        /// <summary>Validates, then saves. Returns the new ConfigVersion.</summary>
        public int Save(UiScreenLoadResult session)
        {
            if (session == null) throw new ArgumentNullException("session");
            _auth.Demand(DocumentDesignerPermissions.Edit);
            var errors = UiLayoutValidator.Validate(session.Layout, session.Catalog);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            return _store.Save(_auth.ActorUserId, _auth.ActorCode, session.Layout, session.Layout.ConfigVersion);
        }
    }

    /// <summary>
    /// Runtime of a layout screen: load layout (SQL checks VIEW), demand Site-scoped VIEW on the main view and on every
    /// child view, re-validate against the catalogue, then build parameterized reads. Read-only by construction.
    /// </summary>
    public sealed class UiScreenRuntime
    {
        private readonly IUiLayoutStore _store;
        private readonly IUiScreenAuthorizer _auth;
        private readonly SqlUiDataReader _reader;
        private readonly Func<long> _actor;

        public UiScreenRuntime(IUiLayoutStore store, IUiScreenAuthorizer auth, SqlUiDataReader reader, Func<long> actorUserId)
        {
            _store = store; _auth = auth; _reader = reader; _actor = actorUserId;
        }

        public static UiScreenRuntime CreateDefault()
        {
            return new UiScreenRuntime(new SqlUiLayoutStore(), new SessionUiScreenAuthorizer(), new SqlUiDataReader(),
                () => new SessionDocumentDesignerAuthorizer().ActorUserId);
        }

        public UiScreenLoadResult Open(string screenCode, string siteCode)
        {
            var loaded = _store.Load(_actor(), screenCode);
            var errors = UiLayoutValidator.Validate(loaded.Layout, loaded.Catalog);
            if (errors.Count > 0)
                throw new InvalidOperationException("Layout màn hình " + screenCode + " không còn khớp view đã duyệt:" +
                    Environment.NewLine + string.Join(Environment.NewLine, errors));
            _auth.DemandView(loaded.Layout.ViewFunctionCode, siteCode);
            foreach (var child in loaded.Layout.Children) _auth.DemandView(child.ViewFunctionCode, siteCode);
            return loaded;
        }

        public UiQuickFilter DefaultFilter(UiScreenLayout layout)
        {
            return layout.Filters.FirstOrDefault(f => f.IsDefault);
        }

        public DataTable ReadMainPage(UiScreenLoadResult screen, string siteCode, UiQuickFilter filter, int pageIndex, out long total)
        {
            total = _reader.Count(UiQueryBuilder.BuildMainCount(screen.Layout, screen.Catalog, siteCode, filter, DateTime.Today));
            return _reader.Query(UiQueryBuilder.BuildMainPage(screen.Layout, screen.Catalog, siteCode, filter, DateTime.Today, pageIndex));
        }

        public DataTable ReadChild(UiScreenLoadResult screen, UiChildLayout child, string siteCode, object parentKey)
        {
            return _reader.Query(UiQueryBuilder.BuildChild(screen.Layout, child, screen.Catalog, siteCode, parentKey));
        }
    }
}