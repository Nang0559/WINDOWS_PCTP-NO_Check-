using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Framework.Documents
{
    public enum DocumentFilterOp { Equals, Contains, GreaterOrEqual, LessOrEqual }

    /// <summary>One filter on a registered header field. The value is always sent as a SQL parameter.</summary>
    public sealed class DocumentFilter
    {
        public string FieldCode { get; set; }
        public DocumentFilterOp Op { get; set; }
        public object Value { get; set; }

        public DocumentFilter() { }

        public DocumentFilter(string fieldCode, DocumentFilterOp op, object value)
        {
            FieldCode = fieldCode;
            Op = op;
            Value = value;
        }
    }

    /// <summary>Paged, filtered and ordered request for document headers. Field names are validated against the registry.</summary>
    public sealed class DocumentListQuery
    {
        public const int MaxPageSize = 500;
        public const int DefaultPageSize = 100;
        public const int MaxFilters = 8;

        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        /// <summary>Registered header field; null orders by the document key.</summary>
        public string OrderByField { get; set; }
        public bool Descending { get; set; }
        public List<DocumentFilter> Filters { get; private set; }

        public DocumentListQuery()
        {
            PageSize = DefaultPageSize;
            Descending = true;
            Filters = new List<DocumentFilter>();
        }

        /// <summary>Newest documents first, no filter; used by callers that only need "the latest N".</summary>
        public static DocumentListQuery Latest(int top)
        {
            return new DocumentListQuery { PageSize = Math.Max(1, Math.Min(MaxPageSize, top)) };
        }
    }

    /// <summary>One page of headers plus the total number of rows matching the same filters and scope.</summary>
    public sealed class DocumentPage
    {
        public DataTable Rows { get; set; }
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int PageCount
        {
            get { return PageSize <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize; }
        }
    }

    /// <summary>Result of resolving which departments a user may read (when the document has a DepartmentField).</summary>
    public sealed class DocumentDataScope
    {
        public static readonly DocumentDataScope AllDepartments = new DocumentDataScope(true, null);

        public bool IsAllDepartments { get; private set; }
        public IReadOnlyCollection<string> Departments { get; private set; }

        public DocumentDataScope(bool allDepartments, IEnumerable<string> departments)
        {
            IsAllDepartments = allDepartments;
            Departments = new List<string>(departments ?? new string[0]).AsReadOnly();
        }
    }
}
