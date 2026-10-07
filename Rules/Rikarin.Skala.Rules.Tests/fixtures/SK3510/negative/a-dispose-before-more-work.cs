using System;
using System.Text;

// ⚠ #412's audit: the explicit call is the disposal the next statement relies on — there a writer's
// flush read back through the file. Deleting it moves the disposal to the end of the scope.
public sealed class Buffered : IDisposable {
    readonly StringBuilder _target;
    readonly StringBuilder _pending = new();

    public Buffered(StringBuilder target) => _target = target;

    public void Write(string text) => _pending.Append(text);

    public void Dispose() {
        _target.Append(_pending);
        _pending.Clear();
    }
}

public static class Probe {
    public static string Run() {
        var target = new StringBuilder();
        using var writer = new Buffered(target);
        writer.Write("hello");
        writer.Dispose();
        return "read back: '" + target + "'";
    }
}
