using System.Text;

public sealed class Report {
    // ⚠ The callee may read it, store it or return its text, and nothing here is obliged to say so.
    public void Write(string name) {
        var builder = new StringBuilder();
        builder.Append(name);
        Emit(builder);
    }

    static void Emit(StringBuilder builder) => System.Console.WriteLine(builder.ToString());
}
