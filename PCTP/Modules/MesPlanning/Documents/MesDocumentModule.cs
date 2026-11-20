using System;
using System.Collections.Generic;
using PCTP.Framework.Documents;

namespace PCTP.Modules.MesPlanning.Documents
{
    /// <summary>
    /// Startup registration point for MES documents (opt-in; existing forms stay the entry points).
    /// <see cref="Initialize"/> builds the runtime once and validates every definition against SQL metadata and the
    /// real schema. <see cref="GetRuntime"/> refuses to hand out a runtime whose validation failed (fail-closed).
    /// </summary>
    public static class MesDocumentModule
    {
        private static readonly object Gate = new object();
        private static DocumentRuntimeServices _runtime;
        private static DocumentStartupReport _report;
        private static string _initializationError;

        public static IEnumerable<DocumentFormDefinition> Definitions()
        {
            yield return ComponentDemandDefinition.Create();
        }

        /// <summary>Creates a new, unvalidated runtime. Prefer <see cref="GetRuntime"/> in application code.</summary>
        public static DocumentRuntimeServices CreateRuntime()
        {
            return DocumentRuntimeServices.CreateDefault(Definitions());
        }

        /// <summary>
        /// Builds and validates the runtime. Safe to call at startup after sign-in: it never throws, so a bad
        /// document configuration disables the document screens without stopping the application.
        /// </summary>
        public static DocumentStartupReport Initialize()
        {
            lock (Gate)
            {
                try
                {
                    var runtime = CreateRuntime();
                    var report = runtime.ValidateAll();
                    _runtime = runtime;
                    _report = report;
                    _initializationError = null;
                }
                catch (Exception ex)
                {
                    _runtime = null;
                    _report = null;
                    _initializationError = ex.GetBaseException().Message;
                }
                return _report;
            }
        }

        public static DocumentStartupReport LastReport
        {
            get { lock (Gate) return _report; }
        }

        /// <summary>True only when startup validation ran and the module passed.</summary>
        public static bool IsReady(string moduleCode)
        {
            lock (Gate)
            {
                if (_runtime == null || _report == null) return false;
                var result = _report.Find(moduleCode);
                return result != null && result.IsOk;
            }
        }

        public static string DescribeProblem(string moduleCode)
        {
            lock (Gate)
            {
                if (_initializationError != null) return "Không khởi tạo được Document Runtime: " + _initializationError;
                if (_report == null) return "Document Runtime chưa được khởi tạo.";
                var result = _report.Find(moduleCode);
                if (result == null) return "Chứng từ chưa được đăng ký: " + moduleCode;
                return result.IsOk ? null : "Cấu hình chứng từ " + moduleCode + " không hợp lệ: " + result.Message;
            }
        }

        /// <summary>The validated runtime for <paramref name="moduleCode"/>; throws when startup validation did not pass.</summary>
        public static DocumentRuntimeServices GetRuntime(string moduleCode)
        {
            lock (Gate)
            {
                string problem = DescribeProblem(moduleCode);
                if (problem != null) throw new InvalidOperationException(problem);
                return _runtime;
            }
        }
    }
}
