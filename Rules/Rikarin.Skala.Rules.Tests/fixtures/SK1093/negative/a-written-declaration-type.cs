using System.IO;

public sealed class Written {
    public TextWriter Make() {
        TextWriter writer = (TextWriter)new StringWriter();
        return writer;
    }
}
