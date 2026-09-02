using System.Collections.Generic;
using System.Text;

public sealed class Report {
    public void Write(IEnumerable<string> names) {
        var builder = new StringBuilder();
        foreach (var name in names) {
            builder.AppendLine(name);
        }
    }
}
