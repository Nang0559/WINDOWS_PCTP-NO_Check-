using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCTP.Models;
using System.Data.SqlClient;

namespace PCTP.DAL
{
    public class QrcodeDAL: XuLySqlServer
    {
        public List<QrcodeModels> sp_loadQRCode()
        {
            string json = JsonConvert.SerializeObject(LoadData("sp_loadDocQR"));
            return JsonConvert.DeserializeObject<List<QrcodeModels>>(json);

        }
        public int sp_themQR(QrcodeModels Qr_Rc)
        {
            return Execute("sp_themQR",new SqlParameter("@LOTFCC", Qr_Rc.LOTFCC ),
            new SqlParameter("@MAHANGFCC", Qr_Rc.MAHANGFCC) ,
            new SqlParameter("@SLTEMFCC", Qr_Rc.SLTEMFCC)
           , new SqlParameter("@LOTHVN", Qr_Rc.LOTHVN)
           , new SqlParameter("@MAHANGHVN", Qr_Rc.MAHANGHVN)
           , new SqlParameter("@SLTEMHVN", Qr_Rc.SLTEMHVN)
           , new SqlParameter("@STATUS", Qr_Rc.STATUS)
           , new SqlParameter("@MAFCC", Qr_Rc.MAFCC)
           , new SqlParameter("@STT", Qr_Rc.STT)
           , new SqlParameter("@KETQUA", Qr_Rc.KETQUA)
           , new SqlParameter("@CUA", Qr_Rc.CUA)
           , new SqlParameter("@TRUYEN", Qr_Rc.TRUYEN)
           , new SqlParameter("@GIO", Qr_Rc.GIO)
           , new SqlParameter("@STTBAN", Qr_Rc.GIO)
           , new SqlParameter("@SUALOTHVN", Qr_Rc.STTBAN)
           , new SqlParameter("@FindTem", Qr_Rc.FindTem));
           
        }
        public int sp_xoaQR(QrcodeModels Qr_Rc)
        {
            
            return Execute("Delete DOCQRCODE");
        }
    }
}
