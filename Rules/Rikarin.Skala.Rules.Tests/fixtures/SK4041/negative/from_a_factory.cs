using System.Text;

public sealed class Report {
    static readonly StringBuilder Shared = new();

    // ⚠ The builder is somebody else's. "Nothing in this method reads it" says nothing at all about
    // a buffer that was handed over, which is why the initializer has to be a construction.
    public void Write(string name) {
        var builder = Rent();
        builder.Append(name);
    }

    static StringBuilder Rent() => Shared;
}
