using System.Collections.Generic;

namespace PCTP.VIEWSTOCK.Services
{
    internal class NhapKhoCompletedEvent
    {
        private int soLot;
        private List<string> errors;

        public NhapKhoCompletedEvent(int soLot, List<string> errors)
        {
            this.soLot = soLot;
            this.errors = errors;
        }
    }
}