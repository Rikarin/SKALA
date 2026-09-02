using System.Text;

public sealed class Report {
    // ⚠ `Length` is a read. The buffer is being used as a counter, which is a strange thing to do
    // and not this rule's business.
    public int Write(string name) {
        var builder = new StringBuilder();
        builder.Append(name);
        return builder.Length;
    }
}
