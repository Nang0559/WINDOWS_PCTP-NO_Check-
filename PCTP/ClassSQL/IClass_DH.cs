using System;

namespace PCTP.ClassSQL
{
    public interface IClass_DH
    {
        string ADDNM { get; set; }
        string CUA { get; set; }
        string DV { get; set; }
        int GIOGIAO { get; set; }
        int HOP { get; set; }
        string LOT { get; set; }
        string MAHANG { get; set; }
        DateTime NGAYGIAO { get; set; }
        string NHAMAY { get; set; }
        string Note { get; set; }
        int SOLUONG { get; set; }
        string STATUS { get; set; }
        string STATUSDOC { get; set; }
        int STT { get; set; }
        string TENHANG { get; set; }
        string TRUYEN { get; set; }
        string TTPHIEU { get; set; }
    }
}