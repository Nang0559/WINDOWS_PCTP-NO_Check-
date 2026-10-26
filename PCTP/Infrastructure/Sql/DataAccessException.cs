using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.ClassSQL
{
    public class DataAccessException : Exception
    {
        public DataAccessException()
        {
        }
        private DataAccessStatus _dataAccessStatus { get; set; }


        public DataAccessStatus DataAccessStatusInfo
        {
            get { return _dataAccessStatus; }
            set { _dataAccessStatus = value; }
        }

        public DataAccessException(DataAccessStatus dataAccessStatus)
        {
            _dataAccessStatus = dataAccessStatus;
        }
        public DataAccessException(string message, Exception innerException, DataAccessStatus dataAccessStatus) : base(message, innerException)
        {
            _dataAccessStatus = dataAccessStatus;
        }


    }
}
