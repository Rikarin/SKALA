using System.Text;

public sealed class Report {
    string last = string.Empty;

    public void Append(StringBuilder? builder, string line) {
        builder?.AppendLine(last = line);
    }

    public string Last => last;
}
