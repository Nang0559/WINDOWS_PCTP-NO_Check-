using System;
using System.Collections.Generic;
using System.Text;

namespace MyValidation.Core.Types
{
    public class Required : ITypeValidator
    {
        public string Validate(object value, System.Xml.XmlNode node)
        {
            if (value != null && value.ToString().Length > 0)
            {
                return String.Empty;
            }
            else
            {
                return node.Attributes["errorMessage"].Value
                    .Replace("{v:value}", value.ToString());
            }
        }
    }
}
