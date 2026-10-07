// To a plain `string`: the numbered holes, an `int` whose `ToString()` the handler reproduces, and a
// literal the text can hold, with a builder whose rendering nothing after it can change. Pinned by
// Probe (#425).
using System.Text;

public static class Probe {
    public static string Run() {
        var done = 3;
        var total = 4;
        var name = "x";
        var log = new StringBuilder("a");
        var first = string.Format("{0} of {1} by {2}", done, total, name);
        var second = $"{done.ToString()} items {"left"}";
        var third = string.Format("{0} then {1}", log, done);
        return first + "/" + second + "/" + third;
    }
}
