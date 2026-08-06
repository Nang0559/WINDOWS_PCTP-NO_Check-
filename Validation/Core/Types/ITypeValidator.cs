using System;
using System.Collections.Generic;
using System.Text;

namespace MyValidation.Core.Types
{
    /// <summary>
    ///  Type validator interface.
    /// </summary>
    interface ITypeValidator
    {
        /// <summary>
        /// Validate function.
        /// </summary>
        /// <param name="value">Value to be validated.</param>
        /// <param name="node">XmlNode that contains validation rule.</param>
        /// <returns>Emply line if success else error message.</returns>
        string Validate(object value, System.Xml.XmlNode node);
    }
}
