using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>Reads configured menu rows for the shell (migration 055). No designer permission needed: presentation data only.</summary>
    public interface IDocumentMenuRuntimeStore
    {
        IReadOnlyList<DocumentMenuEntry> ListConfigured();
    }

    /// <summary>
    /// SQL only supplies placement and text; the C# registry decides that the screen exists and the screen's own VIEW
    /// permission decides who sees it. Inactive entries are retained so the shell can hide a known menu item.
    /// </summary>
    public static class DocumentMenuProvider
    {
        public static IReadOnlyList<DocumentMenuEntry> Resolve(IDocumentRegistry registry, IDocumentMenuRuntimeStore store,
            Func<string, bool> canView)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (store == null) throw new ArgumentNullException("store");
            if (canView == null) throw new ArgumentNullException("canView");

            var result = new List<DocumentMenuEntry>();
            foreach (var e in store.ListConfigured())
            {
                DocumentFormDefinition def;
                if (e == null || !DocumentMenuGroups.IsAllowed(e.MenuGroup)) continue;
                if (string.IsNullOrWhiteSpace(e.MenuText)) continue;
                if (!registry.TryGet(e.ModuleCode, out def)) continue;
                if (!canView(def.ViewFunctionCode)) continue;
                result.Add(e);
            }
            return result
                .OrderBy(x => DocumentMenuGroups.All.ToList().IndexOf(x.MenuGroup))
                .ThenBy(x => x.SequenceNo)
                .ThenBy(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}