using System;
using System.Collections.Generic;
using System.Text;

namespace MyValidation.Eval.Types
{
    public interface ITypeEvaluator
    {
        System.Windows.Forms.Form Form
        {
            set;
        }

        /// <summary>
        /// Evaluate function.
        /// </summary>
        /// <param name="expression">Expression to be validated.</param>
        /// <returns>Evaluation result</returns>
        bool Evaluate(string expression);
    }
}
