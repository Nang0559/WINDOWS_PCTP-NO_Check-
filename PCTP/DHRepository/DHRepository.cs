using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.DHRepository
{
    public class DHRepository:BaseRepository
    {
        public DHRepository()
        {

        }
        public DHRepository(string ConnectionString)
        {
            _connectionString = ConnectionString;
        }
        public IEnumerable<Class_DH> GetDH(string NM,DateTime NgayGiao,string GG)
        {
            List<Class_DH> class_DHList = new List<Class_DH>();
            DataAccessStatus dataAccessStatus = new DataAccessStatus();
            bool MatchingRecordFound = false;
            string sql = "elect STT,CUA, TRUYEN,MAHANG, TENHANG, LOT,DV,SOLUONG, NGAYGIAO,GIOGIAO,STATUS,TTPHIEU as TTPHIEU,NHAMAY,@ADDNM,HOP,STATUSDOC,Note from LUUPHIEUGIAOHANG " +
                          " where NHAMAY like @NHAMAY and NGAYGIAO = @NGAYGIAO  and GIOGIAOFCC = @GIOFCC ";

            using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
            {
                try
                {
                    sqlConnection.Open();

                    using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                    {
                        cmd.CommandText = sql;
                        cmd.Prepare();
                        cmd.Parameters.Add(new SqlParameter("@NHAMAY", NM));
                        cmd.Parameters.Add(new SqlParameter("@NGAYGIAO", NgayGiao));
                        cmd.Parameters.Add(new SqlParameter("@GIOFCC", GG));

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            MatchingRecordFound = reader.HasRows;
                            while (reader.Read())
                            {
                                Class_DH class_dh = new Class_DH();
                                class_dh.STT = int.Parse(reader["STT"].ToString());
                                class_dh.CUA = reader["PART"].ToString();
                                class_dh.TRUYEN = reader["NAME"].ToString();
                                class_dh.MAHANG = reader["MAHANG"].ToString();
                                class_dh.TENHANG = reader["TENHANG"].ToString();
                                class_dh.LOT = reader["LOT"].ToString();
                                class_dh.DV = reader["DV"].ToString();

                                class_dh.SOLUONG = int.Parse(reader["SOLUONG"].ToString());

                                class_dh.NGAYGIAO = DateTime.Parse(reader["NGAYGIAO"].ToString());
                                class_dh.GIOGIAO = int.Parse(reader["SLNHAP"].ToString());
                                class_dh.STATUS =  reader["STATUS"].ToString();
                                class_dh.TTPHIEU = reader["TTPHIEU"].ToString();
                                class_dh.NHAMAY =  reader["NHAMAY"].ToString();

                                class_dh.ADDNM = reader["ADDNM"].ToString();
                                class_dh.HOP = int.Parse(reader["HOP"].ToString());
                                class_dh.STATUSDOC = reader["STATUSDOC"].ToString();
                                class_dh.Note = reader["Note"].ToString();
                                class_DHList.Add(class_dh);
                            }
                        }
                        sqlConnection.Close();
                    }
                }
                catch (SqlException e)
                {
                    dataAccessStatus.setValues(status: "Error", operationSucceeded: false, exceptionMessage: e.Message, customMessage: "Unable to get Department Model for requested ID", helpLink: e.HelpLink, errorCode: e.ErrorCode, stackTrace: e.StackTrace);

                    throw new DataAccessException(e.Message, e.InnerException, dataAccessStatus);
                    //throw e;
                }

                if (!MatchingRecordFound)
                {
                   // dataAccessStatus.setValues(status: "Error", operationSucceeded: false, exceptionMessage: "", customMessage: $"Record not found. Unable to get Department Model for Department ID {LotNo}. Id {LotNo} does not exist in the database.", helpLink: "", errorCode: 0, stackTrace: "");

                    throw new DataAccessException(dataAccessStatus);
                    //throw e;
                }

                return class_DHList;
            }
        }
        public void Update(IClass_DH stockModel)
        {
            int result = -1;
            DataAccessStatus dataAccessStatus = new DataAccessStatus();

            using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
            {
                try
                {
                    sqlConnection.Open();
                }
                catch (SqlException e)
                {
                    dataAccessStatus.setValues(status: "Error", operationSucceeded: false, exceptionMessage: e.Message, customMessage: "Unable to update DepartmentModel. Could not open a database connection", helpLink: e.HelpLink, errorCode: e.ErrorCode, stackTrace: e.StackTrace);

                    throw new DataAccessException(e.Message, e.InnerException, dataAccessStatus);
                }

                string updateSql =
                       "UPDATE luuphieugiaohang "
                     + "SET STATUS = @STATUS "
                     
                     + "where NHAMAY like @NHAMAY and NGAYGIAO = @NGAYGIAO  and GIOGIAOFCC = @GIOFCC ";

                

                using (SqlCommand cmd = new SqlCommand(null, sqlConnection))
                {
                    try
                    {
                        RecordExistsCheck(cmd, stockModel, TypeOfCheck.DoesExistinDB, RequestType.Update);
                    }
                    catch (DataAccessException ex)
                    {
                        ex.DataAccessStatusInfo.CustomMessage = "Stock Model could not be updated because it is not in the database.";
                        ex.DataAccessStatusInfo.ExceptionMessage = string.Copy(ex.Message);
                        ex.DataAccessStatusInfo.StackTrace = string.Copy(ex.StackTrace);
                        throw ex;
                    }

                    cmd.CommandText = updateSql;
                    cmd.Prepare();
                    cmd.Parameters.AddWithValue("@NHAMAY", stockModel.NHAMAY);
                    cmd.Parameters.AddWithValue("@NGAYGIAO", stockModel.NGAYGIAO);
                    cmd.Parameters.AddWithValue("@GIOFCC", stockModel.GIOGIAO);
                    try
                    {
                        result = cmd.ExecuteNonQuery();
                    }
                    catch (SqlException e)
                    {
                        dataAccessStatus.setValues(status: "Error", operationSucceeded: false, exceptionMessage: String.Copy(e.Message), customMessage: "Unable to update Stock Model", helpLink: String.Copy(e.HelpLink), errorCode: e.ErrorCode, stackTrace: String.Copy(e.StackTrace));

                        throw new DataAccessException(e.Message, e.InnerException, dataAccessStatus);
                    }
                }
                sqlConnection.Close();
            }
        }
        private bool RecordExistsCheck(SqlCommand cmd, IClass_DH stockModel, TypeOfCheck typeOfExistenceCheck, RequestType requestType)
        {
            Int32 countOfRecsFound = 0;
            bool RecordExistsCheckPassed = true;

            DataAccessStatus dataAccessStatus = new DataAccessStatus();

            cmd.Prepare();

            if ((requestType == RequestType.Add) || (requestType == RequestType.ComfirmAdd))
            {
                cmd.CommandText = "Select count(*) from luuphieugiaohang where  where NHAMAY like @NHAMAY and NGAYGIAO = @NGAYGIAO  and GIOGIAOFCC = @GIOFCC ";
                cmd.Parameters.AddWithValue("@NHAMAY", stockModel.NHAMAY);
                cmd.Parameters.AddWithValue("@NGAYGIAO", stockModel.NGAYGIAO);
                cmd.Parameters.AddWithValue("@GIOFCC", stockModel.GIOGIAO);
            }
           // else if ((requestType == RequestType.Update) || (requestType == RequestType.ConfirmDelete) || (requestType == RequestType.Detete))
            {
                cmd.CommandText = "Select count(*) from luuphieugiaohang where  where NHAMAY like @NHAMAY and NGAYGIAO = @NGAYGIAO  and GIOGIAOFCC = @GIOFCC ";
                cmd.Parameters.AddWithValue("@NHAMAY", stockModel.NHAMAY);
                cmd.Parameters.AddWithValue("@NGAYGIAO", stockModel.NGAYGIAO);
                cmd.Parameters.AddWithValue("@GIOFCC", stockModel.GIOGIAO);
            }

            try
            {
                countOfRecsFound = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (SqlException e)
            {
                string msg = e.Message;
                throw;
            }

            if ((typeOfExistenceCheck == TypeOfCheck.DoesNotExistinDB) && (countOfRecsFound > 0))
            {
                dataAccessStatus.Status = "Error";
                RecordExistsCheckPassed = false;

                throw new DataAccessException(dataAccessStatus);
            }
            else if ((typeOfExistenceCheck == TypeOfCheck.DoesExistinDB) && (countOfRecsFound == 0))
            {
                dataAccessStatus.Status = "Error";
                RecordExistsCheckPassed = false;
                throw new DataAccessException(dataAccessStatus);
            }
            return RecordExistsCheckPassed;
        }

    }
}
