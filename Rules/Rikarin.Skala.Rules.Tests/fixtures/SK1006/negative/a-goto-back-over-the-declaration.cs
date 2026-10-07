// ⚠ #425: a `goto` may not jump back to a label before a `using` declaration in the same block
// (CS8649). Measured for #412's audit: this retry loop opens and disposes `r1`, then `r2`, and stops
// compiling once the statement becomes a declaration.
using System.Text;

public sealed class Resource : System.IDisposable {
    readonly StringBuilder log;
    readonly int number;

    public Resource(StringBuilder log, int number) {
        this.log = log;
        this.number = number;
    }

    public void Dispose() => log.Append("dispose ").Append(number).Append(';');
}

public static class Probe {
    static void Retry(StringBuilder log) {
        var attempt = 0;
    again:
        attempt++;
        log.Append("open ").Append(attempt).Append(';');
        using (var resource = new Resource(log, attempt)) {
            if (attempt < 2) {
                goto again;
            }

            log.Append("body;");
        }
    }

    public static string Run() {
        var log = new StringBuilder();
        Retry(log);
        return log.ToString();
    }
}
