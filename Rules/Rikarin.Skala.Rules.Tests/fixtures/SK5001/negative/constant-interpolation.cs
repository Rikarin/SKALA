using System.Data;
using System.Net;

// An interpolated string whose holes are all constants is a constant. Building a statement out of
// schema names this way is ordinary and carries nothing an attacker chose.
public static class Migrations {
    const string VersionTable = "schema_version";

    public static void Read(HttpListenerRequest request, IDbCommand command) {
        command.CommandText = $"select coalesce(max(version), 0) from {VersionTable}";
    }
}
