using System;
using System.Collections.Generic;
using System.Text;

namespace MyValidation.Core.Types
{
    class RangeDouble : ITypeValidator
    {
        public string Validate(object value, System.Xml.XmlNode node)
        {
            double min = node.Attributes["minValue"] == null ? Double.MinValue : Double.Parse(node.Attributes["minValue"].Value);
            double max = node.Attributes["maxValue"] == null ? Double.MaxValue : Double.Parse(node.Attributes["maxValue"].Value);

            double parsed;

            bool result = Double.TryParse(value.ToString(), out parsed);

            if (result && parsed > min && parsed < max)
            {
                return String.Empty;
            }
            else
            {
                return node.Attributes["errorMessage"].Value
                    .Replace("{v:min}", min.ToString())
                    .Replace("{v:max}", max.ToString())
                    .Replace("{v:value}", value.ToString());
            }

        }
    }
}
