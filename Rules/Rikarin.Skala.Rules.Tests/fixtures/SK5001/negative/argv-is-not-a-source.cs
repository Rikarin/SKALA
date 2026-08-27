using System.Data;

// A command-line tool's argv comes from the principal the process runs as. There is no trust
// boundary being crossed, so there is nothing to inject across.
public static class Tool {
    public static void Main(string[] args, IDbCommand command) {
        command.CommandText = "select * from files where path = '" + args[0] + "'";
    }
}
