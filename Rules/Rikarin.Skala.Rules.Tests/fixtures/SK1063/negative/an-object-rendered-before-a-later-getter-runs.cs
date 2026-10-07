// ⚠ #425: `string.Format` evaluates every argument and then renders them; the handler renders each one
// as soon as it is evaluated. A builder rendered first and changed by the getter after it is "ab 1" one
// way and "a 1" the other.
using System.Text;

public sealed class Counter {
    readonly StringBuilder log;

    public Counter(StringBuilder log) => this.log = log;

    public int Next {
        get {
            log.Append('b');
            return 1;
        }
    }
}

public static class Probe {
    public static string Run() {
        var log = new StringBuilder("a");
        var counter = new Counter(log);
        return string.Format("{0} {1}", log, counter.Next);
    }
}
