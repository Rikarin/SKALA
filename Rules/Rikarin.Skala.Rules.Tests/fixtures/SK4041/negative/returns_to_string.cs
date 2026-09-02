using System.Text;

public sealed class Report {
    public string Write(string name) {
        var builder = new StringBuilder();
        builder.Append(name);
        return builder.ToString();
    }
}
