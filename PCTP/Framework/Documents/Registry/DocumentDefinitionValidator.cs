using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Fail-closed check of mutable SQL metadata against the trusted C# definition. It accepts only
    /// plain metadata; there is no API that takes SQL text or an executable object name.
    /// </summary>
    public static class DocumentDefinitionValidator
    {
        private const string AllowedFormatPunctuation = " #,.:/-%";

        public static void DemandMatchesRegistry(DocumentFormDefinition trusted, DocumentMetadata metadata)
        {
            if (trusted == null) throw new ArgumentNullException("trusted");
            if (metadata == null) throw new ArgumentNullException("metadata");
            try
            {
                Validate(trusted, metadata);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException("SQL document metadata is malformed: " + ex.Message, ex);
            }
        }

        private static void Validate(DocumentFormDefinition trusted, DocumentMetadata metadata)
        {
            string workflow = string.IsNullOrWhiteSpace(metadata.WorkflowCode)
                ? DocumentFormDefinition.NoWorkflow : metadata.WorkflowCode.Trim();
            if (!Same(trusted.ModuleCode, metadata.ModuleCode) ||
                !Same(trusted.HeaderView, DocumentIdentifiers.SqlObject(metadata.HeaderView, "HeaderView")) ||
                !Same(trusted.DetailView, DocumentIdentifiers.SqlObject(metadata.DetailView, "DetailView")) ||
                !Same(trusted.WorkflowCode, workflow))
                Fail("module, view or workflow binding does not match the trusted application registry");

            var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in metadata.Fields)
            {
                DocumentRegion region;
                if (!DocumentEditorCodes.TryParseRegion(field.RegionCode, out region))
                    Fail("unknown region '" + field.RegionCode + "' for field " + field.FieldCode);
                var registered = trusted.FindField(region, field.FieldCode);
                if (registered == null)
                    Fail("field '" + field.FieldCode + "' is not registered for " + region);
                if (!seenFields.Add(region + ":" + registered.FieldCode))
                    Fail("duplicate field metadata '" + field.FieldCode + "'");
                DocumentFieldType editor;
                if (!DocumentEditorCodes.TryParse(field.EditorCode, out editor) || !registered.AllowsEditor(editor))
                    Fail("editor '" + field.EditorCode + "' is not allowed for field " + field.FieldCode);
                if (field.Width.HasValue && (field.Width.Value < 20 || field.Width.Value > 2000))
                    Fail("width out of range for field " + field.FieldCode);
                if (!IsSafeDisplayFormat(field.DisplayFormat))
                    Fail("display format is not allowed for field " + field.FieldCode);
                if (!string.IsNullOrWhiteSpace(field.LookupCode))
                {
                    if (editor != DocumentFieldType.Lookup)
                        Fail("lookup code is only allowed on a LOOKUP editor (field " + field.FieldCode + ")");
                    if (!IsSafeLookupCode(field.LookupCode))
                        Fail("lookup code is malformed for field " + field.FieldCode);
                }
            }

            var seenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var command in metadata.Commands)
            {
                if (!trusted.AllowsCommand(command.CommandCode))
                    Fail("command '" + command.CommandCode + "' is not registered");
                if (!seenCommands.Add(command.CommandCode.Trim()))
                    Fail("duplicate command metadata '" + command.CommandCode + "'");
                // RequiredStatusCode can only restrict a command, and only when the trusted definition
                // says which header column carries the numeric status; otherwise it cannot be interpreted.
                if (command.RequiredStatusCode.HasValue && trusted.StatusField == null)
                    Fail("RequiredStatusCode on command '" + command.CommandCode +
                        "' cannot be interpreted because the document registers no StatusField");
            }
            if (trusted.Commands.Any(c => !seenCommands.Contains(c.CommandCode)))
                Fail("registered command is missing from SQL metadata");

            var seenLayouts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layout in metadata.Layouts)
            {
                DocumentRegion region;
                if (!DocumentEditorCodes.TryParseRegion(layout.LayoutCode, out region))
                    Fail("unknown layout '" + layout.LayoutCode + "'");
                if (!seenLayouts.Add(layout.LayoutCode.Trim()))
                    Fail("duplicate layout '" + layout.LayoutCode + "'");
            }
        }

        /// <summary>Lookup codes are names of approved lookups, never SQL: letters, digits, underscore and dot only.</summary>
        public static bool IsSafeLookupCode(string code)
        {
            try
            {
                DocumentIdentifiers.Code(code, "LookupCode");
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Display formats are data, so only a small punctuation/letter/digit set is accepted.</summary>
        public static bool IsSafeDisplayFormat(string format)
        {
            if (string.IsNullOrEmpty(format)) return true;
            if (format.Length > 40) return false;
            return format.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') || AllowedFormatPunctuation.IndexOf(c) >= 0);
        }

        /// <summary>
        /// Checks the trusted registry against the real SQL objects: every registered view exists and exposes every
        /// registered column with a compatible type, every command procedure exists, and its parameter list matches
        /// the declared one exactly (name, type, decimal precision/scale). Fail-closed on the first mismatch.
        /// </summary>
        public static void DemandMatchesSchema(DocumentFormDefinition trusted, IDocumentSchemaProbe probe)
        {
            if (trusted == null) throw new ArgumentNullException("trusted");
            if (probe == null) throw new ArgumentNullException("probe");

            CheckView(trusted, probe, trusted.HeaderView, DocumentRegion.Header);
            CheckView(trusted, probe, trusted.DetailView, DocumentRegion.Detail);

            foreach (var command in trusted.Commands)
            {
                var actual = probe.GetProcedureParameters(command.ProcedureName);
                if (actual == null)
                    FailSchema("procedure " + command.ProcedureName + " does not exist (command " + command.CommandCode + ")");
                var byName = Index(actual);
                foreach (var declared in command.Parameters)
                {
                    DocumentSqlColumn real;
                    if (!byName.TryGetValue(declared.Name, out real))
                        FailSchema("procedure " + command.ProcedureName + " has no parameter @" + declared.Name);
                    if (!string.Equals(real.TypeName, SqlTypeName(declared.DbType), StringComparison.OrdinalIgnoreCase))
                        FailSchema("parameter @" + declared.Name + " of " + command.ProcedureName + " is " + real.TypeName +
                            " but the registry declares " + SqlTypeName(declared.DbType));
                    if (declared.DbType == SqlDbType.Decimal &&
                        (real.Precision != declared.Precision || real.Scale != declared.Scale))
                        FailSchema("parameter @" + declared.Name + " of " + command.ProcedureName + " is decimal(" +
                            real.Precision + "," + real.Scale + ") but the registry declares decimal(" +
                            declared.Precision + "," + declared.Scale + ")");
                }
                // T-SQL parameter defaults are not visible in sys.parameters, so an undeclared parameter
                // can never be assumed optional.
                var unmapped = actual.FirstOrDefault(a => !command.Parameters.Any(d =>
                    string.Equals(d.Name, a.Name, StringComparison.OrdinalIgnoreCase)));
                if (unmapped != null)
                    FailSchema("procedure " + command.ProcedureName + " has parameter @" + unmapped.Name +
                        " that the registry does not declare");
            }
        }

        private static void CheckView(DocumentFormDefinition trusted, IDocumentSchemaProbe probe, string view, DocumentRegion region)
        {
            var columns = probe.GetViewColumns(view);
            if (columns == null) FailSchema("view " + view + " does not exist");
            var byName = Index(columns);
            foreach (var field in trusted.FieldsOf(region))
            {
                DocumentSqlColumn column;
                if (!byName.TryGetValue(field.FieldCode, out column))
                    FailSchema("view " + view + " has no column " + field.FieldCode);
                if (!IsCompatible(field.Type, column.TypeName))
                    FailSchema("column " + field.FieldCode + " of " + view + " is " + column.TypeName +
                        " which is not compatible with registered type " + field.Type);
            }
        }

        private static Dictionary<string, DocumentSqlColumn> Index(IEnumerable<DocumentSqlColumn> items)
        {
            var map = new Dictionary<string, DocumentSqlColumn>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items) map[(item.Name ?? string.Empty).TrimStart('@')] = item;
            return map;
        }

        private static readonly string[] TextTypes = { "varchar", "nvarchar", "char", "nchar", "text", "ntext" };
        private static readonly string[] NumberTypes =
            { "tinyint", "smallint", "int", "bigint", "decimal", "numeric", "float", "real", "money", "smallmoney" };
        private static readonly string[] DateTypes = { "date", "datetime", "datetime2", "smalldatetime", "datetimeoffset" };

        public static bool IsCompatible(DocumentFieldType type, string sqlType)
        {
            string t = (sqlType ?? string.Empty).ToLowerInvariant();
            switch (type)
            {
                case DocumentFieldType.Number: return NumberTypes.Contains(t);
                case DocumentFieldType.Date:
                case DocumentFieldType.DateTime: return DateTypes.Contains(t);
                case DocumentFieldType.Check: return t == "bit";
                default: return TextTypes.Contains(t); // Text, Memo, Lookup, Barcode, Qr
            }
        }

        public static string SqlTypeName(SqlDbType type)
        {
            switch (type)
            {
                case SqlDbType.BigInt: return "bigint";
                case SqlDbType.Int: return "int";
                case SqlDbType.SmallInt: return "smallint";
                case SqlDbType.TinyInt: return "tinyint";
                case SqlDbType.NVarChar: return "nvarchar";
                case SqlDbType.VarChar: return "varchar";
                case SqlDbType.NChar: return "nchar";
                case SqlDbType.Char: return "char";
                case SqlDbType.Decimal: return "decimal";
                case SqlDbType.Bit: return "bit";
                case SqlDbType.DateTime2: return "datetime2";
                case SqlDbType.DateTime: return "datetime";
                case SqlDbType.Date: return "date";
                case SqlDbType.UniqueIdentifier: return "uniqueidentifier";
                default: return type.ToString().ToLowerInvariant();
            }
        }

        private static void FailSchema(string reason)
        {
            throw new InvalidOperationException("SQL schema does not match the trusted registry: " + reason + ".");
        }

        private static bool Same(string a, string b)
        {
            return string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static void Fail(string reason)
        {
            throw new InvalidOperationException("SQL document metadata rejected: " + reason + ".");
        }
    }
}
