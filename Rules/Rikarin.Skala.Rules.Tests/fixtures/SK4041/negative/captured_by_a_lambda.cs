using System;
using System.Text;

public sealed class Report {
    // ⚠ Where a captured reference runs is not where it is written: the delegate may be stored,
    // returned or run later, and whether the buffer is read is then a question about its callers.
    public Action Write(string name) {
        var builder = new StringBuilder();
        builder.Append(name);
        return () => Console.WriteLine(builder.ToString());
    }
}
