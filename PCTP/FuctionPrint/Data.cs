using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.FuctionPrint
{
    public class Record
    {
        //public Record()
        //{
        //}
        public int STT { get; set; }

        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string Model { get; set; }
        public DateTime DocDate { get; set; }
        public string ItemLotCode { get; set; }
        public int ShiftCode { get; set; }
        public int QCDG { get; set; }
        public int Quantity9 { get; set; }
        public int SLG{ get; set; }
        public bool State { get; set; }
        public string QRCODE { get; set; }

    }
    public class DetailGL
    {
        public int? STT { get; set; }
        public int Quantity9 { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string ItemLotCode { get; set; }
        public int ShiftCode { get; set; }
        public string Model { get; set; }
        public string MO { get; set; }
        public DateTime DocDate { get; set; }
        public string QRCODE { get; set; }
    }
    //public class DataHelper
    //{
    //    public static BindingList<DetailGL> GetData(int count, int Rec)
    //    {
    //        BindingList<Record> records = new BindingList<Record>();
    //        BindingList<DetailGL> GL = new BindingList<DetailGL>();
            
    //    }
    //    public static BindingList<Record> GetData(int count)
    //    {
    //        BindingList<Record> records = new BindingList<Record>();
    //        for (int i = 0; i < count; i++)
    //            records.Add(new Record()
    //            {
    //                ID = i,
    //                ParentID = i % 5,
    //                Text = i % 2 == 0 ? string.Format("CText {0}", i) : string.Format("DText {0}", i),
    //                Dt = DateTime.Now.AddDays(i),
    //                State = i % 2 == 0,
    //                Image = SystemIcons.Information.ToBitmap(),
    //            });
    //        return records;
    //    }
    //    public static BindingList<Detail> GetDetailData(int count)
    //    {
    //        BindingList<Detail> records = new BindingList<Detail>();
    //        for (int i = 0; i < count; i++)
    //            records.Add(new Detail()
    //            {
    //                ID = i,
    //                Text = string.Format("Text Text Text Text Text Text Text Text Text Text Text Text Text{0}", i),
    //                Info = string.Format("Info {0}", i)
    //            });
    //        return records;
    //    }

    //}
}
