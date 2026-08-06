using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.IO;

namespace MyValidation.Core
{
    public class Validator
    {
        private ValidatedForm form;
        private IList<string> rules;
        private Dictionary<string, Types.ITypeValidator> typeValidators;
        private Eval.Evalautor eval;
        private bool result;
       
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="form">Form to validate</param>
        public Validator(ValidatedForm form)
        {
            this.form = form;
            rules = new List<string>();
            typeValidators = new Dictionary<string, MyValidation.Core.Types.ITypeValidator>();

            //dynamically load all validators
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type t in types)
            {
                // If object implemented ITypeValidator Interface
                if (t.GetInterface("ITypeValidator", true) != null)
                {
                    // use classname as index
                    typeValidators.Add(t.Name.ToLower(), (Types.ITypeValidator)Activator.CreateInstance(null, t.FullName).Unwrap());
                }
            }

            eval = new Eval.Evalautor(form);
        }

        /// <summary>
        /// Adds rule xml
        /// </summary>
        /// <param name="xmlPath">Path of the xml. Xml must be "embedded source"</param>
        public void AddRule(string xmlPath)
        {
            
            bool res = XmlToolkit.Validate(Assembly.GetEntryAssembly().GetManifestResourceStream(xmlPath),
                                           Assembly.GetExecutingAssembly().GetManifestResourceStream("Validation.Rule.xsd"));


            if (res)
            {
                rules.Add(xmlPath);
            }
            else
            {
                throw new Exception("Invalid rule xml format");
            }
        }

        /// <summary>
        /// Gets a control object from form using reflection
        /// </summary>
        /// <param name="name">Control name</param>
        /// <returns>Control object</returns>
        private object GetFormField(string name)
        {
            return form.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(form);
        }

        /// <summary>
        /// Gets value of a propery from control using reflection
        /// </summary>
        /// <param name="control">Control object</param>
        /// <param name="name">Property name</param>
        /// <returns>Value of propery</returns>
        private object GetControlProperty(object control, string name)
        {
            return control.GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(control, null);
        }

        /// <summary>
        /// Checks rules of an xml
        /// </summary>
        /// <param name="ruleXml">Rule xml</param>
        private void CheckRules(string ruleXml)
        {
            //load Rule Xml
            Stream fileStream = Assembly.GetEntryAssembly().GetManifestResourceStream(ruleXml);
            XmlDocument ruleSet = new XmlDocument();
            ruleSet.Load(fileStream);

            XmlNode rules = ruleSet.SelectSingleNode("rules");

            //get ErrorProvider
            ErrorProvider eProvider = GetFormField("eProvider") as ErrorProvider;

            //parse and check Rules
            foreach (XmlNode node in rules.ChildNodes)
            {
                if (node.NodeType == XmlNodeType.Comment)
                {
                    continue;
                }
                if(node.Attributes["maxValue"] != null)
                {
                    node.Attributes["maxValue"].Value = "5";
                    ruleSet.Save(Application.StartupPath + "/ RuleSet.xml");
                }    
                //evaluate when condition first, if any
                if (node.Attributes["when"] != null && !eval.Evaluate(node.Attributes["when"].Value))
                {
                    continue;
                }

                string[] parts = node.Attributes["target"].Value.Split('.');

                Control control = GetFormField(parts[0]) as Control;
                object value = GetControlProperty(control, parts[1]);

                // go deeper, if any ..
                for (int i = 2; i < parts.Length; i++)
                {
                    value = GetControlProperty(value, parts[i]);
                }
                

                //find appropriate validator, by node name
                string msg = typeValidators[node.Name.ToLower()].Validate(value, node);

                if (!String.IsNullOrEmpty(msg))
                {
                    eProvider.SetError(control, msg);
                    result = false;
                }
                else
                {
                    eProvider.SetError(control, "");
                }
            }
        }

        /// <summary>
        /// Validates form based on rules added by .AddRule()
        /// Shows error messages if any
        /// </summary>
        /// <returns>Validation result</returns>
        public bool Validate()
        {
            if (form == null)
            {
                throw new Exception();
            }
            result = true;
            ((ErrorProvider)GetFormField("eProvider")).Clear();

            foreach (string ruleXml in rules)
            {
                CheckRules(ruleXml);
            }

            return result;
        }
    }
}
