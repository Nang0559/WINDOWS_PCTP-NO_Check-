using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using MyValidation.Core;

namespace MyValidation
{
    public class ValidatedForm : Form
    {
        protected ErrorProvider eProvider;
        protected Validator validator;
        public ValidatedForm()
        {
            eProvider = new ErrorProvider(this);
        }
    }
}
