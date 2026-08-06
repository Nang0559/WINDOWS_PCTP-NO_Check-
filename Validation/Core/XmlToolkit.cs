using System;
using System.Collections;
using System.Data;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Text;

namespace MyValidation.Core
{
    internal class XmlToolkit
    {
        // Validation Error Message
        static string eMessages = "";

        public static void ValidationEventHandler(object sender, ValidationEventArgs args)
        {
            eMessages = eMessages + args.Message + "\n";
        }

        /// <summary>
        /// Validates given xml with given schema
        /// </summary>
        /// <param name="xml">Xml to validate</param>
        /// <param name="xsd">Schema</param>
        /// <returns>Validation result</returns>
        public static bool Validate(Stream xml, Stream xsd)
        {
            eMessages = "";

            XmlSchema schema = XmlSchema.Read(xsd, null);
            XmlReaderSettings settings = new XmlReaderSettings();

            settings.ValidationType = ValidationType.Schema;
            settings.ValidationEventHandler += new ValidationEventHandler(ValidationEventHandler);
            settings.Schemas.Add(schema);

            XmlReader reader = XmlReader.Create(xml, settings);

            while (reader.Read()) ;

            reader.Close();

            if (!String.IsNullOrEmpty(eMessages))
            {
                Console.WriteLine(eMessages);
                return false;
            }
            else
            {
                return true;
            }
        }

    }
}
