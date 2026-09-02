using System.Text;

public sealed class Report {
    public void Write(string first, string second) {
        var builder = new StringBuilder();
        builder.Append(first).Append(second).AppendLine();
    }
}
