using System;
using System.Collections.Generic;
using System.Text;

namespace MyValidation.Core.Types
{
    class RangeInt : ITypeValidator
    {
        public string Validate(object value, System.Xml.XmlNode node)
        {

            int min = node.Attributes["minValue"] == null ? Int32.MinValue : Int32.Parse(node.Attributes["minValue"].Value);
            int max = node.Attributes["maxValue"] == null ? Int32.MaxValue : Int32.Parse(node.Attributes["maxValue"].Value);

            int parsed;

            bool result = Int32.TryParse(value.ToString(), out parsed);

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
