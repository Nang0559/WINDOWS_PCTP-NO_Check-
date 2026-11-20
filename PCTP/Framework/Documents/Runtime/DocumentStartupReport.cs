using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Framework.Documents
{
    public sealed class DocumentStartupResult
    {
        public string ModuleCode { get; set; }
        public bool IsOk { get; set; }
        /// <summary>Why the module failed, or a non-fatal note (for example "no SQL metadata, using code defaults").</summary>
        public string Message { get; set; }
    }

    /// <summary>Outcome of checking every registered document against SQL metadata and the real schema.</summary>
    public sealed class DocumentStartupReport
    {
        private readonly List<DocumentStartupResult> _results = new List<DocumentStartupResult>();

        public IReadOnlyList<DocumentStartupResult> Results { get { return _results.AsReadOnly(); } }
        public bool AllOk { get { return _results.All(r => r.IsOk); } }

        internal void Add(string moduleCode, bool ok, string message)
        {
            _results.Add(new DocumentStartupResult { ModuleCode = moduleCode, IsOk = ok, Message = message });
        }

        public DocumentStartupResult Find(string moduleCode)
        {
            return _results.FirstOrDefault(r => string.Equals(r.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase));
        }

        public override string ToString()
        {
            return string.Join(Environment.NewLine, _results.Select(r =>
                (r.IsOk ? "OK   " : "FAIL ") + r.ModuleCode + (string.IsNullOrEmpty(r.Message) ? string.Empty : ": " + r.Message)));
        }
    }
}
