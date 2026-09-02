using System;
using System.Text;

public sealed class Report {
    // ⚠ Two names for one buffer. The second name is what reads it, and the first has no way to
    // know that without following the alias — so a reference that is not a write ends the analysis.
    public void Write(string name) {
        var builder = new StringBuilder();
        builder.Append(name);
        var same = builder;
        Console.WriteLine(same.ToString());
    }
}
