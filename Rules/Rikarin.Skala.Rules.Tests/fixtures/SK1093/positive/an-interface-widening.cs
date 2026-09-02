using System.IO;

public sealed class Writers {
    public TextWriter Make() {
        var writer = (TextWriter)new StringWriter();
        return writer;
    }
}
