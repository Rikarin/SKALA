using System.IO;

public sealed class Documented {
    public TextWriter Make() {
        // Widened so the caller cannot reach the buffer.
        var writer = (TextWriter)new StringWriter();
        return writer;
    }
}
