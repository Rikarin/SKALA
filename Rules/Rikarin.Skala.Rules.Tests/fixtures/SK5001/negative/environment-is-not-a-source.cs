using System;
using System.Data;

// The process's own configuration, set by whoever started it — the same principal as argv.
public static class Report {
    public static void Run(IDbCommand command) {
        var tenant = Environment.GetEnvironmentVariable("TENANT");
        command.CommandText = "select * from rows where tenant = '" + tenant + "'";
    }
}
