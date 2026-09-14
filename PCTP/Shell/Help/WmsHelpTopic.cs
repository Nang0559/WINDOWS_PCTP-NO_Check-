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

        internal string Key { get; private set; }
        internal string Title { get; private set; }
        internal string Purpose { get; private set; }
        internal string Preconditions { get; private set; }
        internal string Steps { get; private set; }
        internal string Confirmation { get; private set; }
        internal string CommonErrors { get; private set; }
        internal string Troubleshooting { get; private set; }
        internal string Mermaid { get; private set; }
    }
}
