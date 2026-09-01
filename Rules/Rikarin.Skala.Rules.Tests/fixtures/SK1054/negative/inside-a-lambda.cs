using System;

// A lambda body is its own declaration space; the local is the enclosing method's.
public sealed class Reader {
    static bool Try(out int value) {
        value = 1;
        return true;
    }

    public void Run(Action<Func<bool>> run) {
        int value;
        run(() => Try(out value));
    }
}
