using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>Menu placement of a C#-registered screen. Presentation only: it never names a view, procedure or command.</summary>
    public sealed class DocumentMenuEntry
    {
        public string ModuleCode { get; set; }
        public string DisplayName { get; set; }
        public string MenuGroup { get; set; }
        public string MenuText { get; set; }
        public int SequenceNo { get; set; }
        public bool IsActive { get; set; }
        /// <summary>ConfigVersion of the screen definition when the entry was loaded (optimistic concurrency token).</summary>
        public int ConfigVersion { get; set; }
        public bool HasMenu { get { return !string.IsNullOrEmpty(MenuGroup); } }
    }

    public static class DocumentMenuGroups
    {
        public static readonly IReadOnlyList<string> All = new[] { "NHAPKHO", "KHO", "GIAOHANG", "BAOCAO", "CONGCU" };
        public static bool IsAllowed(string group)
        {
            return group != null && All.Contains(group.Trim(), StringComparer.Ordinal);
        }
    }

    /// <summary>Calls migration 054 procedures; they re-check RBAC, ConfigVersion and write audit.</summary>
    public interface IDocumentMenuStore
    {
        IReadOnlyList<DocumentMenuEntry> List(long actorUserId);
        DocumentDesignSaveResult Save(long actorUserId, string actorCode, DocumentMenuEntry entry, int expectedConfigVersion);
    }

    /// <summary>Menu management for PCTP UI Designer. Only screens already in the C# registry can be placed.</summary>
    public sealed class DocumentMenuDesignerService
    {
        private readonly IDocumentRegistry _registry;
        private readonly IDocumentMenuStore _store;
        private readonly IDocumentDesignerAuthorizer _authorizer;
        private readonly Action _afterChange;

        public DocumentMenuDesignerService(IDocumentRegistry registry, IDocumentMenuStore store,
            IDocumentDesignerAuthorizer authorizer, Action afterChange = null)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (store == null) throw new ArgumentNullException("store");
            if (authorizer == null) throw new ArgumentNullException("authorizer");
            _registry = registry;
            _store = store;
            _authorizer = authorizer;
            _afterChange = afterChange;
        }

        public IReadOnlyList<DocumentMenuEntry> List()
        {
            _authorizer.Demand(DocumentDesignerPermissions.View);
            var registered = new HashSet<string>(_registry.All.Select(d => d.ModuleCode), StringComparer.OrdinalIgnoreCase);
            return _store.List(_authorizer.ActorUserId)
                .Where(e => registered.Contains(e.ModuleCode)).ToList();
        }

        public DocumentDesignSaveResult Save(DocumentMenuEntry entry)
        {
            _authorizer.Demand(DocumentDesignerPermissions.Edit);
            if (entry == null) throw new ArgumentNullException("entry");
            var trusted = _registry.GetRequired(entry.ModuleCode);
            if (!DocumentMenuGroups.IsAllowed(entry.MenuGroup))
                throw new InvalidOperationException("Nhóm menu không hợp lệ.");
            string text = (entry.MenuText ?? string.Empty).Trim();
            if (text.Length == 0 || text.Length > 160 || text.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
                throw new InvalidOperationException("Tên menu không hợp lệ.");
            if (entry.SequenceNo < 0 || entry.SequenceNo > 10000)
                throw new InvalidOperationException("Thứ tự menu phải trong khoảng 0..10000.");

            var toSave = new DocumentMenuEntry
            {
                ModuleCode = trusted.ModuleCode, MenuGroup = entry.MenuGroup.Trim(), MenuText = text,
                SequenceNo = entry.SequenceNo, IsActive = entry.IsActive, ConfigVersion = entry.ConfigVersion
            };
            var result = _store.Save(_authorizer.ActorUserId, _authorizer.ActorCode, toSave, entry.ConfigVersion);
            if (_afterChange != null) _afterChange();
            return result;
        }
    }
}