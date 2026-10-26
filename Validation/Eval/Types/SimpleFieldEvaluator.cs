using System;
using System.Collections.Generic;
using System.Text;

namespace MyValidation.Eval.Types
{
    public class SimpleFieldEvaluator : ITypeEvaluator
    {
        private System.Windows.Forms.Form form;
        public System.Windows.Forms.Form Form
        {
            set
            {
                form = value;
            }
        }

        private object GetFormField(string name)
        {
            return form.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(form);
        }

        private object GetControlProperty(object control, string name)
        {
            return control.GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(control, null);
        }

        // <Field> <operand> <value>
        public bool Evaluate(string expression)
        {
            // [0] : Field  [1] : Operand [2] : Value
            string[] parts = expression.Trim().Replace("  ", " ").Split(' ');

            object control = GetFormField(parts[0].Split('.')[0]);
            object field = GetControlProperty(control, parts[0].Split('.')[1]);


            switch (parts[1]) //operator
            {
                case ">":
                    if (Double.Parse(field.ToString()) > Double.Parse(parts[2]))
                    {
                        return true;
                    }
                    break;

                case "<":
                    if (Double.Parse(field.ToString()) < Double.Parse(parts[2]))
                    {
                        return true;
                    }
                    break;

                case "=": //operand type determination
                    
                    double d;
                    bool b;

                    if (Double.TryParse(parts[2], out d) && Double.Parse(field.ToString()) == d)
                    {
                        return true;
                    }
                    else if (Boolean.TryParse(parts[2], out b) && Boolean.Parse(field.ToString()) == b)
                    {
                        return true;
                    }
                    else if (parts[2].Equals(field.ToString(), StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                    break;

                case "!=": //operand type determination
                    
                    double dd;
                    bool bb;

                    if (Double.TryParse(parts[2], out dd) && Double.Parse(field.ToString()) != dd)
                    {
                        return true;
                    }
                    else if (Boolean.TryParse(parts[2], out bb) && Boolean.Parse(field.ToString()) != bb)
                    {
                        return true;
                    }
                    else if (!parts[2].Equals(field.ToString(), StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                    break;
            }
            return false;
        }
    }
}
