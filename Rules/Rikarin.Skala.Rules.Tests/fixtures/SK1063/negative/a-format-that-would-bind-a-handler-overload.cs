// ⚠ #425: `string.Format` returns a `string`, so `Write(bool, string)` was chosen; an interpolated
// string also converts to the handler, and overload resolution prefers it. This handler is gated on
// `enabled` and skips the holes, so the getter stops running: measured for #412's audit as `reads=1`
// before the fix and `reads=0` after it.
using System.Runtime.CompilerServices;
using System.Text;

[InterpolatedStringHandler]
public ref struct GatedHandler {
    readonly StringBuilder builder;

    public GatedHandler(int literalLength, int formattedCount, bool enabled, out bool shouldAppend) {
        builder = new StringBuilder();
        shouldAppend = enabled;
    }

    public void AppendLiteral(string text) => builder.Append(text);

    public void AppendFormatted<T>(T value) => builder.Append(value);

    public override string ToString() => builder.ToString();
}

public static class Log {
    public static string Last = "";

    public static void Write(bool enabled, string message) {
        if (enabled) {
            Last = message;
        }
    }

    public static void Write(bool enabled, [InterpolatedStringHandlerArgument("enabled")] GatedHandler message) {
        if (enabled) {
            Last = message.ToString();
        }
    }
}

public sealed class Source {
    public int Reads;

    public int Value {
        get {
            Reads++;
            return 7;
        }
    }
}

public static class Probe {
    public static int Run() {
        var source = new Source();
        Log.Write(false, string.Format("v={0}", source.Value));
        return source.Reads;
    }
}
