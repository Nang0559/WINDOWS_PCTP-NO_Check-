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
        /// Document Designer over the same trusted registry. After a save the runtime is rebuilt and revalidated, so a
        /// presentation change that disagrees with the registry disables the screen instead of being applied silently.
        /// </summary>
        public static DocumentDesignerService CreateDesigner()
        {
            return new DocumentDesignerService(
                new DocumentRegistry(Definitions()),
                new SqlDocumentMetadataRepository(),
                new SqlDocumentMetadataWriter(),
                new SessionDocumentDesignerAuthorizer(),
                () => Initialize());
        }

        /// <summary>Menu placement designer over the same C# Trusted Registry (presentation only).</summary>
        public static DocumentMenuDesignerService CreateMenuDesigner()
        {
            return new DocumentMenuDesignerService(
                new DocumentRegistry(Definitions()),
                new SqlDocumentMenuStore(),
                new SessionDocumentDesignerAuthorizer());
        }

        /// <summary>
        /// Menu entries the shell may apply. SQL supplies placement only; registry and per-screen VIEW permission
        /// are checked in C#. On failure the shell retains its standard menu.
        /// </summary>
        public static IReadOnlyList<DocumentMenuEntry> ResolveShellMenu(Func<string, bool> canView)
        {
            try
            {
                return DocumentMenuProvider.Resolve(
                    new DocumentRegistry(Definitions()), new SqlDocumentMenuStore(), canView);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("Document menu configuration skipped: " + ex.GetBaseException().Message);
                return new DocumentMenuEntry[0];
            }
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
