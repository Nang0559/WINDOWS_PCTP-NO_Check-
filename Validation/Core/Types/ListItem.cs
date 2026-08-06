using System;
using System.Collections.Generic;
using System.Text;

namespace MyValidation.Core.Types
{
    public class ListItem : ITypeValidator
    {
        public string Validate(object value, System.Xml.XmlNode node)
        {
         
            if (int.Parse(value.ToString()) > 0)
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
