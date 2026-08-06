using System;
using System.Collections.Generic;
using System.Text;

namespace MyValidation.Eval
{
    public class Evalautor
    {
        System.Windows.Forms.Form form;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="form">Form contains components to be evaluated</param>
        public Evalautor(System.Windows.Forms.Form form)
        {
            this.form = form;
        }

        /// <summary>
        /// Evaluates an expression on form
        /// </summary>
        /// <param name="expression">Expression to evaluate</param>
        /// <returns>Evaluation result</returns>
        public bool Evaluate(string expression)
        {
            //expression type selection..
            Types.ITypeEvaluator evaluator = new Types.SimpleFieldEvaluator();
            evaluator.Form = form;

            return evaluator.Evaluate(expression);
         }
    }
}
