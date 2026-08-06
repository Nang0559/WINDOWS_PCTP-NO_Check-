using DevExpress.CodeParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PCTP.FuctionMain
{
    class ClassFunction
    {
         public string [] LOT (string QRIN)
        {
            string LOTOUT = "";
            string[] _LOTOUT;
            _LOTOUT = QRIN.Split(':');
            if(_LOTOUT.Length==1)
            {

            }
            else
            {

            }
            return _LOTOUT;
        }
        public List<string>  DLLOT (string LOT,bool isYAMH)
        {
            List<string>  _DLLOT = new List<string>();
            if (LOT.Length == 27)
            {
                if (isYAMH)
                {

                    _DLLOT.Add(LOT.Substring(0, 13).ToString());
                    _DLLOT.Add(LOT.Substring(0, 13).ToString().Substring(11, 1));
                    _DLLOT.Add(LOT.Substring(0, 13).ToString().Substring(12, 1));
                }
                else
                {
                    _DLLOT.Add(LOT.Substring(0, 12).ToString());
                    _DLLOT.Add(LOT.Substring(0, 12).ToString().Substring(11, 1));
                }
            }
            return _DLLOT;
        }
    }

}
