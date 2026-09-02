using System.Text;

public sealed class Report {
    public void Write(string name) {
        var builder = new StringBuilder();
        builder.Append(name);
    }
}
