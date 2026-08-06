using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.DHRepository
{
    public abstract class BaseRepository
    {
        protected string _connectionString;
    
    public enum TypeOfCheck
    {
        DoesExistinDB,DoesNotExistinDB
    }
    public enum RequestType
    {
        Add,Update,Read,Delete,ComfirmAdd,ConfirmDelete
    }
    }
}
