using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MyValidation.Core.Types
{
    class RegExp : ITypeValidator
    {
        public string Validate(object value, System.Xml.XmlNode node)
        {
            Regex regex = new Regex(node.Attributes["exp"].Value);
            Match m = regex.Match(value.ToString());

            if (m.Success)
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
