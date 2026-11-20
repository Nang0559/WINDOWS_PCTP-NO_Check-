using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Application service behind PCTP UI Designer. It can only change layer-2 presentation of documents that the
    /// code registry already allows. Every operation is authorized here AND again in the SQL procedure; the client-side
    /// checks exist to fail early with a clear message, the SQL side is the enforcement point.
    /// </summary>
    public sealed class DocumentDesignerService
    {
        private readonly IDocumentRegistry _registry;
        private readonly IDocumentMetadataRepository _metadata;
        private readonly IDocumentMetadataWriter _writer;
        private readonly IDocumentDesignerAuthorizer _authorizer;
        private readonly Action _afterChange;

        public DocumentDesignerService(IDocumentRegistry registry, IDocumentMetadataRepository metadata,
            IDocumentMetadataWriter writer, IDocumentDesignerAuthorizer authorizer, Action afterChange = null)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (metadata == null) throw new ArgumentNullException("metadata");
            if (writer == null) throw new ArgumentNullException("writer");
            if (authorizer == null) throw new ArgumentNullException("authorizer");
            _registry = registry;
            _metadata = metadata;
            _writer = writer;
            _authorizer = authorizer;
            _afterChange = afterChange;
        }

        public bool CanEdit { get { return _authorizer.Has(DocumentDesignerPermissions.Edit); } }
        public bool CanApprove { get { return _authorizer.Has(DocumentDesignerPermissions.Approve); } }

        public IReadOnlyList<DocumentDesignerModuleInfo> ListModules()
        {
            _authorizer.Demand(DocumentDesignerPermissions.View);
            var list = new List<DocumentDesignerModuleInfo>();
            foreach (var trusted in _registry.All)
            {
                var info = new DocumentDesignerModuleInfo { ModuleCode = trusted.ModuleCode, DisplayName = trusted.ModuleCode };
                try
                {
                    var meta = _metadata.Load(trusted.ModuleCode);
                    if (meta != null)
                    {
                        info.HasMetadata = true;
                        info.ConfigVersion = meta.ConfigVersion;
                        info.DisplayName = string.IsNullOrWhiteSpace(meta.DisplayName) ? trusted.ModuleCode : meta.DisplayName;
                        info.MissingFieldCount = MissingFields(trusted, meta).Count;
                        info.MissingCommandCount = MissingCommands(trusted, meta).Count;
                        DocumentDefinitionValidator.DemandMatchesRegistry(trusted, meta);
                    }
                }
                catch (Exception ex)
                {
                    info.Problem = ex.GetBaseException().Message;
                }
                list.Add(info);
            }
            return list;
        }

        public DocumentDesignerSession Open(string moduleCode)
        {
            _authorizer.Demand(DocumentDesignerPermissions.View);
            var trusted = _registry.GetRequired(moduleCode);
            var meta = _metadata.Load(trusted.ModuleCode);
            var session = new DocumentDesignerSession { Trusted = trusted };
            if (meta == null)
            {
                session.MissingFields = trusted.Fields.Select(f => Key(f.Region, f.FieldCode)).ToList();
                session.MissingCommands = trusted.Commands.Select(c => c.CommandCode).ToList();
                return session;
            }
            session.Loaded = Clone(meta);
            session.LoadedConfigVersion = meta.ConfigVersion;
            session.MissingFields = MissingFields(trusted, meta);
            session.MissingCommands = MissingCommands(trusted, meta);
            return session;
        }

        /// <summary>Validated preview of a draft exactly as the runtime would resolve it. Throws when the draft is invalid.</summary>
        public ResolvedDocument Preview(DocumentDesignerSession session, DocumentMetadata draft)
        {
            if (session == null) throw new ArgumentNullException("session");
            if (draft == null) throw new ArgumentNullException("draft");
            DocumentDefinitionValidator.DemandMatchesRegistry(session.Trusted, draft);
            DemandEveryRegionHasVisibleField(draft);
            return DocumentLayoutResolver.Resolve(session.Trusted, draft);
        }

        public DocumentDesignSaveResult Save(DocumentDesignerSession session, DocumentMetadata draft)
        {
            _authorizer.Demand(DocumentDesignerPermissions.Edit);
            if (session == null) throw new ArgumentNullException("session");
            if (draft == null) throw new ArgumentNullException("draft");
            if (!session.HasMetadata)
                throw new InvalidOperationException("Module chưa có cấu hình giao diện trong SQL. Hãy dùng 'Tạo giao diện' (quyền APPROVE) trước.");
            var trusted = session.Trusted;
            if (!string.Equals(draft.ModuleCode, trusted.ModuleCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cấu hình không thuộc màn hình đang thiết kế.");

            // The designer edits presentation only. Anything else must equal what was loaded.
            DemandOnlyPresentationChanged(session.Loaded, draft);
            // Same fail-closed validator the runtime uses at startup: unknown field/command, editor outside the
            // allow-list, unsafe format, width range, status restriction.
            DocumentDefinitionValidator.DemandMatchesRegistry(trusted, draft);
            DemandEveryRegionHasVisibleField(draft);
            DocumentLayoutResolver.Resolve(trusted, draft);

            var result = _writer.Save(_authorizer.ActorUserId, _authorizer.ActorCode, draft, session.LoadedConfigVersion);
            if (_afterChange != null) _afterChange();
            return result;
        }

        /// <summary>
        /// Creates SQL presentation metadata for a C#-registered module, or adds only missing presentation rows. The seed is built from the
        /// trusted registry, never from user input: there is no API that accepts a view, procedure or command name.
        /// </summary>
        public DocumentDesignSaveResult Provision(string moduleCode)
        {
            _authorizer.Demand(DocumentDesignerPermissions.Approve);
            var trusted = _registry.GetRequired(moduleCode);
            var existing = _metadata.Load(trusted.ModuleCode);
            string displayName = existing != null && !string.IsNullOrWhiteSpace(existing.DisplayName)
                ? existing.DisplayName : trusted.ModuleCode;
            var seed = BuildSeed(trusted, displayName);
            DocumentDefinitionValidator.DemandMatchesRegistry(trusted, seed);
            var result = _writer.Provision(_authorizer.ActorUserId, _authorizer.ActorCode, seed);
            if (_afterChange != null) _afterChange();
            return result;
        }

        public DataTable ListAudit(string moduleCode, int top = 200)
        {
            _authorizer.Demand(DocumentDesignerPermissions.View);
            var trusted = _registry.GetRequired(moduleCode);
            return _writer.ListAudit(_authorizer.ActorUserId, trusted.ModuleCode, top);
        }

        internal static DocumentMetadata BuildSeed(DocumentFormDefinition trusted, string displayName)
        {
            var seed = new DocumentMetadata
            {
                ModuleCode = trusted.ModuleCode,
                HeaderView = Unbracket(trusted.HeaderView),
                DetailView = Unbracket(trusted.DetailView),
                WorkflowCode = trusted.WorkflowCode,
                DisplayName = displayName,
                ConfigVersion = 1
            };
            foreach (var region in new[] { DocumentRegion.Header, DocumentRegion.Detail })
            {
                int sequence = 0;
                foreach (var field in trusted.FieldsOf(region))
                {
                    sequence += 10;
                    seed.Fields.Add(new DocumentFieldMetadata
                    {
                        FieldCode = field.FieldCode,
                        RegionCode = DocumentEditorCodes.ToRegionCode(region),
                        Caption = field.FieldCode,
                        SequenceNo = sequence,
                        EditorCode = DocumentEditorCodes.ToCode(field.Type),
                        IsVisible = true,
                        IsReadOnly = true
                    });
                }
            }
            int commandSequence = 0;
            foreach (var command in trusted.Commands)
            {
                commandSequence += 10;
                seed.Commands.Add(new DocumentCommandMetadata
                {
                    CommandCode = command.CommandCode,
                    Caption = command.CommandCode.Replace('_', ' '),
                    SequenceNo = commandSequence,
                    IsVisible = true
                });
            }
            return seed;
        }

        internal static DocumentMetadata Clone(DocumentMetadata source)
        {
            var copy = new DocumentMetadata
            {
                ModuleCode = source.ModuleCode, HeaderView = source.HeaderView, DetailView = source.DetailView,
                WorkflowCode = source.WorkflowCode, DisplayName = source.DisplayName, ConfigVersion = source.ConfigVersion
            };
            foreach (var f in source.Fields)
                copy.Fields.Add(new DocumentFieldMetadata
                {
                    FieldCode = f.FieldCode, RegionCode = f.RegionCode, Caption = f.Caption, SequenceNo = f.SequenceNo,
                    Width = f.Width, DisplayFormat = f.DisplayFormat, EditorCode = f.EditorCode, IsVisible = f.IsVisible,
                    IsReadOnly = f.IsReadOnly, IsRequired = f.IsRequired, LookupCode = f.LookupCode
                });
            foreach (var c in source.Commands)
                copy.Commands.Add(new DocumentCommandMetadata
                {
                    CommandCode = c.CommandCode, Caption = c.Caption, SequenceNo = c.SequenceNo,
                    IsVisible = c.IsVisible, RequiredStatusCode = c.RequiredStatusCode
                });
            foreach (var l in source.Layouts)
                copy.Layouts.Add(new DocumentLayoutMetadata
                {
                    LayoutCode = l.LayoutCode, RegionCode = l.RegionCode, Caption = l.Caption,
                    SequenceNo = l.SequenceNo, IsVisible = l.IsVisible
                });
            return copy;
        }

        private static void DemandOnlyPresentationChanged(DocumentMetadata loaded, DocumentMetadata draft)
        {
            if (!Same(loaded.HeaderView, draft.HeaderView) || !Same(loaded.DetailView, draft.DetailView) ||
                !Same(loaded.WorkflowCode, draft.WorkflowCode))
                throw new InvalidOperationException("Designer không được đổi view hoặc workflow của chứng từ.");
            if (draft.Fields.Count != loaded.Fields.Count || draft.Commands.Count != loaded.Commands.Count ||
                draft.Layouts.Count != loaded.Layouts.Count)
                throw new InvalidOperationException("Designer không được thêm hoặc bớt cột, lệnh hay vùng.");

            foreach (var field in draft.Fields)
            {
                var before = loaded.Fields.FirstOrDefault(f => Same(f.RegionCode, field.RegionCode) && Same(f.FieldCode, field.FieldCode));
                if (before == null)
                    throw new InvalidOperationException("Designer không được khai báo cột mới: " + field.FieldCode);
                if (!Same(before.EditorCode, field.EditorCode) || before.IsReadOnly != field.IsReadOnly ||
                    before.IsRequired != field.IsRequired || !Same(before.LookupCode, field.LookupCode))
                    throw new InvalidOperationException("Designer không được đổi editor, chỉ-đọc, bắt-buộc hay lookup của cột " + field.FieldCode);
            }
            foreach (var command in draft.Commands)
            {
                var before = loaded.Commands.FirstOrDefault(c => Same(c.CommandCode, command.CommandCode));
                if (before == null)
                    throw new InvalidOperationException("Designer không được khai báo lệnh mới: " + command.CommandCode);
                if (before.RequiredStatusCode != command.RequiredStatusCode)
                    throw new InvalidOperationException("Designer không được đổi điều kiện trạng thái của lệnh " + command.CommandCode);
            }
            foreach (var layout in draft.Layouts)
                if (!loaded.Layouts.Any(l => Same(l.LayoutCode, layout.LayoutCode)))
                    throw new InvalidOperationException("Designer không được khai báo vùng mới: " + layout.LayoutCode);
        }

        private static void DemandEveryRegionHasVisibleField(DocumentMetadata draft)
        {
            foreach (var group in draft.Fields.GroupBy(f => (f.RegionCode ?? string.Empty).ToUpperInvariant()))
                if (!group.Any(f => f.IsVisible))
                    throw new InvalidOperationException("Vùng " + group.Key + " phải còn ít nhất một cột hiển thị.");
        }

        private static List<string> MissingFields(DocumentFormDefinition trusted, DocumentMetadata meta)
        {
            var have = new HashSet<string>(meta.Fields.Select(f => (f.RegionCode ?? string.Empty).ToUpperInvariant() + ":" + f.FieldCode),
                StringComparer.OrdinalIgnoreCase);
            return trusted.Fields.Where(f => !have.Contains(Key(f.Region, f.FieldCode)))
                .Select(f => Key(f.Region, f.FieldCode)).ToList();
        }

        private static List<string> MissingCommands(DocumentFormDefinition trusted, DocumentMetadata meta)
        {
            var have = new HashSet<string>(meta.Commands.Select(c => c.CommandCode), StringComparer.OrdinalIgnoreCase);
            return trusted.Commands.Where(c => !have.Contains(c.CommandCode)).Select(c => c.CommandCode).ToList();
        }

        private static string Key(DocumentRegion region, string fieldCode)
        {
            return DocumentEditorCodes.ToRegionCode(region) + ":" + fieldCode;
        }

        private static string Unbracket(string bracketed)
        {
            return bracketed.Replace("[", string.Empty).Replace("]", string.Empty);
        }

        private static bool Same(string a, string b)
        {
            return string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
