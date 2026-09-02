using System.IO;

public sealed class Conditional {
    public TextWriter Make(bool real) {
        var writer = real ? (TextWriter)new StringWriter() : null;
        return writer;
    }
}
