using System;

// An invocation is not counted as a side effect. Treating every call as one would report every
// conditional invocation that takes an argument, which is most of them.
public sealed class Trace {
    public void Record(Action<string>? sink, int value) {
        sink?.Invoke(Format(value));
    }

    static string Format(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
