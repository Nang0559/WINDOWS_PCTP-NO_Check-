using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Shell.Help
{
    internal sealed class WmsHelpService
    {
        private readonly IDictionary<string, WmsHelpTopic> _topics;

        internal WmsHelpService()
        {
            _topics = WmsHelpCatalog.GetAll()
                .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        }

        internal void Show(Control owner, string topicKey)
        {
            WmsHelpTopic topic = Get(topicKey);
            FormWmsHelp form = new FormWmsHelp(topic, this);
            if (owner != null)
                form.Show(owner);
            else
                form.Show();
        }

        internal void ShowCurrent(Control owner)
        {
            string key = WmsHelpContext.Resolve(owner);
            Show(owner, key);
        }

        internal WmsHelpTopic Get(string topicKey)
        {
            WmsHelpTopic topic;
            if (!string.IsNullOrWhiteSpace(topicKey) && _topics.TryGetValue(topicKey, out topic))
                return topic;

            return _topics["Dashboard"];
        }

        internal IList<WmsHelpTopic> GetAll()
        {
            return _topics.Values.OrderBy(x => x.Title).ToList();
        }
    }
}
