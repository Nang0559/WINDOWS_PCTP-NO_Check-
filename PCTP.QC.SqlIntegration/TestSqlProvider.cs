namespace PCTP.ClassSQL;

// Test-only stand-in for the legacy provider. The production UnitOfWork only
// consumes this connection-string property; no production credentials/defaults
// are compiled into the integration harness.
public sealed class SQLPROVIDER
{
    public string B7R2_FCCdb { get; set; } = string.Empty;
}
