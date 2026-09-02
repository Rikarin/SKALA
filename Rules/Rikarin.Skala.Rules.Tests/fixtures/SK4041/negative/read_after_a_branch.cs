using System;
using System.Text;

public sealed class Report {
    // ⚠ Filled inside an `if` and read after it. A scan that stopped at the enclosing block would
    // report this, which is why the scan covers the whole member body.
    public void Write(string name, bool verbose) {
        var builder = new StringBuilder();
        if (verbose) {
            builder.Append(name);
        }

        Console.WriteLine(builder.ToString());
    }
}
