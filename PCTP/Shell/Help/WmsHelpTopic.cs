using System;

namespace PCTP.Shell.Help
{
    internal sealed class WmsHelpTopic
    {
        internal WmsHelpTopic(
            string key,
            string title,
            string purpose,
            string preconditions,
            string steps,
            string confirmation,
            string commonErrors,
            string troubleshooting,
            string mermaid)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("key");

            Key = key;
            Title = title ?? string.Empty;
            Purpose = purpose ?? string.Empty;
            Preconditions = preconditions ?? string.Empty;
            Steps = steps ?? string.Empty;
            Confirmation = confirmation ?? string.Empty;
            CommonErrors = commonErrors ?? string.Empty;
            Troubleshooting = troubleshooting ?? string.Empty;
            Mermaid = mermaid ?? string.Empty;
        }

        // Public properties are intentional: WinForms ListBox.DisplayMember
        // resolves properties through reflection and cannot bind to internal-only properties.
        public string Key { get; private set; }
        public string Title { get; private set; }
        public string Purpose { get; private set; }
        public string Preconditions { get; private set; }
        public string Steps { get; private set; }
        public string Confirmation { get; private set; }
        public string CommonErrors { get; private set; }
        public string Troubleshooting { get; private set; }
        public string Mermaid { get; private set; }
    }
}
