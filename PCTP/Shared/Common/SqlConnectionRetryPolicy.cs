namespace PCTP.Shared.Common
{
    /// <summary>
    /// Conservative SQL connection-open retry classification.
    /// These numbers are not permission to replay SQL commands or transactions.
    /// </summary>
    public static class SqlConnectionRetryPolicy
    {
        public static bool IsTransientConnectionErrorNumber(int number)
        {
            switch (number)
            {
                case -2:      // connect timeout
                case 2:       // SQL network endpoint not found
                case 53:      // network path not found
                case 64:      // network name deleted
                case 233:     // connection initialization failure
                case 10053:   // connection aborted
                case 10054:   // connection reset
                case 10060:   // network timeout
                case 11001:   // host not found
                    return true;
                default:
                    return false;
            }
        }
    }
}
