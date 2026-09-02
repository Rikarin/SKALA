using System.Text;

public sealed class Report {
    StringBuilder? kept;

    public void Write(string name) {
        var builder = new StringBuilder();
        builder.Append(name);
        kept = builder;
    }

    public override string ToString() => kept?.ToString() ?? string.Empty;
}
